using UnityEngine;
using System;
using Fusion;

[RequireComponent(typeof(Collider))]
public class RigidbodyCollision : NetworkBehaviour, ICollisionDetector
{
    public event Action<UniversalCollisionData> OnImpactDetected;

    private void OnCollisionEnter(Collision collision)
    {
        // Filter out micro-collisions just like your old setup
        if (collision.impulse.magnitude > 0.01f)
        {
            var impactData = new UniversalCollisionData
            {
                HitObject = collision.gameObject,
                Point = collision.GetContact(0).point,
                Normal = collision.GetContact(0).normal,
                ImpulseMagnitude = collision.impulse.magnitude
            };

            OnImpactDetected?.Invoke(impactData);
        }
    }
}