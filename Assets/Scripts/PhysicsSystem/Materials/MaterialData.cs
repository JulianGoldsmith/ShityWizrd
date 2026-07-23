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
    public bool useDensity;
    public float Density;

    public bool useFriction;
    public float Friction;

    public bool useRestitution;
    public float Restitution;

    public bool useHardness;
    public float Hardness;

    public bool useBrittleness;
    public float Brittleness;

    public bool useStickiness;
    public float Stickiness;

    public void SetAllInvolved(bool involved)
    {
        useDensity = involved;
        useFriction = involved;
        useRestitution = involved;
        useHardness = involved;
        useBrittleness = involved;
        useStickiness = involved;
    }

    public static PhysicsPropertyBlock ApplyConditionOverride(PhysicsPropertyBlock currentBlock, in PhysicsPropertyBlock overrideBlock, float intensity)
    {
        float clampedIntensity = Mathf.Clamp01(intensity);
        PhysicsPropertyBlock result = currentBlock;

        if (overrideBlock.useDensity)
        {
            result.useDensity = true;
            result.Density = Mathf.Lerp(currentBlock.Density, overrideBlock.Density, clampedIntensity);
        }

        if (overrideBlock.useFriction)
        {
            result.useFriction = true;
            result.Friction = Mathf.Lerp(currentBlock.Friction, overrideBlock.Friction, clampedIntensity);
        }

        if (overrideBlock.useRestitution)
        {
            result.useRestitution = true;
            result.Restitution = Mathf.Lerp(currentBlock.Restitution, overrideBlock.Restitution, clampedIntensity);
        }

        if (overrideBlock.useHardness)
        {
            result.useHardness = true;
            result.Hardness = Mathf.Lerp(currentBlock.Hardness, overrideBlock.Hardness, clampedIntensity);
        }

        if (overrideBlock.useBrittleness)
        {
            result.useBrittleness = true;
            result.Brittleness = Mathf.Lerp(currentBlock.Brittleness, overrideBlock.Brittleness, clampedIntensity);
        }

        if (overrideBlock.useStickiness)
        {
            result.useStickiness = true;
            result.Stickiness = Mathf.Lerp(currentBlock.Stickiness, overrideBlock.Stickiness, clampedIntensity);
        }

        return result;
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

        float normalizedValue = Mathf.InverseLerp(beginThreshold, completeThreshold, targetValue);

        if (transitionCurve == null || transitionCurve.length == 0) return normalizedValue;

        return transitionCurve.Evaluate(normalizedValue);
    }
}

public enum BonkStressType
{
    Hot,
    Cold,
    Burn,
    Shock
}

[System.Serializable]
public struct BonkResponse
{
    public bool addsBonk;
    public BonkStressType stressType;
    public float minBonk;
    public float maxBonk;
    public AnimationCurve responseCurve;

    public float Evaluate(float conditionValue)
    {
        if (!addsBonk || conditionValue <= 0f) return 0f;

        float normalizedCondition = Mathf.Clamp01(conditionValue);
        float curveValue = responseCurve == null || responseCurve.length == 0 ? normalizedCondition : responseCurve.Evaluate(normalizedCondition);

        return Mathf.Lerp(minBonk, maxBonk, Mathf.Clamp01(curveValue));
    }

    public static BonkResponse CreateDefault(BonkStressType stressType)
    {
        return new BonkResponse
        {
            addsBonk = false,
            stressType = stressType,
            minBonk = 0f,
            maxBonk = 30f,
            responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f)
        };
    }
}

[System.Serializable]
public struct CalculatedConditionState
{
    public bool useFrozen;
    public float Frozen;

    public bool useHeated;
    public float Heated;

    public bool useBurning;
    public float Burning;

    public bool useConductive;
    public float Conductive;
}

[System.Serializable]
public struct BonkBreakdown
{
    public float Hot;
    public float Cold;
    public float Burn;
    public float Shock;

    public float Total => Hot + Cold + Burn + Shock;

