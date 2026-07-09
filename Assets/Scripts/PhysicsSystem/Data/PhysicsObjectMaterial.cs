using Fusion;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "physicsobjectmaterial", menuName = "PhysicsSystem/PhysicsObjectMaterial", order = 1)]
public class PhysicsObjectMaterial : ScriptableObject
{
    #region Identity & Visuals
    [Header("Identity")]
    public string material_name;
    public PHYSICS_OBJECT_MATERIAL label;

    public Material vfx_material;
    public bool casts_shadows = true;
    public Color shatter_particle_color;

    protected static readonly int FrozenID = Shader.PropertyToID("_Frozen");
    protected static readonly int HeatedID = Shader.PropertyToID("_Heated");
    protected static readonly int BurntID = Shader.PropertyToID("_Burnt");
    protected static readonly int GooifiedID = Shader.PropertyToID("_Gooified");
    protected static readonly int StoneifiedID = Shader.PropertyToID("_Stoneified");
    protected static readonly int ChargedID = Shader.PropertyToID("_Charged");
    #endregion

    [Header("Core Data Profile")]
    public MaterialData baseData;

    [Header("Warp Overrides")]
    public MaterialData stoneifyOverride;
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

        // ==========================================
        // PHASE 1: DRIVER EVOLUTION
        // Evolve current state based on previous state
        // ==========================================
        float tempDelta = CalculateTemperatureDelta(in prevState, deltaTime);
        float wetnessDelta = CalculateWetnessDelta(in prevState, deltaTime);
        float stoneifyDelta = CalculateStoneifyDelta(in prevState, deltaTime);
        float gooifyDelta = CalculateGooifyDelta(in prevState, deltaTime);

        currentState.Temperature += tempDelta;
        currentState.Wetness = Mathf.Clamp01(currentState.Wetness + wetnessDelta);
        currentState.Stoneify = Mathf.Clamp01(currentState.Stoneify + stoneifyDelta);
        currentState.Gooify = Mathf.Clamp01(currentState.Gooify + gooifyDelta);

        // ==========================================
        // PHASE 2: CONDITION EVALUATION (Chemistry)
        // Read Previous Conditions -> Write Current Conditions
        // ==========================================
        ConditionAccumulator condAcc = new ConditionAccumulator();

        // 1. Accumulate Base Material (Weight: 1.0)
        if (baseData != null)
        {
            // We spoof 'applyWhenUsedAsWarp' to true here just for the base calculation, 
            // ensuring the base material always calculates its conditions.
            baseData.frozenCondition.applyWhenUsedAsWarp = true;
            baseData.burningCondition.applyWhenUsedAsWarp = true;
            baseData.heatedCondition.applyWhenUsedAsWarp = true;
            baseData.conductiveCondition.applyWhenUsedAsWarp = true;
            baseData.AccumulateConditions(ref condAcc, 1.0f, in prevState);
        }

        // 2. Accumulate Warps
        if (currentState.Stoneify > 0f && stoneifyOverride != null) stoneifyOverride.AccumulateConditions(ref condAcc, currentState.Stoneify, in prevState);
        if (currentState.Gooify > 0f && gooifyOverride != null) gooifyOverride.AccumulateConditions(ref condAcc, currentState.Gooify, in prevState);

