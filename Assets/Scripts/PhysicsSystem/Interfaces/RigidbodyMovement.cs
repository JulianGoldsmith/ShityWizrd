using UnityEngine;
using Fusion;

[RequireComponent(typeof(Rigidbody))]
public class RigidbodyMovement : NetworkBehaviour, IMovementHandler
{
    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    public Vector3 CurrentVelocity => _rb.linearVelocity;

    public void ApplyImpulse(Vector3 force, Vector3 hitPosition = default)
    {
        if (hitPosition == default)
            _rb.AddForce(force, ForceMode.Impulse);
        else
            _rb.AddForceAtPosition(force, hitPosition, ForceMode.Impulse);
    }

    public void ApplyContinuousForce(Vector3 force)
    {
        _rb.AddForce(force, ForceMode.Force);
    }

    

    public void SetVelocity(Vector3 newVelocity)
    {
        throw new System.NotImplementedException();
    }

    public void Halt()
    {
        throw new System.NotImplementedException();
    }

    public void ApplyForce(Vector3 force, ForceMode forceMode = ForceMode.Force)
    {
        throw new System.NotImplementedException();
    }
}
