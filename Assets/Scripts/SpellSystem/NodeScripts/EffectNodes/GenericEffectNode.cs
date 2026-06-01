using Fusion;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GenericEffectNode", menuName = "SpellNodes/Effect/Generic Effect Node")]
public class GenericEffectNode : EffectNode
{
    [Header("Network Identity")]
    [Tooltip("This is assigned automatically when published to the Master Dictionary. Do not edit manually.")]
    public int NetworkEffectID = -1;

    [Header("Execution Settings")]
    public EffectLifecycle Lifecycle = EffectLifecycle.Duration;

    [Tooltip("Used only if Lifecycle is Duration. (0 = Infinite)")]
    public int DurationTicks = 300;

    [Header("Layer 1 Mutations")]
    public List<EffectComponent> Components = new List<EffectComponent>();

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {

        return new GenericRuntimeEffect()
        {
            NetworkEffectID = this.NetworkEffectID,
            LifeCycle = this.Lifecycle,
            DurationTicks = this.DurationTicks,
            Components = new System.Collections.Generic.List<EffectComponent>(this.Components)
        };
    }

    public override void Execute(List<SpellTriggerInfo> triggerInfo)
    {
    }

    public override List<SocketDefinition> GetSockets()
    {
        var sockets = base.GetSockets();

        for (int i = 0; i < Components.Count; i++)
        {
            EffectComponent comp = Components[i];

            sockets.Add(new SocketDefinition(
                name: $"{comp.Target} Mag In", // e.g., "Temperature Mag In"
                type: SocketType.Data,
                direction: SocketDirection.Input,
                tag: DataTypeTag.Generic,
                dataType: typeof(float),
                owningNodeGUID: this.InstanceGuid
  
            ));
        }

        return sockets;
    }
}

public class GenericRuntimeEffect : IEffect
{
    public int NetworkEffectID;
    public EffectLifecycle LifeCycle;
    public int DurationTicks;
    public List<EffectComponent> Components;

    public void Execute(SpellCreatedCore core, List<SpellTriggerInfo> hitInfos)
    {
        foreach (var info in hitInfos)
        {
            if (!info.IsValid || info.HitObject == null) continue;

            GameObject target = info.HitObject;
            StatusEffectManager targetManager = null;

            // Safely find the manager (handling SubObjects perfectly)
            if (target.TryGetComponent<StatusEffectManager>(out var effectManager))
            {
                targetManager = effectManager;
            }
            else if (target.TryGetComponent<PhysicsSubObject>(out PhysicsSubObject pso) && pso.parent_physics_object != null)
            {
                //pso.parent_physics_object.TryGetComponent<StatusEffectManager>(out targetManager);
            }

            if (targetManager == null) continue;

            if (LifeCycle == EffectLifecycle.Instant)
            {
                if (targetManager.TryGetComponent<PhysicsObjectProperties>(out var props))
                {
                    ApplyInstantMutations(props);
                }
            }
            else
            {
                ProposedEffectPayload payload = new ProposedEffectPayload
                {
                    DurationInTicks = DurationTicks,
                    EffectType = LifeCycle,
                    Magnitude = 0, 
                    TargetId = core.Object.Id
                };

                targetManager.AddEffect((byte)NetworkEffectID, payload);
            }
        }
    }

    private void ApplyInstantMutations(PhysicsObjectProperties props)
    {
        MaterialState state = props.CachedNetworkState.State;
        PhysicsObjectMaterial mat = props.physicsobjectmaterial;
        if (mat == null) return;

        foreach (var comp in Components)
        {
            ApplyMutation(mat, ref state, comp, comp.BaseMagnitude);
        }

        props.CachedNetworkState.State = state;
        props.ForceCheckpoint();
    }

    public static void ApplyMutation(PhysicsObjectMaterial mat, ref MaterialState state, EffectComponent comp, float magnitude)
    {
        switch (comp.Target)
        {
            case Layer1Target.Temperature: mat.MutateTemperature(ref state, comp.Mutation, magnitude); break;
            case Layer1Target.Wetness: mat.MutateWetness(ref state, comp.Mutation, magnitude); break;
            case Layer1Target.ScaleMultiplier: mat.MutateScale(ref state, comp.Mutation, magnitude); break;
            case Layer1Target.DensityMultiplier: mat.MutateDensity(ref state, comp.Mutation, magnitude); break;
        }
    }
}

public class GenericStatusEffect : IStatusEffect
{
    public void OnAllocated(NetworkedMemoryAllocator memory, ref ActiveStatusEffectData newEffectData, ProposedEffectPayload payload)
    {
        GenericEffectNode blueprint = MasterEffectDictionary.Instance.BakedEffects[newEffectData.EffectID];
        if (blueprint == null) return;

        int floatsNeeded = blueprint.Components.Count;
        if (memory.TryClaimFloats(floatsNeeded, out byte startIndex))
        {
            newEffectData.FloatOffset = startIndex;

            for (int i = 0; i < floatsNeeded; i++)
            {
                newEffectData.SetFloat(i, blueprint.Components[i].BaseMagnitude, memory);
            }
        }
    }

    public bool TryStack(NetworkRunner runner, ref ActiveStatusEffectData existingEffect, NetworkedMemoryAllocator memory, ProposedEffectPayload newPayload)
    {
        if (newPayload.DurationInTicks > 0)
        {
            int proposedEndTick = runner.Tick + newPayload.DurationInTicks;
            existingEffect.EndTick = Mathf.Max(existingEffect.EndTick, proposedEndTick);
        }
        return true;
    }

    public void Tick(int simTick, PhysicsObject target, NetworkedMemoryAllocator memory, ref ActiveStatusEffectData effectData, ref MaterialState currentState, PhysicsObjectMaterial mat)
    {
        GenericEffectNode blueprint = MasterEffectDictionary.Instance.BakedEffects[effectData.EffectID];
        if (blueprint == null) return;

        for (int i = 0; i < blueprint.Components.Count; i++)
        {
            EffectComponent comp = blueprint.Components[i];

            float networkedMagnitude = effectData.GetFloat(i, memory);

            float tickMagnitude = networkedMagnitude * target.Runner.DeltaTime;

            if (blueprint.Lifecycle == EffectLifecycle.Channeled)
            {
                tickMagnitude *= effectData.PresenceCount;
            }

            GenericRuntimeEffect.ApplyMutation(mat, ref currentState, comp, tickMagnitude);
        }
    }

    public void OnRemoved(NetworkedMemoryAllocator memory, ref ActiveStatusEffectData effectData)
    {
        GenericEffectNode blueprint = MasterEffectDictionary.Instance.BakedEffects[effectData.EffectID];
        if (blueprint != null)
        {
            memory.FreeFloats(effectData.FloatOffset, blueprint.Components.Count);
        }
    }
}

public enum EffectLifecycle
{
    Instant = 0,   
    Duration = 1,  
    Channeled = 2  
}

public enum Layer1Target
{
    Temperature,
    Wetness,
    Charge,
    Rubberization,
    Lubrication,
    ScaleMultiplier,
    DensityMultiplier,
    GravityMultiplier
}

[System.Serializable]
public class EffectComponent
{
    [Tooltip("The Layer 1 Material State property to modify.")]
    public Layer1Target Target;

    [Tooltip("How the magnitude is applied to the target property.")]
    public MutationType Mutation;

    [Tooltip("The base numerical value applied by this component.")]
    public float BaseMagnitude;

    [Tooltip("If true, the visual dictionary will attempt to spawn an auto-VFX for this specific component.")]
    public bool AutoVFX = true;

    public bool effectsPlayerObjects = false;
}