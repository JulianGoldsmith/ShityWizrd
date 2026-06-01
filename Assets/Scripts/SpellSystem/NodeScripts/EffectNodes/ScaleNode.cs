using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ScaleNode", menuName = "SpellNodes/Effect/ScaleNode")]
public class ScaleNode : EffectNode
{
    [Header("Scale Settings")]
    [Tooltip("Amount to add/subtract. 125 = Max Size, -125 = Min Size")]
    public int scaleChangeAmount = 10;

 
    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        return new ScaleEffect()
        {
            ScaleChangeAmount = scaleChangeAmount
        };
    }

    // Assuming your graph passes the hit target to this method
    public override void Execute(List<SpellTriggerInfo> triggerInfos)
    {
        //Debug.Log($"PUSHPULL node Executed");

       /* foreach (var info in triggerInfos)
        {
            GameObject target = info.HitObject;


            if (target.TryGetComponent<PhysicsObject>(out PhysicsObject PO))
            {
                ApplyScaleChange(PO);
            }
            else if (target.TryGetComponent<PhysicsSubObject>(out PhysicsSubObject PSO))
            {
                //ApplyScaleChange(PSO); -- need to think how this will work
            }
        }*/
    }

    
}

public class ScaleEffect : IEffect
{
    public int ScaleChangeAmount;

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
                    DurationInTicks = 0, // 0 = permanent until cleansed
                    Magnitude = ScaleChangeAmount / 100f, // Convert to a multiplier
                    TargetId = core.Object != null ? core.Object.Id : default
                };

                // 3. Send it to the engine! 
                // (Assuming '1' is the ID for your Scale/Grow effect in your registry)
                effectManager.AddEffect(1, payload);
            }
        }
    }
}