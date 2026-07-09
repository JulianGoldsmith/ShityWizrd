using System.Collections.Generic;
using UnityEngine;

public enum DriverOrCondition
{
    None,
    Temperature,
    Wetness,
    Charge,
    Frozen,
    Heated,
    Burning,
    Conductive
}

[System.Serializable]
public struct PhysicsPropertyBlock
{
    public float Density;
    public float Friction;
    public float Restitution;
    public float Hardness;
    public float Brittleness;
    public float Stickiness;

    public static PhysicsPropertyBlock Lerp(PhysicsPropertyBlock a, PhysicsPropertyBlock b, float t)
    {
        return new PhysicsPropertyBlock
        {
            Density = Mathf.Lerp(a.Density, b.Density, t),
            Friction = Mathf.Lerp(a.Friction, b.Friction, t),
            Restitution = Mathf.Lerp(a.Restitution, b.Restitution, t),
            Hardness = Mathf.Lerp(a.Hardness, b.Hardness, t),
            Brittleness = Mathf.Lerp(a.Brittleness, b.Brittleness, t),
            Stickiness = Mathf.Lerp(a.Stickiness, b.Stickiness, t)
        };
    }
}

[System.Serializable]
public struct ConditionEvaluator
{
    public bool applyWhenUsedAsWarp;
    public DriverOrCondition targetDriver;
    public float beginThreshold;
    public float completeThreshold;
    public AnimationCurve transitionCurve;

    public float Evaluate(float targetValue)
    {
        if (beginThreshold == completeThreshold) return 0f;
        float t = Mathf.InverseLerp(beginThreshold, completeThreshold, targetValue);
        if (transitionCurve == null || transitionCurve.length == 0) return t;
        return transitionCurve.Evaluate(t);
    }
}

[CreateAssetMenu(fileName = "NewMaterialData", menuName = "PhysicsSystem/MaterialData")]
public class MaterialData : ScriptableObject
{
    [Header("Base Properties")]
    public PhysicsPropertyBlock baseProperties;

    [Header("Conditions (Interpreters)")]
    public ConditionEvaluator frozenCondition;
    [HideInInspector] public bool useFrozenBlock;
    public PhysicsPropertyBlock frozenProperties;

    public ConditionEvaluator heatedCondition;
    [HideInInspector] public bool useHeatedBlock;
    public PhysicsPropertyBlock heatedProperties;

    public ConditionEvaluator burningCondition;
    [HideInInspector] public bool useBurningBlock;
    public PhysicsPropertyBlock burningProperties;

    public ConditionEvaluator conductiveCondition;
    [HideInInspector] public bool useConductiveBlock;
    public PhysicsPropertyBlock conductiveProperties;

    [Header("Audio & Visuals")]
    public Material vfx_material;
    public bool casts_shadows = true;
    public Color shatter_particle_color;

    // Automatically applies sensible defaults when creating a new asset in the project
   

    public virtual void AccumulateConditions(ref ConditionAccumulator acc, float intensity, in MaterialState prevState)
    {
        if (intensity <= 0f) return;

        if (frozenCondition.applyWhenUsedAsWarp) acc.frozen.Add(frozenCondition.Evaluate(prevState.GetValue(frozenCondition.targetDriver)), intensity);
        if (heatedCondition.applyWhenUsedAsWarp) acc.heated.Add(heatedCondition.Evaluate(prevState.GetValue(heatedCondition.targetDriver)), intensity);
        if (burningCondition.applyWhenUsedAsWarp) acc.burning.Add(burningCondition.Evaluate(prevState.GetValue(burningCondition.targetDriver)), intensity);
        if (conductiveCondition.applyWhenUsedAsWarp) acc.conductive.Add(conductiveCondition.Evaluate(prevState.GetValue(conductiveCondition.targetDriver)), intensity);
    }

    public virtual void AccumulateProperties(ref SimAccumulator acc, float intensity, in MaterialState currentState)
    {
        if (intensity <= 0f) return;

        PhysicsPropertyBlock workingBlock = baseProperties;

        if (useFrozenBlock && currentState.Frozen > 0f) workingBlock = PhysicsPropertyBlock.Lerp(workingBlock, frozenProperties, currentState.Frozen);
        if (useHeatedBlock && currentState.Heated > 0f) workingBlock = PhysicsPropertyBlock.Lerp(workingBlock, heatedProperties, currentState.Heated);
        if (useBurningBlock && currentState.Burning > 0f) workingBlock = PhysicsPropertyBlock.Lerp(workingBlock, burningProperties, currentState.Burning);
        if (useConductiveBlock && currentState.Conductive > 0f) workingBlock = PhysicsPropertyBlock.Lerp(workingBlock, conductiveProperties, currentState.Conductive);

        acc.density.Add(workingBlock.Density, intensity);
        acc.friction.Add(workingBlock.Friction, intensity);
        acc.restitution.Add(workingBlock.Restitution, intensity);
        acc.hardness.Add(workingBlock.Hardness, intensity);
        acc.brittleness.Add(workingBlock.Brittleness, intensity);
        acc.stickiness.Add(workingBlock.Stickiness, intensity);
    }

    private void Reset()
    {
        baseProperties = new PhysicsPropertyBlock
        {
            Density = 1.0f,
            Friction = 0.5f,
            Restitution = 0.2f,
            Hardness = 0.5f,
            Brittleness = 0.1f,
            Stickiness = 0.0f
        };

        frozenCondition.transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        heatedCondition.transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        burningCondition.transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        conductiveCondition.transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }
}

public struct WeightedProperty
{
    private float valueSum;
    private float weightSum;

    public void Add(float targetValue, float weight)
    {
        valueSum += targetValue * weight;
        weightSum += weight;
    }

    public float Resolve(float baseValue)
    {
        if (weightSum <= 0f) return baseValue;
        if (weightSum >= 1f) return valueSum / weightSum;
        return valueSum + (baseValue * (1f - weightSum));
    }
}

public struct SimAccumulator
{
    public WeightedProperty density, friction, restitution, hardness, brittleness, stickiness;
}

public struct ConditionAccumulator
{
    public WeightedProperty frozen, heated, burning, conductive;
}