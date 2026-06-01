using Fusion;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GrowEffectNode", menuName = "SpellNodes/Effect/GrowEffectNode")]
public class GrowEffectNode : EffectNode
{
    [Header("Grow Settings")]
    [Tooltip("How much the scale multiplier increases per tick (e.g., 0.01 = 1% growth per tick)")]
    [Promotable("Growth Rate", DataTypeTag.Generic)]
    public float growthRate = 0.01f;

    [Tooltip("How many ticks the growth spell lasts. 0 = Infinite.")]
    [Promotable("Duration Ticks", DataTypeTag.Generic)]
    public int durationTicks = 300; 

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        return new GrowRuntimeEffect()
        {
            GrowthRate = growthRate,
            DurationTicks = durationTicks
        };
    }

 

    public override void Execute(List<SpellTriggerInfo> triggerInfo)
    {
        // Usually blank. Graph execution logic happens in the compiled IEffect.
    }
}

// The stateless runtime logic that actually executes when the spell hits something
public class GrowRuntimeEffect : IEffect
{
    public float GrowthRate;
    public int DurationTicks;

    private const byte GROW_EFFECT_ID = 1;

    public void Execute(SpellCreatedCore core, List<SpellTriggerInfo> hitInfos)
    {
        foreach (var info in hitInfos)
        {
            if (!info.IsValid || info.HitObject == null) continue;

            GameObject target = info.HitObject;

            if (target.TryGetComponent<StatusEffectManager>(out var effectManager))
            {
                ProposedEffectPayload payload = new ProposedEffectPayload
                {
                    DurationInTicks = DurationTicks,
                    Magnitude = GrowthRate,
                    EffectType = EffectLifecycle.Duration,
                    TargetId = core.Object.Id 
                };

                effectManager.AddEffect(GROW_EFFECT_ID, payload);
            }
            else if (target.TryGetComponent<PhysicsSubObject>(out PhysicsSubObject pso))
            {
                if (pso.parent_physics_object != null && pso.parent_physics_object.TryGetComponent<StatusEffectManager>(out var parentManager))
                {
                    ProposedEffectPayload payload = new ProposedEffectPayload
                    {
                        DurationInTicks = DurationTicks,
                        Magnitude = GrowthRate,
                        EffectType = EffectLifecycle.Duration,
                        TargetId = core.Object.Id
                    };

                    parentManager.AddEffect(GROW_EFFECT_ID, payload);
                }
            }
        }
    }
}

public class GrowStatusEffect : IStatusEffect
{
    private const int FLOATS_NEEDED = 1;
    private const int SLOT_GROWTH_RATE = 0;


    public void OnAllocated(NetworkedMemoryAllocator memory, ref ActiveStatusEffectData newEffectData, ProposedEffectPayload payload)
    {
        if (memory.TryClaimFloats(FLOATS_NEEDED, out byte startIndex))
        {
            newEffectData.FloatOffset = startIndex;
            newEffectData.SetFloat(SLOT_GROWTH_RATE, payload.Magnitude, memory);
        }
    }

    public bool TryStack(NetworkRunner runner, ref ActiveStatusEffectData existingEffect, NetworkedMemoryAllocator memory, ProposedEffectPayload newPayload)
    {
        if (newPayload.EffectType == EffectLifecycle.Channeled) return true;

        float currentRate = existingEffect.GetFloat(SLOT_GROWTH_RATE, memory);
        existingEffect.SetFloat(SLOT_GROWTH_RATE, currentRate + newPayload.Magnitude, memory);

        if (newPayload.DurationInTicks > 0)
        {
            int proposedEndTick = runner.Tick + newPayload.DurationInTicks;
            existingEffect.EndTick = Mathf.Max(existingEffect.EndTick, proposedEndTick);
        }
        return true;
    }

    public void Tick(int simTick, PhysicsObject target, NetworkedMemoryAllocator memory, ref ActiveStatusEffectData effectData, ref MaterialState currentState, PhysicsObjectMaterial mat)
    {
        float growthPerSecond = effectData.GetFloat(SLOT_GROWTH_RATE, memory);
        float growthThisTick = growthPerSecond * target.Runner.DeltaTime;
        
        // Multiply by the official NETWORKED presence count
        if (effectData.EffectType == EffectLifecycle.Channeled)
        {
            growthThisTick *= effectData.PresenceCount;
        }

        mat.MutateScale(ref currentState, MutationType.Add, growthThisTick);

        if (currentState.ScaleMultiplier > 10f) currentState.ScaleMultiplier = 10f;
    }

    public void OnRemoved(NetworkedMemoryAllocator memory, ref ActiveStatusEffectData effectData)
    {
        memory.FreeFloats(effectData.FloatOffset, FLOATS_NEEDED);
    }
}