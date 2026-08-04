using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Serialization;

[Serializable]
public abstract class AnimStateBase
{
    public string StateName; // For easier debugging
    [HideInInspector]
    public byte StateID;
    public bool ExtractRootMotion;
    [FormerlySerializedAs("ScalePlaybackWithController")]
    public bool UseLocomotionCycle;

    [Min(0f)]
    public float LocomotionCyclesPerSecond = 1f;

    [SerializeField]
    public List<AnimTransition> OutboundTransitions = new List<AnimTransition>();

    // The state now knows exactly how many clips it needs
    public abstract int GetClipCount();

    /// <summary>
    /// Each state handles its own internal connections.
    /// </summary>
    public abstract void InitializeState(PlayableGraph graph, AnimationMixerPlayable stateMixer);

    /// <summary>
    /// Each state handles its own internal blending logic.
    /// </summary>
    public abstract void ProcessState(ref AnimationMixerPlayable stateMixer, float stateLocalTime, float locomotionCycle, GameObject hull, bool isSim);

    protected float GetMotionTime(AnimationClip clip, float speedMultiplier, float stateLocalTime, float locomotionCycle)
    {
        if (UseLocomotionCycle) return locomotionCycle * clip.length;
        return stateLocalTime * speedMultiplier;
    }

    public virtual float GetLocomotionCycleRateMultiplier(GameObject hull)
    {
        return UseLocomotionCycle ? 1f : 0f;
    }
}


[Serializable]
public class BlendTreeMotion1D
{
    public AnimationClip Clip;
    public float Threshold;
    public float TimeScale = 1f;
}

[Serializable]
public class BlendTreeMotion2D
{
    public AnimationClip Clip;
    public Vector2 Position; // The X/Y threshold (e.g., X: 0, Y: 1 for Forward Walk)
    [FormerlySerializedAs("TimeScale")]
    [Min(0f)]
    public float SpeedMultiplier = 1f;
}

[Serializable]
public class BlendTree1DState : AnimStateBase
{
    [SerializeField, AnimParameter(AnimParamType.Float)]
    private string _parameterName;

    // The list of clips to blend
    public List<BlendTreeMotion1D> Motions = new List<BlendTreeMotion1D>();

    public override int GetClipCount() => Motions.Count;

    public override void InitializeState(PlayableGraph graph, AnimationMixerPlayable stateMixer)
    {
        // Sort motions by threshold immediately
        Motions.Sort((a, b) => a.Threshold.CompareTo(b.Threshold));

        for (int i = 0; i < Motions.Count; i++)
        {
            var motion = Motions[i];

            // Just a clean, standard direct connection
            var clipPlayable = AnimationClipPlayable.Create(graph, motion.Clip);
            clipPlayable.Pause();
            graph.Connect(clipPlayable, 0, stateMixer, i);
        }
    }

    public override void ProcessState(ref AnimationMixerPlayable stateMixer, float stateLocalTime, float locomotionCycle, GameObject hull, bool isSim)
    {
        var anim = hull.GetComponent<NetworkAnimator>();
        if (anim == null || Motions.Count == 0) return;

        float val = isSim ? anim.GetSimFloat(_parameterName) : anim.GetRenderFloat(_parameterName);
        int count = Motions.Count;

        // 1. Advance Time for all active inputs, applying TimeScale
        for (int i = 0; i < count; i++)
        {
            var motion = Motions[i];
            float animationTime = GetMotionTime(motion.Clip, motion.TimeScale, stateLocalTime, locomotionCycle);

            stateMixer.GetInput(i).SetTime(animationTime);
            stateMixer.SetInputWeight(i, 0f);
        }

        // 2. The 1D Blend Math
        if (count == 1 || val <= Motions[0].Threshold)
        {
            stateMixer.SetInputWeight(0, 1f);
            return;
        }

        if (val >= Motions[count - 1].Threshold)
        {
            stateMixer.SetInputWeight(count - 1, 1f);
            return;
        }

        // Find which two clips we are between
        for (int i = 0; i < count - 1; i++)
        {
            if (val >= Motions[i].Threshold && val <= Motions[i + 1].Threshold)
            {
                float range = Motions[i + 1].Threshold - Motions[i].Threshold;
                float weight = (val - Motions[i].Threshold) / range;

                stateMixer.SetInputWeight(i, 1f - weight);
                stateMixer.SetInputWeight(i + 1, weight);
                break;
            }
        }
    }
}

[Serializable]
public class BlendState2D : AnimStateBase
{
    [SerializeField, AnimParameter(AnimParamType.Float)]
    private string _parameterX;

