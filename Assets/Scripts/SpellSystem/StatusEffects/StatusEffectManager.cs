using Fusion;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NetworkedMemoryAllocator))]
[RequireComponent(typeof(PhysicsObject))]
public class StatusEffectManager : NetworkBehaviour
{
    const int CAPACITY = 32;
    [Networked, Capacity(CAPACITY)] public NetworkArray<ActiveStatusEffectData> ActiveEffects { get; }

    private NetworkedMemoryAllocator _memory;

    // We don't even need the _physicsObject reference here anymore!

    public bool debugEffectNames = true;
    public List<string> activeEffectNames = new List<string>();

    private byte[] _channelPresenceThisTick = new byte[CAPACITY];

    private bool _isSpawned = false;

    public override void Spawned()
    {
        _memory = GetComponent<NetworkedMemoryAllocator>();
        _isSpawned= true;
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

    public void BeginTick()
    {
       // System.Array.Clear(_channelPresenceThisTick, 0, CAPACITY);
    }

    public void AddEffect(byte effectId, ProposedEffectPayload payload)
    {
        if (!_isSpawned) return;
        IStatusEffect processor = StatusEffectRegistry.GetStatusEffect(effectId);
        if (processor == null) return;

        // STEP 1: Stacking Check
        for (int i = 0; i < ActiveEffects.Length; i++)
        {
            ActiveStatusEffectData existing = ActiveEffects.Get(i);

            if (existing.EffectID == effectId)
            {
                // CHANGE: Use payload.EffectType instead of processor.EffectType
                if (payload.EffectType == EffectLifecycle.Channeled)
                {
                    _channelPresenceThisTick[i]++;
                    return;
                }
                else if (processor.TryStack(Runner, ref existing, _memory, payload))
                {
                    ActiveEffects.Set(i, existing);
                    return;
                }
            }
        }

        // STEP 2: Find an empty slot
        int emptySlot = -1;
        for (int i = 0; i < ActiveEffects.Length; i++)
        {
            if (ActiveEffects.Get(i).EffectID == 0)
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
            EffectType = payload.EffectType,
            StartTick = Runner.Tick,
             // CHANGE: Save the type permanently into network memory!
            EndTick = payload.DurationInTicks > 0 ? Runner.Tick + payload.DurationInTicks : 0,
            PresenceCount = 0
        };

        processor.OnAllocated(_memory, ref newEffect, payload);

        // If it's a duration effect, it changes the math instantly, so force checkpoint
        if (payload.EffectType == EffectLifecycle.Duration)
        {
            GetComponent<PhysicsObject>().physicsObjectProperties.ForceCheckpoint();
        }
        else
        {
            _channelPresenceThisTick[emptySlot] = 1;
        }

  
        ActiveEffects.Set(emptySlot, newEffect);
    }

    // --- REPLACES FixedUpdateNetwork ---
    // The master PhysicsObject calls this to tell the manager to take out the trash.
    public void CleanUpExpiredEffects(int currentTick)
    {
        bool historyChanged = false;

        for (int i = 0; i < ActiveEffects.Length; i++)
        {
            ActiveStatusEffectData effect = ActiveEffects.Get(i);
            if (effect.EffectID == 0) continue;

            IStatusEffect processor = StatusEffectRegistry.GetStatusEffect(effect.EffectID);
            if (processor == null) continue;

            if (effect.EffectType == EffectLifecycle.Channeled)
            {
                if (effect.PresenceCount != _channelPresenceThisTick[i])
                {
                    effect.PresenceCount = _channelPresenceThisTick[i];
                    ActiveEffects.Set(i, effect);
                    historyChanged = true;
                }
            }

            // --- EXPIRY CHECK ---
            if (effect.IsExpired(currentTick))
            {
                RemoveEffect(i, processor, effect);
                historyChanged = true;
            }
        }

        if (historyChanged)
        {
            GetComponent<PhysicsObject>().physicsObjectProperties.ForceCheckpoint();
        }

       
    }

    public void ClearPersistanceCache() {
        for (int i = 0; i < ActiveEffects.Length; i++)
        {
            _channelPresenceThisTick[i] = 0;
        }
    }

    private void RemoveEffect(int arrayIndex, IStatusEffect statusEffect, ActiveStatusEffectData effectData)
    {
        statusEffect.OnRemoved(_memory, ref effectData);
        ActiveEffects.Set(arrayIndex, default);
        _channelPresenceThisTick[arrayIndex] = 0; // Clear the mitt just in case
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _isSpawned = false;
    }
}