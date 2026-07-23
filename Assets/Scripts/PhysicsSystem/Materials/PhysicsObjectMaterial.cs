using Fusion;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "physicsobjectmaterial", menuName = "PhysicsSystem/PhysicsObjectMaterial", order = 1)]
public class PhysicsObjectMaterial : ScriptableObject
{
    #region Identity & Visuals

    [Header("Identity")]
    public string material_name;
    public ushort NetworkMaterialID;

    public Material visual_material;
    public bool casts_shadows = true;
    public Color shatter_particle_color;
    public float gooFlowSpeed = 0.1f;

    protected static readonly int FrozenID = Shader.PropertyToID("_Frozen");
    protected static readonly int HeatedID = Shader.PropertyToID("_Heated");
    protected static readonly int BurntID = Shader.PropertyToID("_Burnt");
    protected static readonly int GooifiedID = Shader.PropertyToID("_Gooified");
    protected static readonly int GooFlowOffsetOSID = Shader.PropertyToID("_GooFlowOffsetOS");
    protected static readonly int StoneifiedID = Shader.PropertyToID("_Stoneified");
    protected static readonly int ChargedID = Shader.PropertyToID("_Charged");

    #endregion

    [Header("Core Data Profile")]
    public MaterialData baseData;

    [Header("Transform Warp Overrides")]
    public MaterialData stoneifyOverride;

    [Header("Coating Warp Overrides")]
    public MaterialData gooifyOverride;

    [Header("Base Evolution Rates")]
    public float ambientCoolingRate = 5f;
    public float naturalDryingRate = 0.05f;
    public float warpDecayRate = 0.1f;

    #region 1. For spell effects to mutate the material

    public virtual void MutateTemperature(ref MaterialState state, MutationType type, float value)
    {
        if (type == MutationType.Add) state.Temperature += value;
        else if (type == MutationType.Multiply) state.Temperature *= value;
    }

    public virtual void MutateWetness(ref MaterialState state, MutationType type, float value)
    {
        if (type == MutationType.Add) state.Wetness += value;
        else if (type == MutationType.SetMax) state.Wetness = Mathf.Max(state.Wetness, value);
    }

    public virtual void MutateStoneification(ref MaterialState state, MutationType type, float value)
    {
        if (type == MutationType.Add) state.Stoneify += value;
        else if (type == MutationType.SetMax) state.Stoneify = Mathf.Max(state.Stoneify, value);
    }

    public virtual void MutateGooification(ref MaterialState state, MutationType type, float value)
    {
        if (type == MutationType.Add) state.Gooify += value;
        else if (type == MutationType.SetMax) state.Gooify = Mathf.Max(state.Gooify, value);
    }

    public virtual void MutateScale(ref MaterialState state, MutationType type, float value)
    {
        if (type == MutationType.Multiply) state.ScaleMultiplier *= value;
        else if (type == MutationType.Add) state.ScaleMultiplier += value;
    }

    public virtual void MutateDensity(ref MaterialState state, MutationType type, float value)
    {
        if (type == MutationType.Multiply) state.DensityMultiplier *= value;
    }

    #endregion

    #region 2. The Simulation Step

    public virtual void ResolveTick(int simTick, in MaterialState prevState, ref MaterialState currentState, NetworkArray<ActiveStatusEffectData> activeEffects, PhysicsObject target, NetworkedMemoryAllocator memory)
    {
        float deltaTime = target.Runner.DeltaTime;

        ProcessActiveEffects(simTick, ref currentState, activeEffects, target, memory);

        float tempDelta = CalculateTemperatureDelta(in prevState, deltaTime);
        float wetnessDelta = CalculateWetnessDelta(in prevState, deltaTime);
        float stoneifyDelta = CalculateStoneifyDelta(in prevState, deltaTime);
        float gooifyDelta = CalculateGooifyDelta(in prevState, deltaTime);

        currentState.Temperature += tempDelta;
        currentState.Wetness = Mathf.Clamp01(currentState.Wetness + wetnessDelta);
        currentState.Stoneify = Mathf.Clamp01(currentState.Stoneify + stoneifyDelta);
        currentState.Gooify = Mathf.Clamp01(currentState.Gooify + gooifyDelta);

        CalculatedMaterialState calculatedMaterialState = CalculateMaterialState(in currentState);

        currentState.Frozen = calculatedMaterialState.Conditions.Frozen;
        currentState.Heated = calculatedMaterialState.Conditions.Heated;
        currentState.Burning = calculatedMaterialState.Conditions.Burning;
        currentState.Conductive = calculatedMaterialState.Conditions.Conductive;
    }

