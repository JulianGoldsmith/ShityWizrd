using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem; 
using static HybridCharacterController;

public class NPCActiveRagdollController : NetworkBehaviour
{
    [Header("Components")]
    public Rigidbody coreRB;
 
    [Header("Grounded Settings")]
    public float extraGroundCheckDistance = 1.0f;
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

  
    public float rootMotionForceStrength = 5.0f;
    public bool useRootMotionXZ = true, useRootMotionY = true;

    // --- BT INTERFACE ---
    [Networked] public bool NetworkedWantsToSprint { get; set; }
    [Networked] public bool NetworkedWantsToJump { get; set; }
    //[Networked] public Vector3 lookDir { get; set; }
    //[Networked] public Quaternion lookRot { get; set; }
    //[Networked] public Vector2 moveInput { get; set; }
    [HideInInspector][Networked] public NetworkButtons _lastButtonsInput { get; set; }

    [HideInInspector][Networked] public Vector3 NetworkedLookVector { get; set; }
    [HideInInspector][Networked] public Vector3 NetworkedMoveVector { get; set; }

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


        if (animStateController != null && targetAnimator != null)
        {
            animStateController.SimulateAnimation();
        }
    }

    public override void FixedUpdateNetwork()
    {
        //if(!HasInputAuthority && !HasStateAuthority)
        //{
        //    Debug.Log("This is running on a proxy");
        //}

        //if (HasStateAuthority)
        //{
        //    Vector2 input = Vector2.zero;
        //    if (Keyboard.current.upArrowKey.isPressed)
        //    {
        //        input += Vector2.up;
        //    }
        //    if (Keyboard.current.downArrowKey.isPressed)
        //    {
        //        input += Vector2.down;
        //    }
        //    if (Keyboard.current.rightArrowKey.isPressed)
        //    {
        //        input += Vector2.right;
        //    }
        //    if (Keyboard.current.leftArrowKey.isPressed)
        //    {
        //        input += Vector2.left;
        //    }
        //    moveInput = input ;

        //    if (Keyboard.current.pKey.isPressed)
        //    {
        //        lookRot *= Quaternion.Euler(0f,10f,0f);
        //    }
        //    if (Keyboard.current.iKey.isPressed)
        //    {
        //        lookRot *= Quaternion.Euler(0f, -10f, 0f);
        //    }
        //    lookDir = lookRot * Vector3.forward;
        //}

        animStateController.SimulateAnimation();

        ApplyCoreSuspention();

        ApplyUprightStabilization();

        UpdateCoreMovement();

        //ApplyRootMotionForce();

        UpdatePDDrives();
    }

    public override void Render()
    {
        UpdateAnimators();
    }

    private void ApplyRootMotionForce(Vector3 rootMotionVelocity)
    {
        if (!IsGrounded || rootMotionVelocity.magnitude < 0.01f)
        {
            return;
        }

        Vector3 force = rootMotionVelocity * rootMotionForceStrength;

        coreRB.AddForce(force, ForceMode.Acceleration);
    }

    private void ApplyCoreSuspention()
    {
        float rootMotionVerticalDelta = animStateController.RootMotionOffsetFromOrigin * transform.localScale.y;

        float distanceToCast = rootMotionVerticalDelta + rideHeight + 0.2f + suspensionCastRadius + extraGroundCheckDistance;

        if (Physics.SphereCast(coreRB.position + Vector3.up*0.2f, suspensionCastRadius, Vector3.down, out RaycastHit hit, distanceToCast, groundLayer, QueryTriggerInteraction.Ignore))
        {
            IsGrounded = true;

            float targetHeight = useRootMotionY? rideHeight+rootMotionVerticalDelta : rideHeight;

            float compression = targetHeight - (coreRB.transform.position - hit.point).magnitude;

            if (compression <= 0)
            {
                return;
            }

            float springForce = rideSpringStrength * compression;

            float verticalVelocity = coreRB.linearVelocity.y;

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
        if (!IsGrounded)
        {

        }
        else
        {

        }


        Vector3 flatLook = Vector3.ProjectOnPlane(NetworkedLookVector, Vector3.up);
        if (flatLook.sqrMagnitude < 1e-6f)
        {
            flatLook = Vector3.ProjectOnPlane(coreRB.transform.forward, Vector3.up);
            if (flatLook.sqrMagnitude < 1e-6f) flatLook = coreRB.transform.forward;
        }
        flatLook.Normalize();

        Quaternion facingFromLookDir = Quaternion.LookRotation(flatLook, Vector3.up);

        Quaternion rootMotionRot = animStateController ? animStateController.RootMotionRotation : Quaternion.identity;
        Vector3 rootMotionEuler = rootMotionRot.eulerAngles;

        float rootMotionYawDeg = (animStateController && animStateController.zIsUp) ? Mathf.DeltaAngle(0f, rootMotionEuler.z)
            : Mathf.DeltaAngle(0f, rootMotionEuler.y);

        Quaternion rootYawOnWorldUp = Quaternion.AngleAxis(rootMotionYawDeg, Vector3.up);

        Quaternion targetRot = facingFromLookDir * rootYawOnWorldUp;

        Quaternion current = coreRB.rotation;

        Quaternion quaternionErrror = targetRot * Quaternion.Inverse(current);
        if (quaternionErrror.w < 0f) { quaternionErrror.x = -quaternionErrror.x; quaternionErrror.y = -quaternionErrror.y; quaternionErrror.z = -quaternionErrror.z; quaternionErrror.w = -quaternionErrror.w; }

        quaternionErrror.ToAngleAxis(out float errorINDegrees, out Vector3 errorAxsis);
        if (float.IsNaN(errorAxsis.x) || errorAxsis.sqrMagnitude < 1e-12f) return;

        float errorInRads = errorINDegrees * Mathf.Deg2Rad;
        Vector3 proportional = errorAxsis * errorInRads;              
        Vector3 derivative = -coreRB.angularVelocity;         

        
        float groundedScale = IsGrounded ? 1f : 0.5f;

        Vector3 torque = proportional * uprightSpringStrength + derivative * uprightSpringDamper;

        coreRB.AddTorque(torque * groundedScale, ForceMode.Acceleration);
    }

    private void UpdateCoreMovement()
    {
        if (!IsGrounded)
            return;

        if (NetworkedWantsToJump)
        {
            coreRB.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            NetworkedWantsToJump = false;
        }

        float targetSpeed = NetworkedWantsToSprint ? maxSprintSpeed : maxWalkSpeed;

        //Vector3 fwd;
        //if (lookRot != Quaternion.identity)
        //{
        //    fwd = Vector3.ProjectOnPlane(lookRot * Vector3.forward, Vector3.up);
        //}
        //else
        //{
        //    fwd = Vector3.ProjectOnPlane(lookDir, Vector3.up);
        //}

        //if (fwd.sqrMagnitude < 1e-6f)
        //{
        //    fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        //}
        //fwd.Normalize();

        //Vector3 right = Vector3.Cross(Vector3.up, fwd);

        //Vector2 input = moveInput;
        //if (input.sqrMagnitude > 1f)
        //    input.Normalize(); 

        Vector3 targetVelocity = NetworkedMoveVector * targetSpeed;

        Vector3 currentHorizontalVelocity = new Vector3(coreRB.linearVelocity.x, 0, coreRB.linearVelocity.z);
        Vector3 velocityError = targetVelocity - currentHorizontalVelocity;

        Vector3 force;
        if (NetworkedMoveVector.magnitude > 0.01f)
            force = velocityError * acceleration;
        else
            force = velocityError * braking;

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
        if (!targetAnimator || coreRB == null) return;

        var targetPos = smoothedNetworkRoot.position + hipsOffset;
        var targetRot = smoothedNetworkRoot.rotation;

        Vector3 vel = coreRB.linearVelocity;
        vel.y = 0f;

        Vector3 fwd = Vector3.ProjectOnPlane(NetworkedLookVector, Vector3.up);
        if (fwd.sqrMagnitude < 1e-6f)
        {
            fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }
        fwd.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        float localX = Vector3.Dot(vel, right); 
        float localY = Vector3.Dot(vel, fwd);   

        float normFactor = maxWalkSpeed > 0f ? maxWalkSpeed : 1f;
        localX /= normFactor;
        localY /= normFactor;

        localX = Mathf.Clamp(localX, -1f, 1f);
        localY = Mathf.Clamp(localY, -1f, 1f);

        animStateController.SetTargetMovement(new Vector2(localX, localY));
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

    public void SetLookDir(Vector3 worldDirection)
    {
        if (!HasStateAuthority) return;
        if (Object.isActiveAndEnabled)
        {
            worldDirection.y = 0;
            NetworkedLookVector = worldDirection.normalized;
        }
    }
    public void SetMoveInput(Vector3 input) //place for the AI to set input
    {
        if (!HasStateAuthority) return;
        if (Object.isActiveAndEnabled)
        {
            NetworkedMoveVector = input;
            
        }
    }
}