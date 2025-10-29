using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem; 
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
    public Transform smoothedNetworkRoot;

    [Header("PD Bones")]
    public List<PdBone> pdBones = new List<PdBone>();
    private float pdDesignDt = 1f / 64f;

    public List<NetworkRigidbody3D> rbComponents = new List<NetworkRigidbody3D>();

    [Header("Animation")]
    public Animator targetAnimator;// finalAnimator;
    public AnimationStateController animStateController;
    public Vector3 hipsOffset;


    // --- BT INTERFACE ---
    [Networked] public Vector3 NetworkedMoveDirection { get; set; }
    [Networked] public NetworkBool NetworkedWantsToSprint { get; set; }
    [Networked] public NetworkBool NetworkedWantsToJump { get; set; }

    public override void Spawned()
    {
        //for (int i = 0; i < pDSprings.Count; i++)
        //{
        //    var spring = pDSprings[i];
        //    spring.Init(coreRB.transform, HasInputAuthority);
        //}

        Runner.SetIsSimulated(this.Object, true);
        foreach (NetworkRigidbody3D nrb in rbComponents)
        {
            Runner.SetIsSimulated(nrb.Object, true);
        }
        animStateController = GetComponent<AnimationStateController>();
    }

    public override void FixedUpdateNetwork()
    {
        //if(!HasInputAuthority && !HasStateAuthority)
        //{
        //    Debug.Log("This is running on a proxy");
        //}

        if (coreRB == null) return;

        if (HasStateAuthority)
        {
            Vector2 input = Vector2.zero;
            if (Keyboard.current.iKey.isPressed)
            {
                input= Vector2.up;
            }
            if (Keyboard.current.kKey.isPressed)
            {
                input = Vector2.down;
            }
            if (Keyboard.current.jKey.isPressed)
            {
                input = Vector2.right;
            }
            if (Keyboard.current.lKey.isPressed)
            {
                input = Vector2.left;
            }
            animStateController.SetTargetMovement(input);
        }

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

        if (Physics.SphereCast(coreRB.position + Vector3.up*0.2f, suspensionCastRadius, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            IsGrounded = true;

            float compression = rideHeight - (coreRB.transform.position - hit.point).magnitude;

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

            Vector3 springTorque = axis.normalized * (angle * Mathf.Deg2Rad * (IsGrounded ?uprightSpringStrength: uprightSpringStrength/4f));

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
        for (int i = 0; i < pdBones.Count; i++)
        {
            var bone = pdBones[i];
            bone.designDeltaTime = pdDesignDt; // same reference dt across the rig
            bone.Step(Runner.DeltaTime);
        }
    }

    public void UpdateAnimators()
    {
        if (!targetAnimator) return;

        var targetPos = smoothedNetworkRoot.position + hipsOffset;
        var targetRot = smoothedNetworkRoot.rotation;

        targetAnimator.gameObject.transform.position = targetPos;
        targetAnimator.gameObject.transform.rotation = targetRot;
        //finalAnimator.gameObject.transform.position = targetPos;
        //finalAnimator.gameObject.transform.rotation = targetRot;
    }

    [ContextMenu("PD Ragdoll/Bake Anchors • Use Each Target Transform")]
    private void ContextBakeAnchorsFromTargets()
    {

        if (pdBones == null || pdBones.Count == 0) { Debug.LogWarning("No PdBones to bake."); return; }

        for (int i = 0; i < pdBones.Count; i++)
        {
            var bone = pdBones[i];
            if (bone == null) continue;
            if (bone.targetTransform == null)
            {
                Debug.LogWarning($"PdBone {i}: targetTransform is missing, skipped.");
                continue;
            }
            bone.BakeAnchorsFromWorldPivot(bone.targetTransform.position);
        }

        Debug.Log("Baked PD anchors from each bone's targetTransform remember to save");

    }
}