    protected virtual void ProcessActiveEffects(int simTick, ref MaterialState currentState, NetworkArray<ActiveStatusEffectData> activeEffects, PhysicsObject target, NetworkedMemoryAllocator memory)
    {
        for (int i = 0; i < activeEffects.Length; i++)
        {
            ActiveStatusEffectData effect = activeEffects.Get(i);

            if (effect.EffectID == 0) continue;

            IStatusEffect logic = StatusEffectRegistry.GetStatusEffect(effect.EffectID);

            if (logic == null || effect.IsExpired(simTick)) continue;

            logic.Tick(simTick, target, memory, ref effect, ref currentState, this);
        }
    }

    protected virtual float CalculateTemperatureDelta(in MaterialState previous, float deltaTime)
    {
        float evaporativeCooling = previous.Wetness > 0f ? 10f * deltaTime : 0f;
        float step = (ambientCoolingRate * deltaTime) + evaporativeCooling;

        return Mathf.MoveTowards(previous.Temperature, 0f, step) - previous.Temperature;
    }

    protected virtual float CalculateWetnessDelta(in MaterialState previous, float deltaTime)
    {
        float heatEvaporation = previous.Temperature > 50f ? (previous.Temperature / 50f) * deltaTime : 0f;
        float step = (naturalDryingRate * deltaTime) + heatEvaporation;

        return Mathf.MoveTowards(previous.Wetness, 0f, step) - previous.Wetness;
    }

    protected virtual float CalculateStoneifyDelta(in MaterialState previous, float deltaTime)
    {
        return Mathf.MoveTowards(previous.Stoneify, 0f, warpDecayRate * deltaTime) - previous.Stoneify;
    }

    protected virtual float CalculateGooifyDelta(in MaterialState previous, float deltaTime)
    {
        return Mathf.MoveTowards(previous.Gooify, 0f, warpDecayRate * deltaTime) - previous.Gooify;
    }

    #endregion

    #region 3. Material Calculations

    public virtual CalculatedMaterialState CalculateMaterialState(in MaterialState materialState)
    {
        CalculatedMaterialState baseMaterialState = baseData != null ? baseData.CalculateMaterialState(in materialState, true) : CalculatedMaterialState.CreateDefault();
        CalculatedMaterialState transformedMaterialState = CalculateTransformWarps(in materialState, in baseMaterialState);
        CalculatedMaterialState finalMaterialState = CalculateCoatingWarps(in materialState, in transformedMaterialState);

        return finalMaterialState;
    }

    private CalculatedMaterialState CalculateTransformWarps(in MaterialState materialState, in CalculatedMaterialState baseMaterialState)
    {
        CalculatedMaterialState transformedMaterialState = baseMaterialState;

        if (stoneifyOverride == null || materialState.Stoneify <= 0f) return transformedMaterialState;

        float stoneifyValue = Mathf.Clamp01(materialState.Stoneify);
        CalculatedMaterialState stoneMaterialState = stoneifyOverride.CalculateMaterialState(in materialState, false);

        transformedMaterialState = BlendTransformWarp(in baseMaterialState, in stoneMaterialState, stoneifyValue);

        return transformedMaterialState;
    }