    public void Add(BonkStressType stressType, float value)
    {
        switch (stressType)
        {
            case BonkStressType.Hot: Hot += value; break;
            case BonkStressType.Cold: Cold += value; break;
            case BonkStressType.Burn: Burn += value; break;
            case BonkStressType.Shock: Shock += value; break;
        }
    }
}

[System.Serializable]
public struct BonkBreakdownInvolvement
{
    public bool Hot;
    public bool Cold;
    public bool Burn;
    public bool Shock;

    public void SetInvolved(BonkStressType stressType, bool involved)
    {
        switch (stressType)
        {
            case BonkStressType.Hot: Hot = involved; break;
            case BonkStressType.Cold: Cold = involved; break;
            case BonkStressType.Burn: Burn = involved; break;
            case BonkStressType.Shock: Shock = involved; break;
        }
    }
}

[System.Serializable]
public struct CalculatedMaterialState
{
    public CalculatedConditionState Conditions;
    public PhysicsPropertyBlock Properties;
    public BonkBreakdown Bonk;
    public BonkBreakdownInvolvement BonkInvolvement;

    public static CalculatedMaterialState CreateDefault()
    {
        PhysicsPropertyBlock defaultProperties = new PhysicsPropertyBlock
        {
            Density = 1f,
            Friction = 0.5f,
            Restitution = 0.2f,
            Hardness = 0.5f,
            Brittleness = 0.1f,
            Stickiness = 0f
        };

        defaultProperties.SetAllInvolved(true);

        return new CalculatedMaterialState
        {
            Conditions = new CalculatedConditionState
            {
                useFrozen = true,
                useHeated = true,
                useBurning = true,
                useConductive = true
            },
            Properties = defaultProperties
        };
    }
}

[CreateAssetMenu(fileName = "NewMaterialData", menuName = "PhysicsSystem/MaterialData")]
public class MaterialData : ScriptableObject
{
    [Header("Base Properties")]
    public PhysicsPropertyBlock baseProperties;

    public ConditionEvaluator frozenCondition;
    public BonkResponse frozenBonkResponse;
    [HideInInspector] public bool useFrozenBlock;
    public PhysicsPropertyBlock frozenProperties;

    public ConditionEvaluator heatedCondition;
    public BonkResponse heatedBonkResponse;
    [HideInInspector] public bool useHeatedBlock;
    public PhysicsPropertyBlock heatedProperties;

    public ConditionEvaluator burningCondition;
    public BonkResponse burningBonkResponse;
    [HideInInspector] public bool useBurningBlock;
    public PhysicsPropertyBlock burningProperties;

    public ConditionEvaluator conductiveCondition;
    public BonkResponse conductiveBonkResponse;
    [HideInInspector] public bool useConductiveBlock;
    public PhysicsPropertyBlock conductiveProperties;

    [Header("Audio & Visuals")]
    public Material visual_material;
    public bool casts_shadows = true;
    public Color shatter_particle_color;

    public virtual CalculatedMaterialState CalculateMaterialState(in MaterialState materialState, bool isBaseMaterial)
    {
        CalculatedMaterialState calculatedMaterialState = new CalculatedMaterialState();

        calculatedMaterialState.Conditions = CalculateConditions(in materialState, isBaseMaterial);
        calculatedMaterialState.Properties = CalculateProperties(in calculatedMaterialState.Conditions, isBaseMaterial);

        AddBonkResponse(ref calculatedMaterialState, in frozenBonkResponse, calculatedMaterialState.Conditions.Frozen, calculatedMaterialState.Conditions.useFrozen);
        AddBonkResponse(ref calculatedMaterialState, in heatedBonkResponse, calculatedMaterialState.Conditions.Heated, calculatedMaterialState.Conditions.useHeated);
        AddBonkResponse(ref calculatedMaterialState, in burningBonkResponse, calculatedMaterialState.Conditions.Burning, calculatedMaterialState.Conditions.useBurning);
        AddBonkResponse(ref calculatedMaterialState, in conductiveBonkResponse, calculatedMaterialState.Conditions.Conductive, calculatedMaterialState.Conditions.useConductive);

        return calculatedMaterialState;
    }

