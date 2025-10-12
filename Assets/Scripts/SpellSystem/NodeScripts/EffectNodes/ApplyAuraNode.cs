using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AuraEffect", menuName = "SpellNodes/Effect/Aura Effect")]
public class ApplyAuraNode : EffectNode
{
    [SerializeField] Aura[] auras;
    public override void Execute(List<SpellTriggerInfo> triggerInfo)
    {
        ApplyPromotableValues();

        foreach (var info in triggerInfo)
        {
            GameObject target = info.HitObject;

            if (target == null) continue;

            if(target.TryGetComponent<AuraContainer>(out AuraContainer ac))
            {
                for (int i = 0; i < auras.Length; i++)
                {
                    ac.AttachAura(auras[i]);
                }
            }
        }
    }
}
