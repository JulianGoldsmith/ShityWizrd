using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AuraEffect", menuName = "SpellNodes/Effect/Aura Effect")]
public class ApplyAuraNode : EffectNode
{
    [SerializeField] Aura[] auras;

    public override IEffect CompileEffect()
    {
        throw new System.NotImplementedException();
    }

    public override void Execute(List<SpellTriggerInfo> triggerInfo)
    {
        foreach (var info in triggerInfo)
        {
            GameObject target = info.HitObject;

            if (target == null) continue;

            if (target.TryGetComponent<AuraContainer>(out AuraContainer ac))
            {
                AttachAurasTo(ac);
            }
            else if (target.TryGetComponent<PhysicsSubObject>(out PhysicsSubObject pso))
            {
                // Didn't hit the object, but hit a subobject of it.
                PhysicsObject po = pso.parent_physics_object;
                if (po != null && po.TryGetComponent<AuraContainer>(out AuraContainer ac_parent))
                {
                    AttachAurasTo(ac_parent);
                }
            }
        }
    }

    void AttachAurasTo(AuraContainer ac)
    {
        if (ac.Object == null || !ac.Object.IsValid)
            return;

        for (int i = 0; i < auras.Length; i++)
        {
            //Debug.Log($"Found AC {ac.name} -> attach aura {auras[i].unique_label}");
            ac.AttachAura(auras[i]);
        }
    }
}