    private CalculatedConditionState CalculateConditions(in MaterialState materialState, bool isBaseMaterial)
    {
        CalculatedConditionState calculatedConditions = new CalculatedConditionState();

        calculatedConditions.useFrozen = isBaseMaterial || frozenCondition.applyWhenUsedAsWarp;
        calculatedConditions.useHeated = isBaseMaterial || heatedCondition.applyWhenUsedAsWarp;
        calculatedConditions.useBurning = isBaseMaterial || burningCondition.applyWhenUsedAsWarp;
        calculatedConditions.useConductive = isBaseMaterial || conductiveCondition.applyWhenUsedAsWarp;

        if (calculatedConditions.useFrozen) calculatedConditions.Frozen = Mathf.Clamp01(frozenCondition.Evaluate(materialState.GetValue(frozenCondition.targetDriver)));
        if (calculatedConditions.useHeated) calculatedConditions.Heated = Mathf.Clamp01(heatedCondition.Evaluate(materialState.GetValue(heatedCondition.targetDriver)));
        if (calculatedConditions.useBurning) calculatedConditions.Burning = Mathf.Clamp01(burningCondition.Evaluate(materialState.GetValue(burningCondition.targetDriver)));
        if (calculatedConditions.useConductive) calculatedConditions.Conductive = Mathf.Clamp01(conductiveCondition.Evaluate(materialState.GetValue(conductiveCondition.targetDriver)));

        return calculatedConditions;
    }

    private PhysicsPropertyBlock CalculateProperties(in CalculatedConditionState calculatedConditions, bool isBaseMaterial)
    {
        PhysicsPropertyBlock workingBlock = baseProperties;

        if (isBaseMaterial) workingBlock.SetAllInvolved(true);

        if (useFrozenBlock && calculatedConditions.useFrozen && calculatedConditions.Frozen > 0f) workingBlock = PhysicsPropertyBlock.ApplyConditionOverride(workingBlock, in frozenProperties, calculatedConditions.Frozen);
        if (useHeatedBlock && calculatedConditions.useHeated && calculatedConditions.Heated > 0f) workingBlock = PhysicsPropertyBlock.ApplyConditionOverride(workingBlock, in heatedProperties, calculatedConditions.Heated);
        if (useBurningBlock && calculatedConditions.useBurning && calculatedConditions.Burning > 0f) workingBlock = PhysicsPropertyBlock.ApplyConditionOverride(workingBlock, in burningProperties, calculatedConditions.Burning);
        if (useConductiveBlock && calculatedConditions.useConductive && calculatedConditions.Conductive > 0f) workingBlock = PhysicsPropertyBlock.ApplyConditionOverride(workingBlock, in conductiveProperties, calculatedConditions.Conductive);

        return workingBlock;
    }

    private void AddBonkResponse(ref CalculatedMaterialState calculatedMaterialState, in BonkResponse bonkResponse, float conditionValue, bool conditionIsInvolved)
    {
        if (!conditionIsInvolved || !bonkResponse.addsBonk) return;

        calculatedMaterialState.Bonk.Add(bonkResponse.stressType, bonkResponse.Evaluate(conditionValue));
        calculatedMaterialState.BonkInvolvement.SetInvolved(bonkResponse.stressType, true);
    }

    private void Reset()
    {
        baseProperties = new PhysicsPropertyBlock
        {
            useDensity = true,
            Density = 1f,
            useFriction = true,
            Friction = 0.5f,
            useRestitution = true,
            Restitution = 0.2f,
            useHardness = true,
            Hardness = 0.5f,
            useBrittleness = true,
            Brittleness = 0.1f,
            useStickiness = true,
            Stickiness = 0f
        };

        frozenCondition.transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        heatedCondition.transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        burningCondition.transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        conductiveCondition.transitionCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        frozenBonkResponse = BonkResponse.CreateDefault(BonkStressType.Cold);
        heatedBonkResponse = BonkResponse.CreateDefault(BonkStressType.Hot);
        burningBonkResponse = BonkResponse.CreateDefault(BonkStressType.Burn);
        conductiveBonkResponse = BonkResponse.CreateDefault(BonkStressType.Shock);
    }
}