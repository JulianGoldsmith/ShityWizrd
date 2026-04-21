using Fusion;
using UnityEngine;
using System.Collections.Generic;


[DefaultExecutionOrder(-50)] //call the ticks on the status effects before running physics
[RequireComponent(typeof(NetworkedMemoryAllocator))]
[RequireComponent(typeof(PhysicsObject))]
public class StatusEffectManager : NetworkBehaviour
{
    const int CAPACITY = 16;
    [Networked, Capacity(CAPACITY)] public NetworkArray<ActiveStatusEffectData> ActiveEffects { get; }

    private NetworkedMemoryAllocator _memory;
    private PhysicsObject _physicsObject;

    public bool debugEffectNames = true;
    public List<string> activeEffectNames = new List<string>();

    public override void Spawned()
    {
        _memory = GetComponent<NetworkedMemoryAllocator>();
        _physicsObject = GetComponent<PhysicsObject>();
    }

    public override void Render()
    {
        base.Render();
        activeEffectNames.Clear();

        if (debugEffectNames)
        {
            for (int i = 0; i < ActiveEffects.Length; i++)
            {
                ActiveStatusEffectData effect = ActiveEffects.Get(i);

                if (effect.EffectID != 0)
                {
                    IStatusEffect processor = StatusEffectRegistry.GetStatusEffect(effect.EffectID);

                    if (processor != null)
                    {
                        activeEffectNames.Add(processor.GetType().Name);
                    }
                    else
                    {
                        activeEffectNames.Add($"Unknown (ID: {effect.EffectID})");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Called by Spell Cores (OverlapSphere, Hitbox, etc.)
    /// </summary>
    public void AddEffect(byte effectId, ProposedEffectPayload payload)
    {
        IStatusEffect processor = StatusEffectRegistry.GetStatusEffect(effectId);
        if (processor == null) return;

        // STEP 1: Stacking Check
        for (int i = 0; i < ActiveEffects.Length; i++)
        {
            ActiveStatusEffectData existing = ActiveEffects.Get(i);

            // Is this the same type of spell?
            if (existing.EffectID == effectId)
            {
                // Ask the logic if it wants to absorb the new hit
                if (processor.TryStack(Runner, ref existing, _memory, payload))
                {
                    // It absorbed it! (e.g., refreshed duration or added scale).
                    // Save the modified struct back to the network array and we are done!
                    ActiveEffects.Set(i, existing);
                    return;
                }
            }
        }

        // STEP 2: Find an empty slot for a new effect
        int emptySlot = -1;
        for (int i = 0; i < ActiveEffects.Length; i++)
        {
            if (ActiveEffects.Get(i).EffectID == 0) // 0 means empty/NULL
            {
                emptySlot = i;
                break;
            }
        }

        if (emptySlot == -1)
        {
            Debug.LogWarning("StatusEffectManager is full! Rejecting new effect.");
            return;
        }

        // STEP 3: Create the base struct
        ActiveStatusEffectData newEffect = new ActiveStatusEffectData
        {
            EffectID = effectId,
            StartTick = Runner.Tick,
            EndTick = payload.DurationInTicks > 0 ? Runner.Tick + payload.DurationInTicks : 0
        };

        // STEP 4: Let the processor claim its memory and write the payload data
        processor.OnAllocated(_memory, ref newEffect, payload);

        // STEP 5: Save it to the network
        ActiveEffects.Set(emptySlot, newEffect);
    }


    // --- EXECUTION LOGIC ---

    public override void FixedUpdateNetwork()
    {
        StatusEffectPropertyModifiers  effectPropertyModifires = new StatusEffectPropertyModifiers();
        effectPropertyModifires.Reset();

        for (int i = 0; i < ActiveEffects.Length; i++)
        {
            ActiveStatusEffectData effect = ActiveEffects.Get(i);

            if (effect.EffectID == 0) continue; // Skip empty slots

            IStatusEffect statusEffect = StatusEffectRegistry.GetStatusEffect(effect.EffectID);
            if (statusEffect == null) continue;

            // STEP 1: Check Expiration
            if (effect.IsExpired(Runner.Tick))
            {
                RemoveEffect(i, statusEffect, effect);
                continue;
            }

            // STEP 2: Execute the continuous logic
            statusEffect.Tick(Runner, _physicsObject, _memory, ref effect, ref effectPropertyModifires);

            // STEP 3: Save back to the array. 
            // (We pass 'ref effect' to Execute, so if the processor decided to 
            // artificially end the effect by changing EndTick, we must sync it).
            ActiveEffects.Set(i, effect);
        }


        _physicsObject.ApplyStatusEffectModifiers(effectPropertyModifires);

    }


    // --- CLEANUP LOGIC ---

    private void RemoveEffect(int arrayIndex, IStatusEffect statusEffect, ActiveStatusEffectData effectData)
    {
        // 1. Let the processor free the exact floats/ints it claimed
        statusEffect.OnRemoved(_memory, ref effectData);

        // 2. Wipe the slot completely clean
        ActiveEffects.Set(arrayIndex, default);
    }
}

public struct StatusEffectPropertyModifiers
{
    // --- 1. ELEMENTAL ACCUMULATORS ---
    public float TemperatureDelta;
    public float MoistureDelta;
    public float ChargeDelta;

    // --- 2. TRANSMUTATION ---
    public byte MaterialOverride; // 0 = default, >0 maps to PHYSICS_OBJECT_MATERIAL

    // --- 3. MULTIPLIERS (Default to 1) ---
    public float ScaleMultiplier;
    public float GravityMultiplier;
    public float DensityMultiplier;
    public float FrictionMultiplier;

    // --- 4. ADDITIVES (Default to 0) ---
    public float ElasticityAdditive;
    public float BrittlenessAdditive;
    public float StickinessAdditive;
    public float HardnessAdditive;

    // Must be called at the start of every tick before passing to effects!
    public void Reset()
    {
        TemperatureDelta = 0f;
        MoistureDelta = 0f;
        ChargeDelta = 0f;

        MaterialOverride = 0;

        ScaleMultiplier = 1f;
        GravityMultiplier = 1f;
        DensityMultiplier = 1f;
        FrictionMultiplier = 1f;

        ElasticityAdditive = 0f;
        BrittlenessAdditive = 0f;
        StickinessAdditive = 0f;
        HardnessAdditive = 0f;
    }
}