    private CalculatedMaterialState BlendTransformWarp(in CalculatedMaterialState baseMaterialState, in CalculatedMaterialState warpMaterialState, float warpValue)
    {
        CalculatedMaterialState result = baseMaterialState;

        result.Conditions.Frozen = BlendTransformValue(baseMaterialState.Conditions.Frozen, warpMaterialState.Conditions.Frozen, warpValue, warpMaterialState.Conditions.useFrozen);
        result.Conditions.Heated = BlendTransformValue(baseMaterialState.Conditions.Heated, warpMaterialState.Conditions.Heated, warpValue, warpMaterialState.Conditions.useHeated);
        result.Conditions.Burning = BlendTransformValue(baseMaterialState.Conditions.Burning, warpMaterialState.Conditions.Burning, warpValue, warpMaterialState.Conditions.useBurning);
        result.Conditions.Conductive = BlendTransformValue(baseMaterialState.Conditions.Conductive, warpMaterialState.Conditions.Conductive, warpValue, warpMaterialState.Conditions.useConductive);

        result.Properties.Density = BlendTransformValue(baseMaterialState.Properties.Density, warpMaterialState.Properties.Density, warpValue, warpMaterialState.Properties.useDensity);
        result.Properties.Friction = BlendTransformValue(baseMaterialState.Properties.Friction, warpMaterialState.Properties.Friction, warpValue, warpMaterialState.Properties.useFriction);
        result.Properties.Restitution = BlendTransformValue(baseMaterialState.Properties.Restitution, warpMaterialState.Properties.Restitution, warpValue, warpMaterialState.Properties.useRestitution);
        result.Properties.Hardness = BlendTransformValue(baseMaterialState.Properties.Hardness, warpMaterialState.Properties.Hardness, warpValue, warpMaterialState.Properties.useHardness);
        result.Properties.Brittleness = BlendTransformValue(baseMaterialState.Properties.Brittleness, warpMaterialState.Properties.Brittleness, warpValue, warpMaterialState.Properties.useBrittleness);
        result.Properties.Stickiness = BlendTransformValue(baseMaterialState.Properties.Stickiness, warpMaterialState.Properties.Stickiness, warpValue, warpMaterialState.Properties.useStickiness);

        result.Bonk.Hot = BlendTransformValue(baseMaterialState.Bonk.Hot, warpMaterialState.Bonk.Hot, warpValue, warpMaterialState.BonkInvolvement.Hot);
        result.Bonk.Cold = BlendTransformValue(baseMaterialState.Bonk.Cold, warpMaterialState.Bonk.Cold, warpValue, warpMaterialState.BonkInvolvement.Cold);
        result.Bonk.Burn = BlendTransformValue(baseMaterialState.Bonk.Burn, warpMaterialState.Bonk.Burn, warpValue, warpMaterialState.BonkInvolvement.Burn);
        result.Bonk.Shock = BlendTransformValue(baseMaterialState.Bonk.Shock, warpMaterialState.Bonk.Shock, warpValue, warpMaterialState.BonkInvolvement.Shock);

        return result;
    }

    private float BlendTransformValue(float baseValue, float warpTarget, float warpValue, bool warpIsInvolved)
    {
        if (!warpIsInvolved || warpValue <= 0f) return baseValue;

        float involvedWarpWeight = Mathf.Max(0f, warpValue);
        float baseWeight = Mathf.Max(0f, 1f - involvedWarpWeight);
        float totalWeight = baseWeight + involvedWarpWeight;

        if (totalWeight <= 0f) return baseValue;

        return ((baseValue * baseWeight) + (warpTarget * involvedWarpWeight)) / totalWeight;
    }

    private CalculatedMaterialState CalculateCoatingWarps(in MaterialState materialState, in CalculatedMaterialState transformedMaterialState)
    {
        CalculatedMaterialState finalMaterialState = transformedMaterialState;

        if (gooifyOverride != null && materialState.Gooify > 0f)
        {
            float gooifyValue = Mathf.Clamp01(materialState.Gooify);
            CalculatedMaterialState gooMaterialState = gooifyOverride.CalculateMaterialState(in materialState, false);

            ApplyCoatingWarp(ref finalMaterialState, in transformedMaterialState, in gooMaterialState, gooifyValue);
        }

        ClampCalculatedMaterialState(ref finalMaterialState);

        return finalMaterialState;
    }

