using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

[Serializable]
public abstract class AnimStateBase
{
    public string StateName; // For easier debugging
    [HideInInspector]
    public byte StateID;
    public bool ExtractRootMotion;

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
    public abstract void ProcessState(ref AnimationMixerPlayable stateMixer, float absoluteTime, GameObject hull, bool isSim);
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
    public float TimeScale = 1f;
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

    public override void ProcessState(ref AnimationMixerPlayable stateMixer, float absoluteTime, GameObject hull, bool isSim)
    {
        var anim = hull.GetComponent<NetworkAnimator>();
        if (anim == null || Motions.Count == 0) return;

        float val = isSim ? anim.GetSimFloat(_parameterName) : anim.GetRenderFloat(_parameterName);
        int count = Motions.Count;

        // 1. Advance Time for all active inputs, applying TimeScale
        for (int i = 0; i < count; i++)
        {
            var inputPlayable = stateMixer.GetInput(i);

            // Clean direct time scaling
            inputPlayable.SetTime(absoluteTime * Motions[i].TimeScale);
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
    [Header("Blend Parameters")]
    [Tooltip("The parameter for the X axis (Horizontal/Strafe).")]
    [SerializeField, AnimParameter(AnimParamType.Float)]
    private string _parameterX;

    [Tooltip("The parameter for the Y axis (Vertical/Forward).")]
    [SerializeField, AnimParameter(AnimParamType.Float)]
    private string _parameterY;

    [Header("Motions")]
    public List<BlendTreeMotion2D> Motions = new List<BlendTreeMotion2D>();

    // We cache weights array to avoid GC allocations during FUN
    private float[] _weightsCache;

    public override int GetClipCount() => Motions.Count;

    public override void InitializeState(PlayableGraph graph, AnimationMixerPlayable stateMixer)
    {
        _weightsCache = new float[Motions.Count];

        for (int i = 0; i < Motions.Count; i++)
        {
            var motion = Motions[i];
            var clipPlayable = AnimationClipPlayable.Create(graph, motion.Clip);
            clipPlayable.Pause();
            graph.Connect(clipPlayable, 0, stateMixer, i);
        }
    }

    public override void ProcessState(ref AnimationMixerPlayable stateMixer, float absoluteTime, GameObject hull, bool isSim)
    {
        var anim = hull.GetComponent<NetworkAnimator>();
        if (anim == null || Motions.Count == 0) return;

        float inputX = isSim ? anim.GetSimFloat(_parameterX) : anim.GetRenderFloat(_parameterX);
        float inputY = isSim ? anim.GetSimFloat(_parameterY) : anim.GetRenderFloat(_parameterY);
        Vector2 input = new Vector2(inputX, inputY);
        int count = Motions.Count;

        // 1. Advance time for all clips
        for (int i = 0; i < count; i++)
        {
            stateMixer.GetInput(i).SetTime(absoluteTime * Motions[i].TimeScale);
            stateMixer.SetInputWeight(i, 0f);
        }

        float inputMag = input.magnitude;
        Vector2 inputDir = inputMag > 0.001f ? input / inputMag : Vector2.up;

        float totalWeight = 0f;
        float[] weights = new float[count];

        // The base penalty for being off-angle
        float baseAnglePenalty = 2.0f;

        for (int i = 0; i < count; i++)
        {
            Vector2 pos = Motions[i].Position;
            float posMag = pos.magnitude;

            // --- 1. EXACT MATCH SHORT-CIRCUIT ---
            float exactDistSq = (input - pos).sqrMagnitude;
            if (exactDistSq < 0.0001f)
            {
                for (int j = 0; j < count; j++) stateMixer.SetInputWeight(j, 0f);
                stateMixer.SetInputWeight(i, 1f);
                return;
            }

            // --- 2. IDLE NODE HANDLING ---
            if (posMag < 0.001f)
            {
                float polarDistSqIdle = (inputMag * inputMag);
                // Added a tiny epsilon (0.0001f) to prevent divide-by-zero explosions
                weights[i] = 1f / (polarDistSqIdle * polarDistSqIdle + 0.0001f);
                totalWeight += weights[i];
                continue;
            }

            // --- 3. DIRECTIONAL POLAR DISTANCE ---
            Vector2 posDir = pos / posMag;
            float dot = Vector2.Dot(inputDir, posDir);

            // THE FIX 1: Smooth Hemisphere Falloff (No more hard 'if' cutoff!)
            // dot ranges from 1 (perfect) to -1 (exact opposite).
            // We map this to a penalty: 0 (perfect) to 2 (opposite).
            float angleMetric = 1f - dot;

            // THE FIX 2: Stateless Origin Singularity Fix
            // As input magnitude approaches 0, the direction becomes hypersensitive and meaningless.
            // By scaling the penalty by inputMag, the angle matters less and less as you stop!
            float dynamicAnglePenalty = baseAnglePenalty * inputMag;

            float magDiff = Mathf.Abs(inputMag - posMag);

            // The "Warped" Distance
            float warpedAngle = angleMetric * dynamicAnglePenalty;
            float polarDistSq = (warpedAngle * warpedAngle) + (magDiff * magDiff);

            // Power of 4 falloff
            weights[i] = 1f / (polarDistSq * polarDistSq + 0.0001f);
            totalWeight += weights[i];
        }

        // --- 4. NORMALIZE AND APPLY ---
        for (int i = 0; i < count; i++)
        {
            float finalWeight = totalWeight > 0.0001f ? (weights[i] / totalWeight) : 0f;

            if (totalWeight <= 0.0001f && i == 0) finalWeight = 1f;

            stateMixer.SetInputWeight(i, finalWeight);
        }
    }
}
