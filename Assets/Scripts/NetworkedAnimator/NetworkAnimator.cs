using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public class NetworkAnimator : NetworkBehaviour
{
    [Header("Authoring Data")]
    public AnimMasterProfileSO Profile;

    [Header("Engine References")]
    public Animator UnityAnimator; // The dummy animator component

    // The Network State
    [Networked] public NetworkedAnimState AnimState { get; set; }

    // The Playables Graph
    private PlayableGraph _graph;
    private AnimationLayerMixerPlayable _masterMixer;
    private AnimationMixerPlayable[] _stateMixers; // Array index perfectly matches StateID

    private float _visualTime;

    // The Blackboard
    private IAnimVarSpeed _speedProvider;

    [Header("Deterministic Rigging")]
    public List<DeterministicAimConstraint> AimConstraints = new List<DeterministicAimConstraint>();

    #region AnimParameters 
    [SerializeField]
    private float[] _floatParams;
    [SerializeField]
    private bool[] _boolParams;

    [SerializeField]
    private float[] _floatVisualParams;
    private float[] _floatVisualVelocities;
    [SerializeField]
    private bool[] _boolVisualParams;

    private Dictionary<string, int> _floatNameMap = new Dictionary<string, int>();
    private Dictionary<string, int> _boolNameMap = new Dictionary<string, int>();

    private Dictionary<string, int> _floatVisualNameMap = new Dictionary<string, int>();
    private Dictionary<string, int> _boolVisualNameMap = new Dictionary<string, int>();

    [Networked, Capacity(32)]
    public NetworkArray<int> TriggerTicks { get; }
    private Dictionary<string, int> _triggerNameMap = new Dictionary<string, int>();

    public Transform RootMotionBone;
    [Tooltip("The local bind pose to lock the bone to so the mesh doesn't double-dip")]
    public Vector3 RootBoneBindPose;

    public Quaternion RootBoneBindRot = Quaternion.identity;
    [Tooltip("Multiplier to fix 0.01 scaled armatures")]
    public Vector3 RootMotionScale = Vector3.one;

    public AnimationClip BindPoseClip;

    [Header("Root Motion Axis Alignment")]
    [Tooltip("Which axis on the bone points FORWARD relative to the character?")]
    public BoneAxis ForwardAxis = BoneAxis.Z;

    [Tooltip("Which axis on the bone points UP relative to the character?")]
    public BoneAxis UpAxis = BoneAxis.Y;
    public void InitializeParameters()
    {
        _floatParams = new float[Profile.FloatParameters.Count];
        _boolParams = new bool[Profile.BoolParameters.Count];
        _floatVisualParams = new float[Profile.FloatParameters.Count];
        _boolVisualParams = new bool[Profile.BoolParameters.Count];
        _floatVisualVelocities = new float[Profile.FloatParameters.Count];

        for (int i = 0; i < Profile.FloatParameters.Count; i++)
        {
            var p = Profile.FloatParameters[i];
            _floatNameMap[p.Name] = i;
            _floatVisualNameMap[p.Name] = i;
            _floatParams[i] = p.DefaultValue;
            _floatVisualParams[i] = p.DefaultValue;
        }

        for (int i = 0; i < Profile.BoolParameters.Count; i++)
        {
            var p = Profile.BoolParameters[i];
            _boolNameMap[p.Name] = i;
            _boolVisualNameMap[p.Name] = i;
            _boolParams[i] = p.DefaultValue;
            _boolVisualParams[i] = p.DefaultValue;
        }

        for (int i = 0; i < Profile.TriggerParameters.Count; i++)
        {
            if (i >= 32)
            {
                Debug.LogError("[NetworkAnimator] Too many triggers! Increase NetworkArray Capacity.");
                break;
            }
            _triggerNameMap[Profile.TriggerParameters[i].Name] = i;
        }
    }

    public void SetSimFloat(string name, float value)
    {
        if (_floatNameMap.TryGetValue(name, out int index))
            _floatParams[index] = value;
        else
            Debug.LogWarning($"[NetworkAnimator] Float parameter '{name}' not found in Profile!");
    }

    public void SetRenderFloat(string name, float targetValue, float dampTime, float deltaTime)
    {
        if (_floatVisualNameMap.TryGetValue(name, out int index))
        {
            _floatVisualParams[index] = Mathf.SmoothDamp(
                _floatVisualParams[index],
                targetValue,
                ref _floatVisualVelocities[index],
                dampTime,
                Mathf.Infinity,
                deltaTime
            );
        }
        else
        {
            Debug.LogWarning($"[NetworkAnimator] Float parameter '{name}' not found in Profile!");
        }
    }

    public void SetSimBool(string name, bool value)
    {
        if (_boolNameMap.TryGetValue(name, out int index))
            _boolParams[index] = value;
        else
            Debug.LogWarning($"[NetworkAnimator] Bool parameter '{name}' not found in Profile!");
    }

    public void SetRenderBool(string name, bool value)
    {
        if (_boolVisualNameMap.TryGetValue(name, out int index))
            _boolVisualParams[index] = value;
        else
            Debug.LogWarning($"[NetworkAnimator] Bool parameter '{name}' not found in Profile!");
    }

    public void SetTrigger(string name)
    {
        if (_triggerNameMap.TryGetValue(name, out int index))
        {
            TriggerTicks.Set(index, Runner.Tick);
        }
        else
        {
            Debug.LogWarning($"[NetworkAnimator] Trigger '{name}' not found in Profile!");
        }
    }

    public float GetSimFloat(string name) => _floatNameMap.TryGetValue(name, out int idx) ? _floatParams[idx] : 0f;
    public float GetRenderFloat(string name) => _floatVisualNameMap.TryGetValue(name, out int idx) ? _floatVisualParams[idx] : 0f;
    public bool GetSimBool(string name) => _boolNameMap.TryGetValue(name, out int idx) ? _boolParams[idx] : false;
    public bool GetRenderBool(string name) => _boolVisualNameMap.TryGetValue(name, out int idx) ? _boolVisualParams[idx] : false;
    public bool GetSimTrigger(string name)
    {
        if (_triggerNameMap.TryGetValue(name, out int index))
        {
            return TriggerTicks.Get(index) == Runner.Tick;
        }
        return false;
    }

    private float GetWeightForTick(int targetTick)
    {
        int totalTicks = AnimState.TransitionEndTick - AnimState.TransitionStartTick;
        if (totalTicks <= 0) return 1f;

        float elapsedTicks = (float)(targetTick - AnimState.TransitionStartTick);
        return Mathf.Clamp01(elapsedTicks / totalTicks);
    }
    #endregion

    public override void Spawned()
    {
        _speedProvider = GetComponent<IAnimVarSpeed>();
        InitializeParameters();
        InitializeGraph();
    }
    public void UpdatePhysicsAnimator(out Vector3 rootMotionDeltaPos, out Quaternion rootMotionDeltaRot
        , out Vector3 absoluteRootOffset, out Quaternion absoluteRootRot)
    {
        rootMotionDeltaPos = Vector3.zero;
        rootMotionDeltaRot = Quaternion.identity;

        absoluteRootOffset = Vector3.zero;
        absoluteRootRot = Quaternion.identity;

        if (!_graph.IsValid()) return;

        byte nextID = CheckTransitions(AnimState.CurrentStateID);
        if (nextID != AnimState.CurrentStateID)
        {
            var newState = AnimState;
            newState.PreviousStateID = AnimState.CurrentStateID;
            newState.CurrentStateID = nextID;
            newState.TransitionStartTick = Runner.Tick;

            float durationSeconds = 0.2f;
            int durationTicks = Mathf.Max(1, (int)(durationSeconds / Runner.DeltaTime));
            newState.TransitionEndTick = Runner.Tick + durationTicks;

            AnimState = newState;
        }

        var currentStateLogic = Profile.AllStates.Find(s => s.StateID == AnimState.CurrentStateID);
        bool extractRM = currentStateLogic != null && currentStateLogic.ExtractRootMotion;


        if (extractRM && RootMotionBone != null)
        {
            int prevTick = Runner.Tick - 1;
            float prevWeight = GetWeightForTick(prevTick);
            float prevTime = prevTick * Runner.DeltaTime;

            ApplyPose(prevWeight, prevTime, true);
            Vector3 localPosT0 = RootMotionBone.localPosition;
            Quaternion localRotT0 = RootMotionBone.localRotation;

            float currWeight = GetWeightForTick(Runner.Tick);
            float currTime = Runner.Tick * Runner.DeltaTime;

            ApplyPose(currWeight, currTime, true);
            Vector3 localPosT1 = RootMotionBone.localPosition;
            Quaternion localRotT1 = RootMotionBone.localRotation;

            Vector3 localDelta = localPosT1 - localPosT0;
            localDelta.Scale(RootMotionScale); 

            rootMotionDeltaPos = transform.TransformDirection(localDelta);
            rootMotionDeltaRot = localRotT1 * Quaternion.Inverse(localRotT0);

            Vector3 localOffsetAbsolute = localPosT1 - RootBoneBindPose;
            localOffsetAbsolute.Scale(RootMotionScale);

            absoluteRootOffset = UnityAnimator.transform.TransformDirection(localOffsetAbsolute);
            absoluteRootRot = localRotT1 * Quaternion.Inverse(RootBoneBindRot);

            Quaternion correction = GetAlignmentCorrection();

            Vector3 rawLocalDelta = localPosT1 - localPosT0;
            Vector3 standardLocalDelta = correction * rawLocalDelta; // Twist it to standard space!
            standardLocalDelta.Scale(RootMotionScale);
            rootMotionDeltaPos = transform.TransformDirection(standardLocalDelta);

            // --- 2. DELTA ROTATION (Turning) ---
            Quaternion rawDeltaRot = localRotT1 * Quaternion.Inverse(localRotT0);
            // To apply a twist to a rotation, you multiply it on both sides (Similarity Transform)
            rootMotionDeltaRot = correction * rawDeltaRot * Quaternion.Inverse(correction);

            // --- 3. ABSOLUTE OFFSET (Y Ride Height) ---
            Vector3 rawAbsoluteOffset = localPosT1 - RootBoneBindPose;
            Vector3 standardAbsoluteOffset = correction * rawAbsoluteOffset; // Twist it!
            standardAbsoluteOffset.Scale(RootMotionScale);
            absoluteRootOffset = UnityAnimator.transform.TransformDirection(standardAbsoluteOffset);

            // --- 4. ABSOLUTE ROTATION ---
            Quaternion rawAbsoluteRot = localRotT1 * Quaternion.Inverse(RootBoneBindRot);
            absoluteRootRot = correction * rawAbsoluteRot * Quaternion.Inverse(correction);

            // Reset bone
            RootMotionBone.localPosition = RootBoneBindPose;
            RootMotionBone.localRotation = RootBoneBindRot;
        }
        else
        {
            float weight = GetWeightForTick(Runner.Tick);
            float time = Runner.Tick * Runner.DeltaTime;
            ApplyPose(weight, time, true);
        }

       
    }


    public void UpdateVisualAnimator(out Vector3 absoluteVisualRootOffset, out Quaternion absoluteVisualRootRot, bool overRideWithSimValues = false)
    {
        absoluteVisualRootOffset = Vector3.zero;
        absoluteVisualRootRot = Quaternion.identity;

        if (!_graph.IsValid()) return;

        // 1. Get Fusion's perfectly smooth continuous clock
        float currentTime = (float)Runner.LocalRenderTime;

        float startTickTime = AnimState.TransitionStartTick * Runner.DeltaTime;
        float endTickTime = AnimState.TransitionEndTick * Runner.DeltaTime;

        float weight = 1f;
        if (endTickTime > startTickTime)
        {
            weight = Mathf.Clamp01((currentTime - startTickTime) / (endTickTime - startTickTime));
        }


        ApplyPose(weight, currentTime, overRideWithSimValues);

        var currentStateLogic = Profile.AllStates.Find(s => s.StateID == AnimState.CurrentStateID);
        if (currentStateLogic != null && currentStateLogic.ExtractRootMotion && RootMotionBone != null)
        {
            Vector3 localOffset = RootMotionBone.localPosition - RootBoneBindPose;
            localOffset.Scale(RootMotionScale);

            absoluteVisualRootOffset = UnityAnimator.transform.TransformDirection(localOffset);
            absoluteVisualRootRot = RootMotionBone.localRotation * Quaternion.Inverse(RootBoneBindRot);

            Quaternion correction = GetAlignmentCorrection();

            Vector3 rawLocalOffset = RootMotionBone.localPosition - RootBoneBindPose;
            Vector3 standardLocalOffset = correction * rawLocalOffset; // Twist it!
            standardLocalOffset.Scale(RootMotionScale);

            absoluteVisualRootOffset = UnityAnimator.transform.TransformDirection(standardLocalOffset);

            Quaternion rawAbsoluteRot = RootMotionBone.localRotation * Quaternion.Inverse(RootBoneBindRot);
            absoluteVisualRootRot = correction * rawAbsoluteRot * Quaternion.Inverse(correction);

            RootMotionBone.localPosition = RootBoneBindPose;
            RootMotionBone.localRotation = RootBoneBindRot;
        }

    }


    private void ApplyPose(float transitionWeight, float absoluteTime, bool isSim)
    {
        for (int i = 0; i < _masterMixer.GetInputCount(); i++)
        {
            float targetWeight = 0;
            if (i == AnimState.CurrentStateID) targetWeight = transitionWeight;
            else if (i == AnimState.PreviousStateID) targetWeight = 1f - transitionWeight;

            _masterMixer.SetInputWeight(i, targetWeight);

            if (targetWeight > 0.001f)
            {
                var stateLogic = Profile.AllStates.Find(s => s.StateID == (byte)i);
                if (stateLogic != null)
                {
                    var mixer = _stateMixers[i];
                    stateLogic.ProcessState(ref mixer, absoluteTime, gameObject, isSim);
                }
            }
        }


        _graph.Evaluate(0f);

        foreach (var constraint in AimConstraints) constraint.Solve();
    }
    private void InitializeGraph()
    {
        if (Profile == null || UnityAnimator == null) return;

        for (int i = 0; i < Profile.AllStates.Count; i++)
        {
            Profile.AllStates[i].StateID = (byte)i;
        }

        int maxStateID = Profile.AllStates.Count - 1;

        _graph = PlayableGraph.Create("NetworkAnimatorGraph");
        _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var output = AnimationPlayableOutput.Create(_graph, "AnimOutput", UnityAnimator);

        _masterMixer = AnimationLayerMixerPlayable.Create(_graph, maxStateID + 1);
        output.SetSourcePlayable(_masterMixer);

        _stateMixers = new AnimationMixerPlayable[maxStateID + 1];

        foreach (var stateDef in Profile.AllStates)
        {
            var stateMixer = AnimationMixerPlayable.Create(_graph, stateDef.GetClipCount());

            _graph.Connect(stateMixer, 0, _masterMixer, stateDef.StateID);
            _masterMixer.SetInputWeight(stateDef.StateID, 0f);

            stateDef.InitializeState(_graph, stateMixer);

            _stateMixers[stateDef.StateID] = stateMixer;
        }

        _graph.Play();
    }


    private byte CheckTransitions(byte currentStateID)
    {
        foreach (var transition in Profile.AnyStateTransitions)
        {
            if (EvaluateTransition(transition)) return transition.TargetStateID;
        }

        var currentStateLogic = Profile.AllStates.Find(s => s.StateID == currentStateID);
        if (currentStateLogic != null)
        {
            foreach (var transition in currentStateLogic.OutboundTransitions)
            {
                if (EvaluateTransition(transition)) return transition.TargetStateID;
            }
        }

        return currentStateID; 
    }

    private bool EvaluateTransition(AnimTransition transition)
    {
        if (transition.Conditions == null || transition.Conditions.Count == 0) return false;

        foreach (var condition in transition.Conditions)
        {
            if (!condition.Evaluate(gameObject)) return false;
        }
        return true;
    }

    public enum BoneAxis
    {
        X, Y, Z,
        NegativeX, NegativeY, NegativeZ
    }

    private Quaternion GetAlignmentCorrection()
    {
        Vector3 localForward = GetAxisVector(ForwardAxis);
        Vector3 localUp = GetAxisVector(UpAxis);

        Quaternion boneOrientation = Quaternion.LookRotation(localForward, localUp);

        return Quaternion.Inverse(boneOrientation);
    }

    private Vector3 GetAxisVector(BoneAxis axis)
    {
        switch (axis)
        {
            case BoneAxis.X: return Vector3.right;
            case BoneAxis.Y: return Vector3.up;
            case BoneAxis.Z: return Vector3.forward;
            case BoneAxis.NegativeX: return Vector3.left;
            case BoneAxis.NegativeY: return Vector3.down;
            case BoneAxis.NegativeZ: return Vector3.back;
            default: return Vector3.forward;
        }
    }

   

    public void OnDestroy()
    {
        if (_graph.IsValid())
        {
            _graph.Destroy();
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Bake Unposed Root Height")]
    private void BakeBindPose()
    {
        if (RootMotionBone == null)
        {
            Debug.LogWarning("[NetworkAnimator] Cannot bake: RootMotionBone is not assigned!");
            return;
        }

        // Warning to prevent baking while the game is running and the character is moving
        if (Application.isPlaying)
        {
            Debug.LogWarning("[NetworkAnimator] Please bake the bind pose in Edit Mode, not while the game is playing!");
            return;
        }

        // 1. Just read the bone's exact local position in the editor!
        // Because the game isn't running, the animator hasn't touched the bones,
        // so this is guaranteed to be the pure, unposed bind pose.
        RootBoneBindPose = RootMotionBone.localPosition;
        RootBoneBindRot = RootMotionBone.localRotation;
        // 2. Mark it dirty so Unity saves the change to the Prefab
        UnityEditor.EditorUtility.SetDirty(this);
        
        Debug.Log($"[NetworkAnimator] Baked Unposed Root Height successfully: {RootBoneBindPose}");
    }
#endif
}