    private void ApplyCoatingWarp(ref CalculatedMaterialState finalMaterialState, in CalculatedMaterialState transformedMaterialState, in CalculatedMaterialState coatingMaterialState, float coatingValue)
    {
        if (coatingMaterialState.Conditions.useFrozen) finalMaterialState.Conditions.Frozen += CalculateCoatingChange(transformedMaterialState.Conditions.Frozen, coatingMaterialState.Conditions.Frozen, coatingValue);
        if (coatingMaterialState.Conditions.useHeated) finalMaterialState.Conditions.Heated += CalculateCoatingChange(transformedMaterialState.Conditions.Heated, coatingMaterialState.Conditions.Heated, coatingValue);
        if (coatingMaterialState.Conditions.useBurning) finalMaterialState.Conditions.Burning += CalculateCoatingChange(transformedMaterialState.Conditions.Burning, coatingMaterialState.Conditions.Burning, coatingValue);
        if (coatingMaterialState.Conditions.useConductive) finalMaterialState.Conditions.Conductive += CalculateCoatingChange(transformedMaterialState.Conditions.Conductive, coatingMaterialState.Conditions.Conductive, coatingValue);

        if (coatingMaterialState.Properties.useDensity) finalMaterialState.Properties.Density += CalculateCoatingChange(transformedMaterialState.Properties.Density, coatingMaterialState.Properties.Density, coatingValue);
        if (coatingMaterialState.Properties.useFriction) finalMaterialState.Properties.Friction += CalculateCoatingChange(transformedMaterialState.Properties.Friction, coatingMaterialState.Properties.Friction, coatingValue);
        if (coatingMaterialState.Properties.useRestitution) finalMaterialState.Properties.Restitution += CalculateCoatingChange(transformedMaterialState.Properties.Restitution, coatingMaterialState.Properties.Restitution, coatingValue);
        if (coatingMaterialState.Properties.useHardness) finalMaterialState.Properties.Hardness += CalculateCoatingChange(transformedMaterialState.Properties.Hardness, coatingMaterialState.Properties.Hardness, coatingValue);
        if (coatingMaterialState.Properties.useBrittleness) finalMaterialState.Properties.Brittleness += CalculateCoatingChange(transformedMaterialState.Properties.Brittleness, coatingMaterialState.Properties.Brittleness, coatingValue);
        if (coatingMaterialState.Properties.useStickiness) finalMaterialState.Properties.Stickiness += CalculateCoatingChange(transformedMaterialState.Properties.Stickiness, coatingMaterialState.Properties.Stickiness, coatingValue);

        if (coatingMaterialState.BonkInvolvement.Hot) finalMaterialState.Bonk.Hot += CalculateCoatingChange(transformedMaterialState.Bonk.Hot, coatingMaterialState.Bonk.Hot, coatingValue);
        if (coatingMaterialState.BonkInvolvement.Cold) finalMaterialState.Bonk.Cold += CalculateCoatingChange(transformedMaterialState.Bonk.Cold, coatingMaterialState.Bonk.Cold, coatingValue);
        if (coatingMaterialState.BonkInvolvement.Burn) finalMaterialState.Bonk.Burn += CalculateCoatingChange(transformedMaterialState.Bonk.Burn, coatingMaterialState.Bonk.Burn, coatingValue);
        if (coatingMaterialState.BonkInvolvement.Shock) finalMaterialState.Bonk.Shock += CalculateCoatingChange(transformedMaterialState.Bonk.Shock, coatingMaterialState.Bonk.Shock, coatingValue);
    }

    private float CalculateCoatingChange(float transformedValue, float coatingTarget, float coatingValue)
    {
        return (coatingTarget - transformedValue) * Mathf.Max(0f, coatingValue);
    }

