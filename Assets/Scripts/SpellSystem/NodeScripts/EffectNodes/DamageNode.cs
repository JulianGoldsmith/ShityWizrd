using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DamageEffect", menuName = "SpellNodes/Effect/Damage Effect")]
public class DamageEffectNode : EffectNode
{
    [Promotable("Damage Amount", DataTypeTag.Damage)]
    public int damageAmount = 10;

    public override void Execute(List<SpellTriggerInfo> triggerInfos)
    {
        ApplyPromotableValues();

        foreach (var info in triggerInfos)
        {
            GameObject target = info.HitObject;

            if (target == null) continue;

            Health healthComponent = target.GetComponent<Health>();

            if (healthComponent != null)
            {
                healthComponent.TakeDamage(this.damageAmount);
            }
        }
    }
}