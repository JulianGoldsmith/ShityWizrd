using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

// The interface the NetworkAnimator will call
public interface IStateBlendProcessor
{
    // Takes the mixer for this specific state, the delta time, and the character hull to pull data from
    void ProcessNode(ref AnimationMixerPlayable stateMixer, float absoluteTime, GameObject hull);
}

// ==========================================
// A Concrete Example: 1D Speed Blend (Idle to Run)
// ==========================================
public struct SpeedBlendProcessor : IStateBlendProcessor
{
    public void ProcessNode(ref AnimationMixerPlayable stateMixer, float absoluteTime, GameObject hull)
    {
        // 1. Get the speed from the blackboard
        var speedProvider = hull.GetComponent<IAnimVarSpeed>();
        float speed = speedProvider != null ? speedProvider.GetCurrentSpeed() : 0f;

        // 2. Normalize speed (assuming 5f is max speed for this example)
        float normalizedSpeed = Mathf.Clamp01(speed / 5f);

        // 3. Apply weights to the ports (Port 0 = Idle, Port 1 = Run)
        stateMixer.SetInputWeight(0, 1f - normalizedSpeed);
        stateMixer.SetInputWeight(1, normalizedSpeed);

        // 4. Explicitly advance the time of the clips connected to this mixer
        for (int i = 0; i < stateMixer.GetInputCount(); i++)
        {
            var clipPlayable = stateMixer.GetInput(i);
            if (clipPlayable.IsValid())
            {
                // In a true setup, we scale time by playback speed, but we'll keep it simple for the test
                clipPlayable.SetTime(absoluteTime);
            }
        }
    }
}