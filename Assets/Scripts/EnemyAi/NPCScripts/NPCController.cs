using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;
using static HybridCharacterController;

public class NPCController : NetworkBehaviour
{
    [Header("Components")]
    public Rigidbody coreRB;
 
    [Header("Grounded Settings")]
    public float groundCheckDistance = 1.0f;
    public LayerMask groundLayer;
    public float suspensionCastRadius = 0.25f;
    [Networked] public NetworkBool IsGrounded { get; set; }

    public float rideHeight = 0.8f;
    public float rideSpringStrength = 100f;
    public float rideSpringDamper = 10f;

    [Header("Upright Settings")]
    public float uprightSpringStrength = 50f;
    public float uprightSpringDamper = 5f;

    [Header("Movement Settings")]
    public float maxWalkSpeed = 3f, maxSprintSpeed = 5f;
    public float acceleration = 20f;
    public float braking = 20f;
    public float jumpForce = 50f;

    [Header("Network Pos")]
    [SerializeField] public Transform networkedRenderRoot;

    [Header("PD armature")]
    public List<PDSpring> pDSprings = new List<PDSpring>();

    [Header("Animation")]
    public Animator targetAnimator, finalAnimator;
    public Vector3 hipsOffset;


    // --- BT INTERFACE ---
    [Networked] public Vector3 NetworkedMoveDirection { get; set; }
    [Networked] public NetworkBool NetworkedWantsToSprint { get; set; }
    [Networked] public NetworkBool NetworkedWantsToJump { get; set; }

    public override void FixedUpdateNetwork()
    {
        if (coreRB == null) return;

        ApplyCoreSuspention();

        ApplyUprightStabilization();

        UpdateCoreMovement();

        UpdatePDDrives();
    }

    public override void Render()
    {
        UpdateAnimators();
    }



    private void ApplyCoreSuspention()
    {

        if (Physics.SphereCast(coreRB.position, suspensionCastRadius, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            IsGrounded = true;

            float compression = rideHeight - hit.distance;

            float springForce = rideSpringStrength * compression;

            float verticalVelocity = Vector3.Dot(coreRB.linearVelocity, Vector3.up);

            float damperForce = rideSpringDamper * verticalVelocity;

            Vector3 suspensionForce = Vector3.up * (springForce - damperForce);

            coreRB.AddForce(suspensionForce, ForceMode.Acceleration);
        }
        else
        {
            IsGrounded = false;
        }
    }

    private void ApplyUprightStabilization()
    {

        Quaternion targetUpRotation = Quaternion.FromToRotation(coreRB.transform.up, Vector3.up);

        targetUpRotation.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f) angle -= 360f;

        Vector3 springTorque = axis.normalized * (angle * Mathf.Deg2Rad * uprightSpringStrength);

        Vector3 damperTorque = -coreRB.angularVelocity * uprightSpringDamper;

        coreRB.AddTorque(springTorque + damperTorque, ForceMode.Acceleration);
    }


    private void UpdateCoreMovement()
    {
        if (!IsGrounded)
        {

            return;
        }

        if (NetworkedWantsToJump)
        {
            coreRB.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            NetworkedWantsToJump = false;
        }

        float targetSpeed = NetworkedWantsToSprint ? maxSprintSpeed : maxWalkSpeed;
        Vector3 targetVelocity = NetworkedMoveDirection * targetSpeed;

        Vector3 currentHorizontalVelocity = new Vector3(coreRB.linearVelocity.x, 0, coreRB.linearVelocity.z);

        Vector3 velocityError = targetVelocity - currentHorizontalVelocity;

        Vector3 force;
        if (targetVelocity.magnitude > 0.01f)
        {

            force = velocityError * acceleration;
        }
        else
        {

            force = velocityError * braking;

        }
        
        coreRB.AddForce(force, ForceMode.Acceleration);
    }


    private void UpdatePDDrives()
    {
        for (int i = 0; i < pDSprings.Count; i++)
        {
            var springs = pDSprings[i];
            springs.UpdateBoneDrive();
        }
    }

    public void UpdateAnimators()
    {
        if (!targetAnimator) return;

        var targetPos = networkedRenderRoot.position + hipsOffset;
        var targetRot = networkedRenderRoot.rotation;

        targetAnimator.gameObject.transform.position = targetPos;
        targetAnimator.gameObject.transform.rotation = targetRot;
        finalAnimator.gameObject.transform.position = targetPos;
        finalAnimator.gameObject.transform.rotation = targetRot;
    }
}