using UnityEngine;
using Fusion;

public class TransformCoreMovement : NetworkBehaviour, IMovementHandler
{
    private PhysicsObject _po;

    [Header("Engine Limits")]
    [Tooltip("Maximum falling speed to prevent tunneling through floors.")]
    public float terminalVelocity = 80f;
    [Tooltip("Absolute speed limit in any direction to prevent raycast tunneling.")]
    public float maxOverallSpeed = 150f;

    [Networked] public Vector3 NetworkedVelocity { get; set; }

    private void Awake()
    {
        _po = GetComponent<PhysicsObject>();
    }

    public override void FixedUpdateNetwork()
    {
        if (NetworkedVelocity.y < -terminalVelocity)
        {
            NetworkedVelocity = new Vector3(NetworkedVelocity.x, -terminalVelocity, NetworkedVelocity.z);
        }

        if (NetworkedVelocity.sqrMagnitude > maxOverallSpeed * maxOverallSpeed)
        {
            NetworkedVelocity = NetworkedVelocity.normalized * maxOverallSpeed;
        }

        transform.position += NetworkedVelocity * Runner.DeltaTime;
    }

    public Vector3 CurrentVelocity => NetworkedVelocity;

    public void ApplyForce(Vector3 force, ForceMode forceMode = ForceMode.Force)
    {
        if (_po == null || _po.currentProperties.mass <= 0) return;

        float mass = _po.currentProperties.mass;

        switch (forceMode)
        {
            case ForceMode.Force:
                // Continuous force over time, scaled by mass (F = MA -> A = F/M)
                NetworkedVelocity += (force / mass) * Runner.DeltaTime;
                break;

            case ForceMode.Acceleration:
                // Continuous acceleration over time, ignoring mass
                NetworkedVelocity += force * Runner.DeltaTime;
                break;

            case ForceMode.Impulse:
                // Instant punch, scaled by mass
                NetworkedVelocity += force / mass;
                break;

            case ForceMode.VelocityChange:
                // Instant speed boost, ignoring mass
                NetworkedVelocity += force;
                break;
        }
    }

    public void ApplyImpulse(Vector3 force, Vector3 hitPosition = default)
    {
        if (_po != null && _po.currentProperties.mass > 0)
        {
            NetworkedVelocity += force / _po.currentProperties.mass;
        }
    }

    public void ApplyContinuousForce(Vector3 force)
    {
        if (_po != null && _po.currentProperties.mass > 0)
        {
            NetworkedVelocity += (force / _po.currentProperties.mass) * Runner.DeltaTime;
        }
    }

    public void SetVelocity(Vector3 newVelocity)
    {
        NetworkedVelocity = newVelocity;
    }

    public void Halt()
    {
        NetworkedVelocity = Vector3.zero;
    }
}
