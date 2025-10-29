using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using Fusion;


public class DeterministicNetworkAnimator
{
    private PlayableGraph _graph;
    private AnimationMixerPlayable _mixer;
    private List<AnimationClipPlayable> _clipPlayables = new List<AnimationClipPlayable>();
    private bool _isInitialized = false;

 
    public DeterministicNetworkAnimator(Animator animator, List<AnimationClip> clips)
    {
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
    }

    public void DestroyGraph()
    {
        if (_graph.IsValid())
        {
            _graph.Destroy();
        }
    }
}