        // 3. Resolve and write to currentState
        currentState.Frozen = condAcc.frozen.Resolve(0f);
        currentState.Heated = condAcc.heated.Resolve(0f);
        currentState.Burning = condAcc.burning.Resolve(0f);
        currentState.Conductive = condAcc.conductive.Resolve(0f);
    }

    protected virtual void ProcessActiveEffects(int simTick, ref MaterialState currentState, NetworkArray<ActiveStatusEffectData> activeEffects, PhysicsObject target, NetworkedMemoryAllocator memory)
    {
        for (int i = 0; i < activeEffects.Length; i++)
        {
            ActiveStatusEffectData effect = activeEffects.Get(i);
            if (effect.EffectID == 0) continue;

            IStatusEffect logic = StatusEffectRegistry.GetStatusEffect(effect.EffectID);
            if (logic == null) continue;

            if (effect.IsExpired(simTick)) continue;

            logic.Tick(simTick, target, memory, ref effect, ref currentState, this);
        }
    }

    #region State EVOLUTION calculates state decay / changes per tick
    protected virtual float CalculateTemperatureDelta(in MaterialState previous, float deltaTime)
    {
        // Evaporative cooling: Being wet actively pulls heat out of the object
        float evaporativeCooling = previous.Wetness > 0f ? 10f * deltaTime : 0f;

        // Move towards 0 (Ambient)
        float targetTemp = 0f;
        float currentTemp = previous.Temperature;

        float step = (ambientCoolingRate * deltaTime) + evaporativeCooling;
        return Mathf.MoveTowards(currentTemp, targetTemp, step) - currentTemp; // Return the delta
    }

    protected virtual float CalculateWetnessDelta(in MaterialState previous, float deltaTime)
    {
        // Heat evaporation: High temps boil off water faster
        float heatEvaporation = previous.Temperature > 50f ? (previous.Temperature / 50f) * deltaTime : 0f;

        float step = (naturalDryingRate * deltaTime) + heatEvaporation;
        return Mathf.MoveTowards(previous.Wetness, 0f, step) - previous.Wetness;
    }

    protected virtual float CalculateStoneifyDelta(in MaterialState previous, float deltaTime)
    {
        // Warps naturally decay over time unless sustained by a spell
        return Mathf.MoveTowards(previous.Stoneify, 0f, warpDecayRate * deltaTime) - previous.Stoneify;
    }

    protected virtual float CalculateGooifyDelta(in MaterialState previous, float deltaTime)
    {
        return Mathf.MoveTowards(previous.Gooify, 0f, warpDecayRate * deltaTime) - previous.Gooify;
    }
    #endregion



    #region 3. Gets sim properties to pass to physics/ xpbd
    public virtual SimProperties GetSimProperties(in MaterialState state, float baseSize, float baseGravityMultiplier)
    {
        SimAccumulator acc = new SimAccumulator();

        if (baseData != null)
        {
            baseData.AccumulateProperties(ref acc, 1.0f, in state);
        }

        // Pushes any active Warps into the accumulator at their respective intensities
        if (state.Stoneify > 0f && stoneifyOverride != null) stoneifyOverride.AccumulateProperties(ref acc, state.Stoneify, in state);
        if (state.Gooify > 0f && gooifyOverride != null) gooifyOverride.AccumulateProperties(ref acc, state.Gooify, in state);

        SimProperties finalProps = new SimProperties();

        finalProps.Friction = Mathf.Max(0f, acc.friction.Resolve(0f));
        finalProps.Bounce = Mathf.Clamp01(acc.restitution.Resolve(0f));
        finalProps.Hardness = acc.hardness.Resolve(0f);
        finalProps.Brittleness = acc.brittleness.Resolve(0f);
        finalProps.Stickiness = Mathf.Clamp01(acc.stickiness.Resolve(0f));

        float finalDensity = acc.density.Resolve(1f);

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

    #endregion

    #region ApplyVisuals
    public virtual void UpdateVisuals(PhysicsObject context, VisualStateData visualState, MaterialPropertyBlock mpb, Renderer[] renderers, float deltaTime)
    {
        // 1. Grab the current networked physics state
        MaterialState simState = context.physicsObjectProperties.CachedNetworkState.State;

        // 2. Smoothly lerp the visual floats toward the rigid networked floats
        // We map the "Conductive" condition to the "Charged" visual state
        visualState.VisualFrozen = Mathf.Lerp(visualState.VisualFrozen, simState.Frozen, deltaTime * 10f);
        visualState.VisualHeated = Mathf.Lerp(visualState.VisualHeated, simState.Heated, deltaTime * 10f);
        visualState.VisualBurnt = Mathf.Lerp(visualState.VisualBurnt, simState.Burning, deltaTime * 10f);
        visualState.VisualGooified = Mathf.Lerp(visualState.VisualGooified, simState.Gooify, deltaTime * 10f);
        visualState.VisualStoneified = Mathf.Lerp(visualState.VisualStoneified, simState.Stoneify, deltaTime * 10f);
        visualState.VisualCharged = Mathf.Lerp(visualState.VisualCharged, simState.Conductive, deltaTime * 10f);

        // 3. Inject into the Material Property Block and apply to all renderers
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            renderers[i].GetPropertyBlock(mpb);

            // Feed the smoothed values into the shader
            mpb.SetFloat(FrozenID, visualState.VisualFrozen);
            mpb.SetFloat(HeatedID, visualState.VisualHeated);
            mpb.SetFloat(BurntID, visualState.VisualBurnt);
            mpb.SetFloat(GooifiedID, visualState.VisualGooified);
            mpb.SetFloat(StoneifiedID, visualState.VisualStoneified);
            mpb.SetFloat(ChargedID, visualState.VisualCharged);

            renderers[i].SetPropertyBlock(mpb);
        }
    }
    #endregion
}

public enum PHYSICS_OBJECT_MATERIAL
{

    NULL = 0,
    STONE = 1,
    GLASS = 2,
    GOO = 3,

    PLAYER = 4,

    MAX_N
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
    // LAYER 0: The final mechanical floats fed directly to Unity's Rigidbody/Colliders
    public float Mass;
    public float Scale;
    public float Friction;
    public float Bounce; // Bounce
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

    // --- NEW: Networked Conditions ---
    public float Frozen;
    public float Heated;
    public float Burning;
    public float Conductive;

    public float ScaleMultiplier;
    public float DensityMultiplier;
    public float GravityMultiplier;

    public void Reset()
    {
        Temperature = 0f; Wetness = 0f; Charge = 0f;
        Stoneify = 0f; Gooify = 0f;
        Frozen = 0f; Heated = 0f; Burning = 0f; Conductive = 0f;
        ScaleMultiplier = 1f; DensityMultiplier = 1f; GravityMultiplier = 1f;
    }

    // Helper method allowing curves to seamlessly pull whatever they need
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
    public float VisualStoneified;
    public float VisualCharged;
}