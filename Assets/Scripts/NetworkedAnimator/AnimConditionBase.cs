using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public abstract class AnimConditionBase
{
    public abstract bool Evaluate(GameObject characterHull);
}

public enum FloatOperator
{
    GreaterThan,
    LessThan,
    EqualTo,
    NotEqualTo
}

[Serializable]
public class FloatCondition : AnimConditionBase
{
    [SerializeField, AnimParameter(AnimParamType.Float)]
    public string ParameterName;

    [Tooltip("How to compare the value.")]
    public FloatOperator Operator;

    [Tooltip("The value to compare against.")]
    public float Threshold;

    public override bool Evaluate(GameObject characterHull)
    {
        var anim = characterHull.GetComponent<NetworkAnimator>();
        if (anim == null) return false;

        float val = anim.GetSimFloat(ParameterName);

        switch (Operator)
        {
            case FloatOperator.GreaterThan:
                return val > Threshold;

            case FloatOperator.LessThan:
                return val < Threshold;

            case FloatOperator.EqualTo:
                // We use a small epsilon for float equality to prevent physics micro-jitter bugs
                return Mathf.Abs(val - Threshold) <= 0.001f;

            case FloatOperator.NotEqualTo:
                return Mathf.Abs(val - Threshold) > 0.001f;

            default:
                return false;
        }
    }
}


[Serializable]
public class BoolCondition : AnimConditionBase
{
    [SerializeField, AnimParameter(AnimParamType.Bool)]
    public string ParameterName;

    [Tooltip("Should this transition happen when the bool is True or False?")]
    public bool ExpectedValue = true; // Unity renders this as a checkbox, effectively giving you True/False

    public override bool Evaluate(GameObject characterHull)
    {
        var anim = characterHull.GetComponent<NetworkAnimator>();
        if (anim == null) return false;

        // Simply check if the parameter matches our expected value
        return anim.GetSimBool(ParameterName) == ExpectedValue;
    }
}

[Serializable]
public class TriggerCondition : AnimConditionBase
{
    [SerializeField, AnimParameter(AnimParamType.Trigger)]
    public string ParameterName;

    public override bool Evaluate(GameObject characterHull)
    {
        var anim = characterHull.GetComponent<NetworkAnimator>();
        if (anim == null) return false;

        return anim.GetSimTrigger(ParameterName);
    }
}

[Serializable]
public class StateTimeCondition : AnimConditionBase
{
    [Tooltip("How many seconds must pass in the current state before this is true?")]
    public float MinimumSecondsInState = 0.2f;

    public override bool Evaluate(GameObject characterHull)
    {
        var anim = characterHull.GetComponent<NetworkAnimator>();
        if (anim == null) return false;

        // Calculate exactly how long we have been in this state based on the deterministic tick
        int ticksInState = anim.Runner.Tick - anim.AnimState.TransitionStartTick;
        float secondsInState = ticksInState * anim.Runner.DeltaTime;

        return secondsInState >= MinimumSecondsInState;
    }
}