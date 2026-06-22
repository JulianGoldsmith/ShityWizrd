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

    [Header("Visuals")]
    public Material vfx_material;
    public bool casts_shadows = true;
    public Color shatter_particle_color;
    protected static readonly int TempID = Shader.PropertyToID("_CurrentTemp");
    protected static readonly int WetID = Shader.PropertyToID("_CurrentWetness");
    #endregion

    #region Base Material Properties (Intensive)
    [Header("Base Physical Properties")]
    public float density = 1.0f;
    public float hardness = 1.0f;
    public float elasticity = 0.0f;
    public float brittleness = 0.1f;
    [Range(0, 1)] public float stickiness = 0.0f;
    [Range(0, 1)] public float friction = 0.0f;
    #endregion

    

    #region 1. The Gatekeeper API (Mutations)
    // Spells MUST call these. They cannot modify the state directly.
    // Making these virtual allows a 'StoneMaterial' to override and ignore heat.

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
    /// <summary>
    /// Processes exactly one tick of time for this material state.
    /// </summary>
    public virtual void ResolveTick(int simTick, ref MaterialState state, NetworkArray<ActiveStatusEffectData> activeEffects, PhysicsObject target, NetworkedMemoryAllocator memory)
    {
        for (int i = 0; i < activeEffects.Length; i++)
        {
            ActiveStatusEffectData effect = activeEffects.Get(i);
            if (effect.EffectID == 0) continue;

            IStatusEffect logic = StatusEffectRegistry.GetStatusEffect(effect.EffectID);
            if (logic == null) continue;

            if (effect.IsExpired(simTick)) continue;

            logic.Tick(simTick, target, memory, ref effect, ref state, this);
        }

        ApplyNaturalDecay(ref state, target.Runner.DeltaTime);
    }

    protected virtual void ApplyNaturalDecay(ref MaterialState state, float deltaTime)
    {
        // Example base decay: Everything slowly normalizes its temperature to 0
        state.Temperature = Mathf.Lerp(state.Temperature, 0f, deltaTime * 0.5f);
        state.Wetness = Mathf.Lerp(state.Wetness, 0f, deltaTime * 0.1f);
    }
    #endregion

    #region 3. Layer 1 -> Layer 0 Translation
    /// <summary>
    /// Converts the base material properties and current magical state into raw mechanical physics values.
    /// </summary>
    public virtual SimProperties GetSimProperties(MaterialState state, float baseSize, float baseGravityMultiplier)
    {
        SimProperties sim = new SimProperties();

        // --- 1. STATE FLAGS ---
        // Define simple thresholds so we can easily check the object's current elemental state
        bool isFrozen = state.Temperature < 0f;
        bool isMelting = state.Temperature > 100f; // Arbitrary high heat threshold
        bool isWet = state.Wetness > 0f;

        // --- 2. VOLUME & MASS ---
        float currentScale = baseSize * state.ScaleMultiplier;
        float volume = currentScale * currentScale * currentScale; // Roughly size^3
        float currentDensity = this.density * state.DensityMultiplier;

        // Mass = Density * Volume. (We add water weight if it is wet, scaling with volume so giant wet objects are very heavy)
        float waterWeight = state.Wetness * volume * 0.5f;
        sim.Mass = Mathf.Max(0.01f, (currentDensity * volume) + waterWeight);

        sim.Scale = currentScale;

        // --- 3. FRICTION ---
        sim.Friction = this.friction;
        sim.Friction -= state.Lubrication;      // Grease makes it slippery
        sim.Friction -= (state.Wetness * 0.2f); // Water makes it slightly slippery

        if (isFrozen && isWet)
        {
            // WET + FREEZING = ICE. Massive drop in friction regardless of base material.
            sim.Friction = 0.05f;
        }
        sim.Friction = Mathf.Max(0f, sim.Friction); // Cannot have negative friction

        // --- 4. RESTITUTION (BOUNCE) ---
        sim.Restitution = this.elasticity;
        sim.Restitution += state.Rubberization; // Rubber spells make it bouncy

        if (isFrozen)
        {
            // Frozen things thud, they don't bounce well
            sim.Restitution *= 0.2f;
        }
        sim.Restitution = Mathf.Clamp01(sim.Restitution); // Keep between 0 and 1

        // --- 5. HARDNESS & BRITTLENESS (Combat / Bonk Stats) ---
        sim.Hardness = this.hardness;
        sim.Brittleness = this.brittleness;

        if (isFrozen)
        {
            sim.Hardness += 2f;    // Freezing makes it harder
            sim.Brittleness += 3f; // But also shatters much easier
        }
        else if (isMelting)
        {
            sim.Hardness *= 0.5f;   // Heat softens objects
            sim.Brittleness *= 0.1f; // Melting things don't shatter, they squish
        }

        // Rubber spells protect the object from shattering on impact
        sim.Brittleness -= (state.Rubberization * 2f);
        sim.Brittleness = Mathf.Max(0f, sim.Brittleness);

        // --- 6. STICKINESS ---
        sim.Stickiness = this.stickiness;
        sim.Stickiness -= state.Lubrication; // Grease ruins stickiness

        if (isMelting)
        {
            sim.Stickiness += 0.3f; // Partially melted objects get gummy/sticky
        }

        // Static Cling: High electrical charge creates a slight magnetic/static pull
        sim.Stickiness += (state.Charge * 0.05f);
        sim.Stickiness = Mathf.Clamp01(sim.Stickiness);

        // --- 7. GRAVITY & DAMPING ---
        sim.GravityMultiplier = baseGravityMultiplier * state.GravityMultiplier;

        // Damping (Drag). Larger scale objects experience slightly more air resistance.
        sim.LinearDamping = this.hardness * 0.5f + (currentScale * 0.1f);
        sim.AngularDamping = sim.Mass * 0.05f;

        return sim;
    }
    #endregion

    #region ApplyVisuals
    public virtual void UpdateVisuals(PhysicsObject context, VisualStateData visualState, MaterialPropertyBlock mpb, Renderer[] renderers, float deltaTime)
    {
        // 1. Get the authoritative networked state
        MaterialState simState = context.physicsObjectProperties.CachedNetworkState.State;

        // 2. Smoothly interpolate the visual state (This data lives safely on the instance!)
        visualState.VisualTemperature = Mathf.Lerp(visualState.VisualTemperature, simState.Temperature, deltaTime * 10f);
        visualState.VisualWetness = Mathf.Lerp(visualState.VisualWetness, simState.Wetness, deltaTime * 10f);

        // 3. Inject into the MPB and apply to all renderers
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;

            renderers[i].GetPropertyBlock(mpb);

            mpb.SetFloat(TempID, visualState.VisualTemperature);
            mpb.SetFloat(WetID, visualState.VisualWetness);

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
    public float Restitution; // Bounce
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

    public float Rubberization;
    public float Lubrication;

    public float ScaleMultiplier;
    public float DensityMultiplier;
    public float GravityMultiplier;

    public void Reset()
    {
        Temperature = 0f;
        Wetness = 0f;
        Charge = 0f;
        Rubberization = 0f;
        Lubrication = 0f;
        ScaleMultiplier = 1f;
        DensityMultiplier = 1f;
        GravityMultiplier = 1f ;
    }
}

public class VisualStateData
{
    public float VisualTemperature;
    public float VisualWetness;
    public float VisualCharge;
}