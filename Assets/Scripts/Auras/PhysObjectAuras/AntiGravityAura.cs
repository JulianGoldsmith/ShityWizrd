using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AntiGravity", menuName = "Auras/AntiGravity")]
public class AntiGravityAura : Aura
{
    // this is a global instance, so must track all simultaneously.
    public AnimationCurve drag_force_curve = new AnimationCurve();
    const float drag_force_scaling_factor = 15.0f;
    const float drag_force_speed_scaling_factor = 6.0f;
    public override void OnApply(AuraContainer container)
    {
        //Debug.Log($"OnApply aura by {name}");
        ModifyGravityOnPhysicsObjectAndChildren(container, false);
    }
    public override void OnTick(AuraContainer container)
    {
        PhysicsObject po = container.GetComponent<PhysicsObject>();
        if (po == null)
        {
            ApplyDragForceOnTick(container.gameObject);                
            return;
        }

        Action<GameObject> action = obj => ApplyDragForceOnTick(obj);
        po.ApplyToSelfAndAllSubObjects(action);
    }

    public override void OnExpire(AuraContainer container)
    {
        ModifyGravityOnPhysicsObjectAndChildren(container, true);
    }

    void ModifyGravityOnPhysicsObjectAndChildren(AuraContainer container, bool useGravity)
    {
        PhysicsObject po = container.GetComponent<PhysicsObject>();
        if (po == null)
        {
            ModifyGravity(container.gameObject, useGravity);
            return;
        }

        Action<GameObject> action = obj => ModifyGravity(obj, useGravity);
        po.ApplyToSelfAndAllSubObjects(action);
    }
    void ModifyGravity(GameObject obj, bool useGravity)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) return;
        //Debug.Log($"Setting gravity to within aura on {obj.name} by {name}");
        rb.useGravity = useGravity;
    }
    void ApplyDragForceOnTick(GameObject obj)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) return;
        float drag_force = drag_force_curve.Evaluate(rb.linearVelocity.magnitude / drag_force_speed_scaling_factor);
        if (drag_force > 0)
            rb.AddForce(-rb.linearVelocity * drag_force * drag_force_scaling_factor, ForceMode.Acceleration);
    }
}
