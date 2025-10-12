using System;
using UnityEngine;

[CreateAssetMenu(fileName = "AntiGravity", menuName = "Auras/AntiGravity")]
public class AntiGravityAura : Aura
{
    public override void OnApply(AuraContainer container)
    {
        ModifyGravityOnPhysicsObjectAndChildren(container, false);
    }
    public override void OnTick(AuraContainer container)
    {

    }

    public override void OnExpire(AuraContainer container)
    {
        ModifyGravityOnPhysicsObjectAndChildren(container, true);
    }

    void ModifyGravityOnPhysicsObjectAndChildren(AuraContainer container, bool useGravity)
    {
        PhysicsObject po = container.GetComponent<PhysicsObject>();
        if (po == null)
            return;

        Action<GameObject> action = obj => ModifyGravity(obj, useGravity);
        po.ApplyToSelfAndAllSubObjects(action);
    }
    void ModifyGravity(GameObject obj, bool useGravity)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) return;
        rb.useGravity = useGravity;
    }
}
