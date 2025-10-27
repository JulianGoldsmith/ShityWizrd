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

    }
    public override void OnTick(AuraContainer container)
    {
        PhysicsObject po = container.GetComponent<PhysicsObject>();
        if (po == null)
        {
            ApplyGravityAndDragForceOnTick(container.gameObject);                
            return;
        }

        Action<GameObject> action = obj => ApplyGravityAndDragForceOnTick(obj);
        po.ApplyToSelfAndAllSubObjects(action);
    }

    public override void OnExpire(AuraContainer container)
    {

    }

    void ApplyGravityAndDragForceOnTick(GameObject obj)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) return;
        // apply a force that contests the object's gravity (if it is affected by gravity)
        if (rb.useGravity)
            rb.AddForce(-Physics.gravity, ForceMode.Acceleration);

        float drag_force = drag_force_curve.Evaluate(rb.linearVelocity.magnitude / drag_force_speed_scaling_factor);
        if (drag_force > 0)
            rb.AddForce(-rb.linearVelocity * drag_force * drag_force_scaling_factor, ForceMode.Acceleration);
    }
}
