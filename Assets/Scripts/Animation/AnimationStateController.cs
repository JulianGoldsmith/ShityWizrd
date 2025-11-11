using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;

public class AnimationStateController : NetworkBehaviour
{
    [HideInInspector] public DeterministicNetworkAnimator _detAnimator;

    [Header("Animator")]
    [SerializeField] private Animator _animator;

    [Header("Animation Clips")]
    [Tooltip("The list of all animations. ORDER MATTERS and must match indices.")]
    [SerializeField] private List<AnimClipConfig> _locomotionClipsConfig;
    [SerializeField] private AnimationClip _tPoseClip;

  

    [Networked]
    public AnimState CurrentState { get; set; }

    [SerializeField] private const int IDLE_INDEX = 0;
    [SerializeField] private const int FORWARD_INDEX = 1;
    [SerializeField] private const int BACKWARD_INDEX = 2;
    [SerializeField] private const int LEFT_INDEX = 3;
    [SerializeField] private const int RIGHT_INDEX = 4;
    [SerializeField] private const int LOCOMOTION_CAPACITY = 5; // We now have 5 clips

    [SerializeField] private NPCActionController _npcActionController;
    [SerializeField] private const int ACTION_CAPACITY = 15;
    [SerializeField] private const int TOTAL_CAPACITY = LOCOMOTION_CAPACITY + ACTION_CAPACITY;

    public enum AnimState
    {
        Locomotion,
        Action,
    }

    [Header("Blending Speeds")]
    [SerializeField] private float _blendSpeed = 5.0f;
    [SerializeField] private float _actionBlendSpeed = 20.0f;
    [SerializeField] private float _locomotionBlendSpeed = 10.0f;

    private float _locomotionSpeedMultiplier = 1.0f;

    private List<AnimClipConfig> _runtimeClipsConfig = new List<AnimClipConfig>();

    [Header("Networked variables")]

    [Networked, Capacity(TOTAL_CAPACITY)]
    public NetworkArray<float> ClipWeights { get; }

    [Networked, Capacity(TOTAL_CAPACITY)]
    public NetworkArray<float> ClipTimes { get; }

    [Networked]
    public int ActiveClipIndex { get; set; } = -1;



    private Vector2 _targetMoveVector;

    public Transform armatureRoot;

    [Header("Needed for blender imports Z = UP")]
    public AnimLocalAxis armatureLocalUpAxis = AnimLocalAxis.Y;
    public AnimLocalAxis armatureLocalForwardAxis = AnimLocalAxis.Z;
    public Vector3 RootMotionRaw => _detAnimator.RootMotionRaw;
    public Vector3 RootMotionDelta => _detAnimator.RootMotionDelta;
    public Quaternion RootMotionRotation => _detAnimator?.RootMotionRotation ?? Quaternion.identity;

    public Quaternion WorldDeltaFromTPose => _detAnimator?.WorldDeltaFromTPose ?? Quaternion.identity;