    private void ClampCalculatedMaterialState(ref CalculatedMaterialState calculatedMaterialState)
    {
        calculatedMaterialState.Conditions.Frozen = Mathf.Clamp01(calculatedMaterialState.Conditions.Frozen);
        calculatedMaterialState.Conditions.Heated = Mathf.Clamp01(calculatedMaterialState.Conditions.Heated);
        calculatedMaterialState.Conditions.Burning = Mathf.Clamp01(calculatedMaterialState.Conditions.Burning);
        calculatedMaterialState.Conditions.Conductive = Mathf.Clamp01(calculatedMaterialState.Conditions.Conductive);

        calculatedMaterialState.Properties.Density = Mathf.Max(0.01f, calculatedMaterialState.Properties.Density);
        calculatedMaterialState.Properties.Friction = Mathf.Max(0f, calculatedMaterialState.Properties.Friction);
        calculatedMaterialState.Properties.Restitution = Mathf.Clamp01(calculatedMaterialState.Properties.Restitution);
        calculatedMaterialState.Properties.Hardness = Mathf.Max(0f, calculatedMaterialState.Properties.Hardness);
        calculatedMaterialState.Properties.Brittleness = Mathf.Max(0f, calculatedMaterialState.Properties.Brittleness);
        calculatedMaterialState.Properties.Stickiness = Mathf.Clamp01(calculatedMaterialState.Properties.Stickiness);

        calculatedMaterialState.Bonk.Hot = Mathf.Max(0f, calculatedMaterialState.Bonk.Hot);
        calculatedMaterialState.Bonk.Cold = Mathf.Max(0f, calculatedMaterialState.Bonk.Cold);
        calculatedMaterialState.Bonk.Burn = Mathf.Max(0f, calculatedMaterialState.Bonk.Burn);
        calculatedMaterialState.Bonk.Shock = Mathf.Max(0f, calculatedMaterialState.Bonk.Shock);
    }

    public virtual SimProperties GetSimProperties(in MaterialState state, float baseSize, float baseGravityMultiplier)
    {
        CalculatedMaterialState calculatedMaterialState = CalculateMaterialState(in state);
        PhysicsPropertyBlock calculatedProperties = calculatedMaterialState.Properties;

        SimProperties finalProps = new SimProperties();

        finalProps.Friction = Mathf.Max(0f, calculatedProperties.Friction);
        finalProps.Bounce = Mathf.Clamp01(calculatedProperties.Restitution);
        finalProps.Hardness = Mathf.Max(0f, calculatedProperties.Hardness);
        finalProps.Brittleness = Mathf.Max(0f, calculatedProperties.Brittleness);
        finalProps.Stickiness = Mathf.Clamp01(calculatedProperties.Stickiness);

        float finalDensity = Mathf.Max(0.01f, calculatedProperties.Density * state.DensityMultiplier);
        float currentScale = baseSize * state.ScaleMultiplier;
        float volume = currentScale * currentScale * currentScale;

        finalProps.Mass = Mathf.Max(0.01f, (finalDensity * volume) + (state.Wetness * volume * 0.5f));
        finalProps.Scale = currentScale;
        finalProps.GravityMultiplier = baseGravityMultiplier * state.GravityMultiplier;
        finalProps.LinearDamping = finalProps.Hardness * 0.5f + (currentScale * 0.1f);
        finalProps.AngularDamping = finalProps.Mass * 0.05f;

        return finalProps;
    }

    #endregion

    #region 4. Bonk Calculations

    public virtual BonkBreakdown CalculateElementalBonk(in MaterialState state)
    {
        CalculatedMaterialState calculatedMaterialState = CalculateMaterialState(in state);

        return calculatedMaterialState.Bonk;
    }

    public virtual float CalculateKineticBonk(float collision_impulse, PhysicsObjectProperties myProperties, PhysicsObjectProperties otherProperties)
    {
        float mass = myProperties.mass > 0f ? myProperties.mass : 1f;
        float hardness_factor = myProperties.hardness > 0f ? myProperties.hardness : 1f;

        if (otherProperties != null) hardness_factor *= otherProperties.hardness > 0f ? otherProperties.hardness : 1f;

        float wall_or_floor_penalty = otherProperties == null ? 0.1f : 1f;

        return 150f * collision_impulse * hardness_factor * wall_or_floor_penalty * myProperties.brittleness * 0.1f / mass;
    }