    [SerializeField, AnimParameter(AnimParamType.Float)]
    private string _parameterY;

    public List<BlendTreeMotion2D> Motions = new List<BlendTreeMotion2D>();

    private float[] _weightsCache;

    public override int GetClipCount() => Motions.Count;

    public override void InitializeState(PlayableGraph graph, AnimationMixerPlayable stateMixer)
    {
        _weightsCache = new float[Motions.Count];

        for (int i = 0; i < Motions.Count; i++)
        {
            var clipPlayable = AnimationClipPlayable.Create(graph, Motions[i].Clip);
            clipPlayable.Pause();
            graph.Connect(clipPlayable, 0, stateMixer, i);
        }
    }

    private Vector2 GetInput(NetworkAnimator anim, bool isSim)
    {
        float inputX = isSim ? anim.GetSimFloat(_parameterX) : anim.GetRenderFloat(_parameterX);
        float inputY = isSim ? anim.GetSimFloat(_parameterY) : anim.GetRenderFloat(_parameterY);
        return new Vector2(inputX, inputY);
    }

    private void EnsureWeightsCache()
    {
        if (_weightsCache == null || _weightsCache.Length != Motions.Count)
        {
            _weightsCache = new float[Motions.Count];
        }
    }

    private void CalculateWeights(Vector2 input)
    {
        EnsureWeightsCache();
        Array.Clear(_weightsCache, 0, _weightsCache.Length);

        int count = Motions.Count;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            if ((input - Motions[i].Position).sqrMagnitude < 0.0001f)
            {
                _weightsCache[i] = 1f;
                return;
            }
        }

        float inputMagnitude = input.magnitude;
        Vector2 inputDirection = inputMagnitude > 0.001f ? input / inputMagnitude : Vector2.up;
        float totalWeight = 0f;
        float baseAnglePenalty = 2f;

        for (int i = 0; i < count; i++)
        {
            Vector2 position = Motions[i].Position;
            float positionMagnitude = position.magnitude;

            if (positionMagnitude < 0.001f)
            {
                float idleDistanceSquared = inputMagnitude * inputMagnitude;
                _weightsCache[i] = 1f / (idleDistanceSquared * idleDistanceSquared + 0.0001f);
                totalWeight += _weightsCache[i];
                continue;
            }

            Vector2 positionDirection = position / positionMagnitude;
            float dot = Vector2.Dot(inputDirection, positionDirection);
            float angleMetric = 1f - dot;
            float dynamicAnglePenalty = baseAnglePenalty * inputMagnitude;
            float magnitudeDifference = Mathf.Abs(inputMagnitude - positionMagnitude);
            float warpedAngle = angleMetric * dynamicAnglePenalty;
            float polarDistanceSquared = warpedAngle * warpedAngle + magnitudeDifference * magnitudeDifference;

            _weightsCache[i] = 1f / (polarDistanceSquared * polarDistanceSquared + 0.0001f);
            totalWeight += _weightsCache[i];
        }

        if (totalWeight <= 0.0001f)
        {
            _weightsCache[0] = 1f;
            return;
        }

        for (int i = 0; i < count; i++)
        {
            _weightsCache[i] /= totalWeight;
        }
    }

    private float GetWeightedSpeedMultiplier()
    {
        float weightedSpeed = 0f;

        for (int i = 0; i < Motions.Count; i++)
        {
            weightedSpeed += _weightsCache[i] * Mathf.Max(0f, Motions[i].SpeedMultiplier);
        }

        return weightedSpeed;
    }

    public override float GetLocomotionCycleRateMultiplier(GameObject hull)
    {
        if (!UseLocomotionCycle) return 0f;

        var anim = hull.GetComponent<NetworkAnimator>();
        if (anim == null || Motions.Count == 0) return 0f;

        CalculateWeights(GetInput(anim, true));
        return GetWeightedSpeedMultiplier();
    }

    public override void ProcessState(ref AnimationMixerPlayable stateMixer, float stateLocalTime, float locomotionCycle, GameObject hull, bool isSim)
    {
        var anim = hull.GetComponent<NetworkAnimator>();
        if (anim == null || Motions.Count == 0) return;

        CalculateWeights(GetInput(anim, isSim));

        for (int i = 0; i < Motions.Count; i++)
        {
            var motion = Motions[i];
            float animationTime = GetMotionTime(motion.Clip, motion.SpeedMultiplier, stateLocalTime, locomotionCycle);

            stateMixer.GetInput(i).SetTime(animationTime);
            stateMixer.SetInputWeight(i, _weightsCache[i]);
        }
    }
}

