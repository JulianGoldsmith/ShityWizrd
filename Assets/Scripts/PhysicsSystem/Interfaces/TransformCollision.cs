using UnityEngine;
using System;
using Fusion;

public class TransformCollision : NetworkBehaviour, ICollisionDetector
{
    public event Action<UniversalCollisionData> OnImpactDetected;
    private TransformCoreMovement _mover;

    public float collisionRadius = 0.5f;
    public LayerMask collisionMask;

    private void Awake()
    {
        _mover = GetComponent<TransformCoreMovement>();
    }

    // ----------------------------------- this needs updating to be lag-compensated!!!! -------------------------
    private void OnTriggerEnter(Collider other)
    {
        // 1. Fake the Hit Point (Find the closest point on the enemy's collider to our center)
        Vector3 hitPoint = other.ClosestPoint(transform.position);

        // 2. Fake the Normal (A rough vector pointing from the enemy back to us)
        Vector3 fakeNormal = (transform.position - other.transform.position).normalized;

        // 3. Fake the Impulse (Using our current speed as a baseline)
        float speed = _mover != null ? _mover.CurrentVelocity.magnitude : 10f;

        var impactData = new UniversalCollisionData
        {
            HitObject = other.gameObject,
            Point = hitPoint,
            Normal = fakeNormal,
            ImpulseMagnitude = speed * 0.5f // Mathematical approximation
        };

        OnImpactDetected?.Invoke(impactData);
    }

    public override void FixedUpdateNetwork()
    {
        /*if (_mover == null || _mover.Velocity.sqrMagnitude < 0.01f) return;

        float distanceThisFrame = _mover.Velocity.magnitude * Runner.DeltaTime;
        var hitOptions = HitOptions.IncludePhysX | HitOptions.IgnoreInputAuthority;

        if (Runner.LagCompensation.SphereCast(transform.position, collisionRadius, _mover.Velocity.normalized, distanceThisFrame, Object.InputAuthority, out var hit, collisionMask, hitOptions))
        {
            var impactData = new UniversalCollisionData
            {
                HitObject = hit.GameObject,
                Point = hit.Point,
                Normal = hit.Normal,
                ImpulseMagnitude = _mover.Velocity.magnitude * 0.5f // Mathematical approximation of force
            };

            OnImpactDetected?.Invoke(impactData);
        }*/
    }
}