    private void Awake()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
    }

    public override void Spawned()
    {
        if (_npcActionController == null)
            _npcActionController = GetComponent<NPCActionController>();

        BuildRuntimeClips();

        List<AnimationClip> clips = _runtimeClipsConfig.Select(config => config.Clip).ToList();
        _detAnimator = new DeterministicNetworkAnimator(_animator, clips, _tPoseClip, armatureRoot, armatureRoot.parent, armatureLocalForwardAxis, armatureLocalUpAxis);

        if (Object.HasStateAuthority)
        {
            ClipWeights.Set(IDLE_INDEX, 1.0f);
            CurrentState = AnimState.Locomotion;
        }
    }

    private void BuildRuntimeClips()
    {
        _runtimeClipsConfig.Clear();

        if (_locomotionClipsConfig.Count != LOCOMOTION_CAPACITY)
            Debug.LogError("Locomotion clips list count does not match LOCOMOTION_CAPACITY!");

        _runtimeClipsConfig.AddRange(_locomotionClipsConfig);

        int currentClipIndex = LOCOMOTION_CAPACITY;
        if (_npcActionController != null)
        {
            foreach (var action in _npcActionController.actions)
            {
                if (currentClipIndex + 2 >= TOTAL_CAPACITY)
                {
                    Debug.LogWarning($"Max action clip capacity ({ACTION_CAPACITY}) reached. Not adding action: {action.name}");
                    break;
                }

                _npcActionController.RegisterActionBaseIndex(action, currentClipIndex);

                _runtimeClipsConfig.Add(new AnimClipConfig { Clip = action.windUpClip, Speed = 1.0f });
                _runtimeClipsConfig.Add(new AnimClipConfig { Clip = action.holdClip, Speed = 1.0f });
                _runtimeClipsConfig.Add(new AnimClipConfig { Clip = action.releaseClip, Speed = 1.0f });

                currentClipIndex += 3;
            }
        }

        int remaining = TOTAL_CAPACITY - _runtimeClipsConfig.Count;
        for (int i = 0; i < remaining; i++)
        {
            _runtimeClipsConfig.Add(new AnimClipConfig { Clip = _tPoseClip, Speed = 1.0f });
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _detAnimator?.DestroyGraph();
    }

    public void SetTargetMovement(Vector2 moveVector)
    {
        _targetMoveVector = moveVector;
    }

    public void SimulateAnimation()
    {
        float[] targetWeights = new float[TOTAL_CAPACITY];

        if (Object.HasStateAuthority || Object.HasInputAuthority)
        {
            switch (CurrentState)
            {
                case AnimState.Locomotion:
                    RunLocomotionLogic(targetWeights);
                    break;
                case AnimState.Action:
                    RunActionLogic(targetWeights);
                    break;
            }
        }
        else
        {
            for (int i = 0; i < TOTAL_CAPACITY; i++)
            {
                targetWeights[i] = ClipWeights[i];
            }
        }

        ApplyBlendingAndAdvanceTime(targetWeights);

        _detAnimator?.ApplyStateToGraph(ClipWeights, ClipTimes);
    }

    private void RunLocomotionLogic(float[] targetWeights)
    {
        float moveMagnitude = _targetMoveVector.magnitude;

        _locomotionSpeedMultiplier = Mathf.Max(1.0f, moveMagnitude);

        float blendMagnitude = Mathf.Clamp01(moveMagnitude);

        targetWeights[IDLE_INDEX] = 1.0f - blendMagnitude;

        if (blendMagnitude > 0.001f)
        {
            float normX = _targetMoveVector.x / moveMagnitude;
            float normY = _targetMoveVector.y / moveMagnitude;

            targetWeights[FORWARD_INDEX] = Mathf.Max(0, normY) * blendMagnitude;
            targetWeights[BACKWARD_INDEX] = Mathf.Max(0, -normY) * blendMagnitude;
            targetWeights[LEFT_INDEX] = Mathf.Max(0, -normX) * blendMagnitude;
            targetWeights[RIGHT_INDEX] = Mathf.Max(0, normX) * blendMagnitude;
        }
        else
        {
            targetWeights[FORWARD_INDEX] = 0;
            targetWeights[BACKWARD_INDEX] = 0;
            targetWeights[LEFT_INDEX] = 0;
            targetWeights[RIGHT_INDEX] = 0;
        }
    }

    private void RunActionLogic(float[] targetWeights)
    {
        for (int i = 0; i < TOTAL_CAPACITY; i++)
            targetWeights[i] = 0;

        if (ActiveClipIndex >= 0 && ActiveClipIndex < TOTAL_CAPACITY)
        {
            targetWeights[ActiveClipIndex] = 1.0f;

            var config = _runtimeClipsConfig[ActiveClipIndex];
            if (!config.Clip.isLooping)
            {
                float currentTime = ClipTimes[ActiveClipIndex];
                if (currentTime >= config.Clip.length)
                {
                    CurrentState = AnimState.Locomotion;
                    ActiveClipIndex = -1;
                    RunLocomotionLogic(targetWeights); 
                }
            }
        }
        else
        {
            // No valid clip, go back to Locomotion
            CurrentState = AnimState.Locomotion;
        }
    }
    private void ApplyBlendingAndAdvanceTime(float[] targetWeights)
    {
        float currentBlendSpeed = (CurrentState == AnimState.Locomotion) ? _locomotionBlendSpeed : _actionBlendSpeed;

        float blendDelta = Runner.DeltaTime * currentBlendSpeed;

        for (int i = 0; i < _runtimeClipsConfig.Count; i++)
        {
            AnimClipConfig config = _runtimeClipsConfig[i];
            float newWeight = ClipWeights[i];

            if (Object.HasStateAuthority || Object.HasInputAuthority)
            {
                float currentWeight = ClipWeights[i];
                float targetWeight = targetWeights[i];
                newWeight = Mathf.MoveTowards(currentWeight, targetWeight, blendDelta);
                ClipWeights.Set(i, newWeight);
            }

            if (newWeight > 0.001f) 
            {
                float effectiveSpeed = config.Speed;

                if (CurrentState == AnimState.Locomotion)
                {
                    if (i == FORWARD_INDEX || i == BACKWARD_INDEX || i == LEFT_INDEX || i == RIGHT_INDEX)
                    {
                        effectiveSpeed *= _locomotionSpeedMultiplier;
                    }
                }

                float timeDelta = Runner.DeltaTime * effectiveSpeed;
                float newTime = ClipTimes[i] + timeDelta;

                if (config.Clip.isLooping)
                {
                    newTime %= config.Clip.length;
                }
                else
                {
                    newTime = Mathf.Clamp(newTime, 0, config.Clip.length);
                }

                ClipTimes.Set(i, newTime);
            }
        }
    }

    public void PlayClip(int clipIndex)
    {
        if (!Object.HasStateAuthority) return;

        for (int i = 0; i < TOTAL_CAPACITY; i++)
        {
            if (i != clipIndex)
                ClipWeights.Set(i, 0);
        }
        ClipWeights.Set(clipIndex, 1.0f);
        CurrentState = AnimState.Action;
        ActiveClipIndex = clipIndex;
        ClipTimes.Set(clipIndex, 0); // Reset time
    }

    public void GoToLocomotion()
    {
        if (!Object.HasStateAuthority) return;

        CurrentState = AnimState.Locomotion;
        ActiveClipIndex = -1;
    }

    public float GetClipLength(int index)
    {
        if (index < 0 || index >= _runtimeClipsConfig.Count) return 0;
        return _runtimeClipsConfig[index].Clip.length;
    }

    public override void Render()
    {
        _detAnimator?.ApplyStateToGraph(ClipWeights, ClipTimes);
    }
}