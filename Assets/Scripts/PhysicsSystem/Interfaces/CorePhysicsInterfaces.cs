using UnityEngine;

public struct UniversalCollisionData
{
    public GameObject HitObject;
    public Vector3 Point;
    public Vector3 Normal;
    public float ImpulseMagnitude;
}

public interface IMovementHandler
{
    Vector3 CurrentVelocity { get; }
    void ApplyImpulse(Vector3 force, Vector3 hitPosition = default);
    void ApplyForce(Vector3 force, ForceMode forceMode = ForceMode.Force);
    void ApplyContinuousForce(Vector3 force);
    void SetVelocity(Vector3 newVelocity);
    void Halt();
}

public interface ICollisionDetector
{
    event System.Action<UniversalCollisionData> OnImpactDetected;
}