    #endregion

    #region Apply Visuals

    public virtual void UpdateVisuals(PhysicsObject context, VisualStateData visualState, MaterialPropertyBlock mpb, List<Renderer> renderers, float deltaTime)
    {
        MaterialState simState = context.physicsObjectProperties.CachedNetworkState.State;

        visualState.VisualFrozen = Mathf.Lerp(visualState.VisualFrozen, simState.Frozen, deltaTime * 10f);
        visualState.VisualHeated = Mathf.Lerp(visualState.VisualHeated, simState.Heated, deltaTime * 10f);
        visualState.VisualBurnt = Mathf.Lerp(visualState.VisualBurnt, simState.Burning, deltaTime * 10f);
        visualState.VisualGooified = Mathf.Lerp(visualState.VisualGooified, simState.Gooify, deltaTime * 10f);
        visualState.VisualStoneified = Mathf.Lerp(visualState.VisualStoneified, simState.Stoneify, deltaTime * 10f);
        visualState.VisualCharged = Mathf.Lerp(visualState.VisualCharged, simState.Conductive, deltaTime * 10f);

        if (visualState.VisualGooified > 0.001f)
        {
            visualState.VisualGooFlowOffsetOS -= context.transform.InverseTransformDirection(Physics.gravity.normalized) * gooFlowSpeed * deltaTime;
        }

        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] == null) continue;

            renderers[i].GetPropertyBlock(mpb);

            mpb.SetFloat(FrozenID, visualState.VisualFrozen);
            mpb.SetFloat(HeatedID, visualState.VisualHeated);
            mpb.SetFloat(BurntID, visualState.VisualBurnt);
            mpb.SetFloat(GooifiedID, visualState.VisualGooified);
            mpb.SetFloat(StoneifiedID, visualState.VisualStoneified);
            mpb.SetFloat(ChargedID, visualState.VisualCharged);

            mpb.SetVector(GooFlowOffsetOSID, visualState.VisualGooFlowOffsetOS);

            renderers[i].SetPropertyBlock(mpb);
        }
    }

    #endregion
}

public enum MutationType
{
    Add,
    Multiply,
    SetMax,
    SetMin,
    Override
}

[System.Serializable]
public struct SimProperties
{
    public float Mass;
    public float Scale;
    public float Friction;
    public float Bounce;
    public float LinearDamping;
    public float AngularDamping;
    public float Brittleness;
    public float Hardness;
    public float Stickiness;
    public float GravityMultiplier;
}

public struct MaterialState : INetworkStruct
{
    public float Temperature;
    public float Wetness;
    public float Charge;

    public float Stoneify;
    public float Gooify;

    public float Frozen;
    public float Heated;
    public float Burning;
    public float Conductive;

    public float ScaleMultiplier;
    public float DensityMultiplier;
    public float GravityMultiplier;

    public void Reset()
    {
        Temperature = 0f;
        Wetness = 0f;
        Charge = 0f;

        Stoneify = 0f;
        Gooify = 0f;

        Frozen = 0f;
        Heated = 0f;
        Burning = 0f;
        Conductive = 0f;

        ScaleMultiplier = 1f;
        DensityMultiplier = 1f;
        GravityMultiplier = 1f;
    }

    public float GetValue(DriverOrCondition target)
    {
        switch (target)
        {
            case DriverOrCondition.Temperature: return Temperature;
            case DriverOrCondition.Wetness: return Wetness;
            case DriverOrCondition.Charge: return Charge;
            case DriverOrCondition.Frozen: return Frozen;
            case DriverOrCondition.Heated: return Heated;
            case DriverOrCondition.Burning: return Burning;
            case DriverOrCondition.Conductive: return Conductive;
            default: return 0f;
        }
    }
}

public class VisualStateData
{
    public float VisualFrozen;
    public float VisualHeated;
    public float VisualBurnt;
    public float VisualGooified;
    public Vector3 VisualGooFlowOffsetOS;
    public float VisualStoneified;
    public float VisualCharged;
}