using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using Fusion;


[System.Serializable]
public class AnimClipConfig
{
    public AnimationClip Clip;
    public float Speed = 1.0f;
}

public class DeterministicNetworkAnimator
{
    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private List<AnimationClipPlayable> _clipPlayables = new List<AnimationClipPlayable>();
    private bool _isInitialized = false;

    public Vector3 RootMotionDelta { get; private set; } ///root motion difference from last fixedUpdate.
    public Vector3 RootMotionRaw { get; private set; } ///root motion raw local position in relation to armature base
    public Quaternion RootMotionRotation { get; private set; }



    private Animator _animator;
    private Transform _armatureRoot;
    private Transform _armatureTrans;
    private Vector3 localRootPosInTPose;
    private Quaternion _tPoseLocalRot;

    private Vector3 _tPoseLocalPos;   
    private Vector3 _prevRootMotionPos;

    private bool _zIsUp;
    private Quaternion _qTPoseWorld;



    public DeterministicNetworkAnimator(Animator animator, List<AnimationClip> clips, AnimationClip tPoseClip, Transform armatureRoot, Transform armatureTrans, AnimLocalAxis animLocalForwardAxis, AnimLocalAxis animLocalUpAxis)
    {
        _tPoseForwardAxis = animLocalForwardAxis;
        _tPoseUpAxis = animLocalUpAxis;
        _armatureTrans = armatureTrans;
        //_zIsUp = zIsUp;
         _animator = animator;
        _armatureRoot = armatureRoot;
        if (animator == null || clips == null || clips.Count == 0)
        {
            Debug.LogError("DeterministicNetworkAnimator: Initialization failed. Animator or clips are null/empty.");
            return;
        }

        _graph = PlayableGraph.Create("DeterministicAnimatorGraph");
        _mixer = AnimationMixerPlayable.Create(_graph, clips.Count);

        for (int i = 0; i < clips.Count; i++)
        {
            var clip = clips[i];
            var clipPlayable = AnimationClipPlayable.Create(_graph, clip);

            clipPlayable.SetApplyFootIK(false); 
            clipPlayable.Pause(); 

            _graph.Connect(clipPlayable, 0, _mixer, i);
            _clipPlayables.Add(clipPlayable);
        }

        var output = AnimationPlayableOutput.Create(_graph, "AnimatorOutput", animator);
        output.SetSourcePlayable(_mixer);
        _graph.Play();
        _isInitialized = true;

        if (tPoseClip != null)
        {
            var tPoseGraph = PlayableGraph.Create("T-PoseSampler");
            var tPoseOutput = AnimationPlayableOutput.Create(tPoseGraph, "TempOutput", _animator);
            var tPoseClipPlayable = AnimationClipPlayable.Create(tPoseGraph, tPoseClip);

            tPoseOutput.SetSourcePlayable(tPoseClipPlayable);
            tPoseClipPlayable.SetTime(0);
            tPoseGraph.Evaluate();

            _tPoseLocalPos = _armatureRoot.localPosition;
            localRootPosInTPose = _armatureRoot.localPosition;
            _tPoseLocalRot = _armatureRoot.localRotation;

            Quaternion parentWorld = _armatureTrans.rotation;

            // This is the pelvis/root *world* rotation when in T-pose:
            _qTPoseWorld = parentWorld * _tPoseLocalRot;

            tPoseGraph.Destroy();

            if (_tPoseUpAxis == AnimLocalAxis.Z)
            {
                _prevRootMotionPos = new Vector3(_tPoseLocalPos.x, 0, _tPoseLocalPos.z);
            }
            else
            {
                _prevRootMotionPos = new Vector3(_tPoseLocalPos.x, 0, _tPoseLocalPos.y);
            }
               
        }
        else
        {
            Debug.LogError("T-Pose Clip is not set! Root motion will be unpredictable.");
            // Set defaults to avoid nulls
            _tPoseLocalPos = Vector3.zero;
            _prevRootMotionPos = Vector3.zero;
            localRootPosInTPose = Vector3.zero;
            _tPoseLocalRot = Quaternion.identity;

        }

    }


    public void ApplyStateToGraph(NetworkArray<float> weights, NetworkArray<float> times)
    {
        if (!_isInitialized) return;

        for (int i = 0; i < _clipPlayables.Count; i++)
        {
            float weight = weights[i];
            float time = times[i];

            _mixer.SetInputWeight(i, weight);
            _clipPlayables[i].SetTime(time);
        }

        _graph.Evaluate(0);

        //position

        Vector3 currentRootPos = _armatureRoot.localPosition;
        
        if (_tPoseUpAxis == AnimLocalAxis.Z)
        {
            RootMotionRaw = new Vector3(_armatureRoot.localPosition.x, _armatureRoot.localPosition.z, _armatureRoot.localPosition.y) * _armatureTrans.localScale.x;
            Vector3 rootDelta = _armatureRoot.localPosition - _prevRootMotionPos;
            RootMotionDelta = new Vector3(rootDelta.x , rootDelta.z, rootDelta.y) * _armatureTrans.localScale.x;
        
        }
        else
        {
            RootMotionRaw = new Vector3(_armatureRoot.localPosition.x, _armatureRoot.localPosition.y, _armatureRoot.localPosition.z) * _armatureTrans.localScale.x;
            Vector3 rootDelta = _armatureRoot.localPosition - _prevRootMotionPos;
            RootMotionDelta = new Vector3(rootDelta.x, rootDelta.y, rootDelta.z) * _armatureTrans.localScale.x;
        }

        _prevRootMotionPos = _armatureRoot.localPosition;

        Quaternion currentLocalRot = _armatureRoot.localRotation;
        Quaternion deltaLocalFromTPose = currentLocalRot * Quaternion.Inverse(_tPoseLocalRot);
        RootMotionRotation = deltaLocalFromTPose; 

        WorldDeltaFromTPose = _qTPoseWorld * deltaLocalFromTPose * Quaternion.Inverse(_qTPoseWorld);
    }

    public void DestroyGraph()
    {
        if (_graph.IsValid())
        {
            _graph.Destroy();
        }
    }

    
    private static Vector3 Axis(AnimLocalAxis a) => a switch
    {
        AnimLocalAxis.X => Vector3.right,
        AnimLocalAxis.Y => Vector3.up,
        AnimLocalAxis.Z => Vector3.forward,
        AnimLocalAxis.NegX => -Vector3.right,
        AnimLocalAxis.NegY => -Vector3.up,
        AnimLocalAxis.NegZ => -Vector3.forward
    };

    [SerializeField] private AnimLocalAxis _tPoseForwardAxis = AnimLocalAxis.Z;
    [SerializeField] private AnimLocalAxis _tPoseUpAxis = AnimLocalAxis.Y;

    public Quaternion WorldDeltaFromTPose { get; private set; }
}
public enum AnimLocalAxis { X, Y, Z, NegX, NegY, NegZ }
