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
        throw new System.NotImplementedException();
    }

    // Assuming your graph passes the hit target to this method
    public override void Execute(List<SpellTriggerInfo> triggerInfos)
    {
        //Debug.Log($"PUSHPULL node Executed");

        foreach (var info in triggerInfos)
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
        }
    }

    private void ApplyScaleChange(PhysicsObject po)
    {
        SpellEffectStates currentState = po.SpellEffectState;

        float percentMultiplier = (100f + scaleChangeAmount) / 100f;

        float currentVisualSize = 1f + (currentState.Scale / 125f);

        float newVisualSize = currentVisualSize * percentMultiplier;

        float calculatedScale = (newVisualSize - 1f) * 125f;

        currentState.Scale = (sbyte)Mathf.Clamp(Mathf.RoundToInt(calculatedScale), -125, 125);

        po.SpellEffectState = currentState;
    }

}
