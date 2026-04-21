using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ScaleNode", menuName = "SpellNodes/Effect/ScaleNode")]
public class ScaleNode : EffectNode
{
    [Header("Scale Settings")]
    [Tooltip("Amount to add/subtract. 125 = Max Size, -125 = Min Size")]
    public int scaleChangeAmount = 10;

    public override IEffect CompileEffect()
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
            // Struct validation check
            if (!info.IsValid || info.HitObject == null) continue;

            GameObject target = info.HitObject;

            if (target.TryGetComponent<PhysicsObject>(out PhysicsObject PO))
            {
                ApplyScaleChange(PO);
            }
            else if (target.TryGetComponent<PhysicsSubObject>(out PhysicsSubObject PSO))
            {
                if (PSO.parent_physics_object != null)
                {
                    //ApplyScaleChange(PSO.parent_physics_object);
                }
            }
        }
    }

    private void ApplyScaleChange(PhysicsObject po)
    {
        SpellEffectStates currentState = po.SpellEffectState;

        float percentMultiplier = (100f + ScaleChangeAmount) / 100f;

        float currentVisualSize = 1f + (currentState.Scale / 125f);

        float newVisualSize = currentVisualSize * percentMultiplier;

        float calculatedScale = (newVisualSize - 1f) * 125f;

        currentState.Scale = (sbyte)Mathf.Clamp(Mathf.RoundToInt(calculatedScale), -125, 125);

        po.SpellEffectState = currentState;
    }
}