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

    public Vector3 RootMotionHorizontalDelta { get; private set; }
    public float RootMotionVerticalDelta { get; private set; }
    public Quaternion RootMotionRotation { get; private set; }

    private Animator _animator;
    private Transform _armatureRoot;
    private Vector3 localRootPosInTPose;
    private Quaternion _tPoseLocalRot;

    private Vector3 _rootMotionOrigin;   
    private Vector3 _prevRootMotionPos;

    private bool _zIsUp;



    public DeterministicNetworkAnimator(Animator animator, List<AnimationClip> clips, AnimationClip tPoseClip, Transform armatureRoot, bool zIsUp)
    {
        _zIsUp = zIsUp;
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

            _rootMotionOrigin = Vector3.zero;

            localRootPosInTPose = Vector3.zero;
            _tPoseLocalRot = _armatureRoot.localRotation;

            tPoseGraph.Destroy();

            if (zIsUp)
            {
                _prevRootMotionPos = new Vector3(_rootMotionOrigin.x, 0, _rootMotionOrigin.y);
            }
            else
            {
                _prevRootMotionPos = new Vector3(_rootMotionOrigin.x, 0, _rootMotionOrigin.z);
            }
               
        }
        else
        {
            Debug.LogError("T-Pose Clip is not set! Root motion will be unpredictable.");
            // Set defaults to avoid nulls
            _rootMotionOrigin = Vector3.zero;
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
        
        Vector3 currentHorizontalPos;
        if (_zIsUp)
        {
            RootMotionVerticalDelta = currentRootPos.z - localRootPosInTPose.z;
            currentHorizontalPos = new Vector3(currentRootPos.x, 0, currentRootPos.y);
        }
        else
        {
            RootMotionVerticalDelta = currentRootPos.y - localRootPosInTPose.y;
            currentHorizontalPos = new Vector3(currentRootPos.x, 0, currentRootPos.z);
        }

       
        Vector3 deltaPos = currentHorizontalPos - _prevRootMotionPos;

        // C. Cache horizontal position for next tick
        _prevRootMotionPos = currentHorizontalPos;

        // D. Guard against teleports
        if (deltaPos.magnitude > 1.0f)
        {
            deltaPos = Vector3.zero;
        }

        RootMotionHorizontalDelta = deltaPos;

        //rotation

        Quaternion currentLocalRot = _armatureRoot.localRotation;
        Quaternion deltaRotation = currentLocalRot * Quaternion.Inverse(_tPoseLocalRot);
        RootMotionRotation = deltaRotation;
    }

    public void DestroyGraph()
    {
        if (_graph.IsValid())
        {
            _graph.Destroy();
        }
    }
}
