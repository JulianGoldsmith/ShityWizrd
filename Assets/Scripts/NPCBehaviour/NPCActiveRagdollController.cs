using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;


public class NPCActiveRagdollController : NetworkBehaviour, IHasPhysicalCore
{
    [Header("Components")]
    public Rigidbody coreRB;

    [Header("RagDoll Strength"), Range(0,2f)]
    [Networked] public float ragDollStrength { get; set; } = 1;
    [Networked] public NetworkBool IsGrounded { get; set; }

    [Networked] public int LastJumpTick { get; set; }
    public float jumpSuspensionDuration = 0.2f;


    [Header("Size")]
    [Min(0.01f)]
    public float sizeMult = 1;
    [Networked] public float CreatureScale { get; set; }
    private bool _hasSpawned;
    public float CurrentCreatureScale => _hasSpawned && CreatureScale > 0f ? CreatureScale : Mathf.Max(0.01f, sizeMult);

    [Header("Grounded Settings")]
    public float extraRideHeight = 0f;
    public float extraGroundCheckDistance = 1.0f;
    public LayerMask groundLayer;
    public float suspensionCastRadius = 0.25f;

    public float rideSpringStrength = 100f;
    public float rideSpringDampingRatio = 1.0f;
    [Networked] public NetworkBool HasStartingRagdollSupport { get; set; }
    [Networked] public float StartingRagdollSupportForce { get; set; }

    [Header("Upright Settings")]
    public float uprightSpringStrength = 50f;
    public float uprightSpringDamper = 5f;

    [Header("Turn Settings")]
    public float turnSpringStrength = 50f;
    public float turnSpringDamper = 5f;

    [Header("Movement Settings")]
    public float maxWalkSpeed = 3f, maxSprintSpeed = 5f;
    public float acceleration = 20f;
    public float braking = 20f;
    public float jumpForce = 50f;

    [Header("Network Pos")]
    [SerializeField] public Transform networkedRenderRoot;
    public Transform smoothedNetworkRoot;

    [Header("PD Bones")]
    //public List<PdBone> pdBones = new List<PdBone>();
    private float pdDesignDt = 1f / 64f;

    public List<NetworkRigidbody3D> rbComponents = new List<NetworkRigidbody3D>();

    [Header("Animation")]
    public NetworkAnimator networkAnimator;
    public Vector3 hipsOffset;

    public float rootMotionForceStrength = 5.0f;
    public bool useRootMotionXZ = true, useRootMotionY = true;
    public float rootYMult = 1f;

    public float _currentAbsoluteRM_Y;
    public Quaternion _currentAbsoluteRM_Rot;

    // --- BT INTERFACE ---


    [HideInInspector][Networked] public NetworkButtons _lastButtonsInput { get; set; }

    private Vector3 _desiredMoveVelocity;
    private Vector3 _desiredLookDirection;
    private bool _wantsToJump;





    public CharacterBonkController characterBonkController;


    public void SetMovementTarget(Vector3 velocity) => _desiredMoveVelocity = velocity;

    public void SetLookDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0;
        _desiredLookDirection = worldDirection.normalized;
    }

    public void TriggerJump()
    {
        if (IsGrounded && Runner.Tick > LastJumpTick + Mathf.CeilToInt(jumpSuspensionDuration / Runner.DeltaTime))
        {
            LastJumpTick = Runner.Tick;

            if (networkAnimator != null)
            {
                networkAnimator.SetTrigger("Jump");
            }
        }
    }

    // Optional: Let behaviours tweak the physical stiffness (e.g., getting frozen)
    public void SetTargetRagdollStrength(float strength) => ragDollStrength = strength;

    public override void Spawned()
    {
        _hasSpawned = true;
        if(HasStateAuthority) CreatureScale = Mathf.Max(0.01f, sizeMult);

        Runner.SetIsSimulated(this.Object, true);
        foreach (NetworkRigidbody3D nrb in rbComponents)
        {
            Runner.SetIsSimulated(nrb.Object, true);
        }
       
        characterBonkController = this.GetComponent<CharacterBonkController>();
        if (HasStateAuthority) TryInitializeStartingRagdollSupport();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _hasSpawned = false;
        base.Despawned(runner, hasState);
    }

    public bool TrySetCreatureScale(float scale)
    {
        if (!HasStateAuthority) return false;

        CreatureScale = Mathf.Max(0.01f, scale);
        return true;
    }

    public void Tick()
    {
        if (HasStateAuthority && !HasStartingRagdollSupport) TryInitializeStartingRagdollSupport();

        if (characterBonkController.BonkedState == BONKEDSTATE.ALIVE)
        {
            ApplyUprightStabilization();

            UpdateCoreMovement();

            //ApplyCoreSuspention();
        }


        UpdateAnimatorParameters();

        if (networkAnimator != null)
        {

            networkAnimator.UpdatePhysicsAnimator(out Vector3 rmDeltaPos, out Quaternion rmDeltaRot, out Vector3 absRmPos, out Quaternion absRmRot);

            float creatureScale = CurrentCreatureScale;
            rmDeltaPos *= creatureScale;
            absRmPos *= creatureScale;

            if (useRootMotionXZ && rmDeltaPos.sqrMagnitude > 0.0001f)
            {
                // Root motion has already been scaled once by CurrentCreatureScale.
                Vector3 rmVelocity = rmDeltaPos / Runner.DeltaTime;

                // OVERRIDE the AI's desired movement with the animation's movement
                //_desiredMoveVelocity = new Vector3(rmVelocity.x, _desiredMoveVelocity.y, rmVelocity.z);
            }

            // --- 2. Y-AXIS RIDE HEIGHT ROOT MOTION ---
            if (useRootMotionY) _currentAbsoluteRM_Y = absRmPos.y;
            else _currentAbsoluteRM_Y = 0f;

            _currentAbsoluteRM_Rot = absRmRot;
        }
        else
        {
            _currentAbsoluteRM_Y = 0f;
            _currentAbsoluteRM_Rot = Quaternion.identity;
        }



        _desiredLookDirection = Vector3.zero;
        _desiredMoveVelocity = Vector3.zero;
        //ApplyRootMotionForce();

    }

    public override void FixedUpdateNetwork()
    {


        // ragDollStrength = this.GetComponent<NPCPhysicsObject>().current_bonkedness/100;
         //SetMovementTarget(transform.forward * maxWalkSpeed);
         //SetLookDirection(transform.right);

        
        

      
    }

    public override void Render()
    {
        if (networkAnimator != null)
        {
            networkAnimator.UpdateVisualAnimator(out Vector3 visualPos, out Quaternion visualRot, true);
        }

        if (smoothedNetworkRoot != null && coreRB != null)
        {
            var targetPos = smoothedNetworkRoot.position + hipsOffset;
            var targetRot = smoothedNetworkRoot.rotation;
        }
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
        float creatureScale = CurrentCreatureScale;
        float rootMotionRideHeight = useRootMotionY ? _currentAbsoluteRM_Y * rootYMult : 0f;
        float targetHeight = Mathf.Max(0.01f, rootMotionRideHeight + (extraRideHeight * creatureScale));
        float castOriginOffset = 0.1f * creatureScale;
        float castRadius = suspensionCastRadius * creatureScale;
        float groundCheckExtension = extraGroundCheckDistance * creatureScale;
        float castDistance = Mathf.Max(0.01f, targetHeight + castOriginOffset + groundCheckExtension - castRadius);
        Vector3 castOrigin = coreRB.position + (Vector3.up * castOriginOffset);

        if (Physics.SphereCast(castOrigin, castRadius, Vector3.down, out RaycastHit hit, castDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            IsGrounded = true;

            float currentHeight = Vector3.Dot(coreRB.position - hit.point, Vector3.up);
            float compression = targetHeight - currentHeight;
            float kp = Mathf.Max(0f, rideSpringStrength);
            float dampingRatio = Mathf.Max(0f, rideSpringDampingRatio);
            float kd = 2f * dampingRatio * Mathf.Sqrt(kp);
            float springAcceleration = 0f;
            float gravitySupportAcceleration = 0f;

            if (compression >= 0f)
            {
                springAcceleration = kp * compression;
                if (HasStartingRagdollSupport) gravitySupportAcceleration = StartingRagdollSupportForce / Mathf.Max(0.01f, coreRB.mass);
            }

            float verticalVelocity = Vector3.Dot(coreRB.linearVelocity, Vector3.up);
            float dampingAcceleration = kd * verticalVelocity;
            Vector3 suspensionAcceleration = Vector3.up * (gravitySupportAcceleration + springAcceleration - dampingAcceleration);
            coreRB.AddForce(suspensionAcceleration, ForceMode.Acceleration);
        }
        else
        {
            IsGrounded = false;
        }
    }

    private bool TryInitializeStartingRagdollSupport()
    {
        if (!HasStateAuthority || HasStartingRagdollSupport || coreRB == null) return HasStartingRagdollSupport;

        HashSet<Rigidbody> startingBodies = new HashSet<Rigidbody> { coreRB };
        foreach (NetworkRigidbody3D networkRB in rbComponents)
        {
            if (networkRB == null) continue;
            Rigidbody body = networkRB.GetComponent<Rigidbody>();
            if (body != null) startingBodies.Add(body);
        }

        float startingSupportForce = 0f;
        foreach (Rigidbody body in startingBodies)
        {
            PhysicsObjectProperties properties = body.GetComponent<PhysicsObjectProperties>();
            if (properties != null && properties.CurrentSimData.Mass <= 0f) return false;

            float startingMass = properties != null ? properties.CurrentSimData.Mass : body.mass;
            float gravityMultiplier = properties != null ? properties.CurrentSimData.GravityMultiplier : body.useGravity ? 1f : 0f;
            float downwardGravityAcceleration = Mathf.Max(0f, -Vector3.Dot(Physics.gravity * gravityMultiplier, Vector3.up));
            startingSupportForce += Mathf.Max(0.01f, startingMass) * downwardGravityAcceleration;
        }

        if (float.IsNaN(startingSupportForce) || float.IsInfinity(startingSupportForce)) return false;

        StartingRagdollSupportForce = startingSupportForce;
        HasStartingRagdollSupport = true;
        return true;
    }

    private void ApplyUprightStabilization()
    {
        Vector3 flatLook = Vector3.ProjectOnPlane(_desiredLookDirection, Vector3.up);
        if (flatLook.sqrMagnitude < 1e-6f) flatLook = Vector3.ProjectOnPlane(coreRB.transform.forward, Vector3.up);
        flatLook.Normalize();

        Quaternion qBase = Quaternion.LookRotation(flatLook, Vector3.up);
        Quaternion qAnimDelta = _currentAbsoluteRM_Rot;
        Quaternion targetRot = qBase * qAnimDelta;

        float strength = Mathf.Min(ragDollStrength, 1f);
        float strengthSquared = strength * strength;
        float groundedMultiplier = IsGrounded ? 1f : 0.2f;

        float uprightKp = uprightSpringStrength * strengthSquared * groundedMultiplier;
        float uprightKd = uprightSpringDamper * strength * groundedMultiplier;
        float turnKp = turnSpringStrength * strengthSquared * groundedMultiplier;
        float turnKd = turnSpringDamper * strength * groundedMultiplier;

        float maxAngleRad = 60f * Mathf.Deg2Rad;
        float maxAccel = 4000f;

        Vector3 uprightAcceleration = CalculateUprightAcceleration(uprightKp, uprightKd, maxAngleRad);
        Vector3 turnAcceleration = CalculateTurnAcceleration(targetRot, turnKp, turnKd, maxAngleRad);
        Vector3 rotationAcceleration = uprightAcceleration + turnAcceleration;

        if (rotationAcceleration.sqrMagnitude > maxAccel * maxAccel) rotationAcceleration = rotationAcceleration.normalized * maxAccel;

        coreRB.maxAngularVelocity = Mathf.Max(coreRB.maxAngularVelocity, 50f);
        coreRB.AddTorque(rotationAcceleration, ForceMode.Acceleration);
    }

    private Vector3 CalculateUprightAcceleration(float kp, float kd, float maximumAngleRadians)
    {
        Vector3 currentUp = coreRB.rotation * Vector3.up;
        Vector3 uprightAxis = Vector3.Cross(currentUp, Vector3.up);
        float axisMagnitude = uprightAxis.magnitude;
        float upDot = Mathf.Clamp(Vector3.Dot(currentUp, Vector3.up), -1f, 1f);
        float uprightAngle = Mathf.Atan2(axisMagnitude, upDot);

        if (axisMagnitude > 1e-6f)
        {
            uprightAxis /= axisMagnitude;
        }
        else if (upDot < 0f)
        {
            uprightAxis = Vector3.ProjectOnPlane(coreRB.rotation * Vector3.right, Vector3.up).normalized;
        }
        else
        {
            uprightAxis = Vector3.zero;
        }

        uprightAngle = Mathf.Min(uprightAngle, maximumAngleRadians);
        Vector3 tiltAngularVelocity = Vector3.ProjectOnPlane(coreRB.angularVelocity, Vector3.up);
        return (uprightAxis * uprightAngle * kp) - (tiltAngularVelocity * kd);
    }

    private Vector3 CalculateTurnAcceleration(Quaternion targetRotationWorld, float kp, float kd, float maximumAngleRadians)
    {
        Vector3 currentForward = Vector3.ProjectOnPlane(coreRB.rotation * Vector3.forward, Vector3.up);
        Vector3 targetForward = Vector3.ProjectOnPlane(targetRotationWorld * Vector3.forward, Vector3.up);
        float yawAngularVelocity = Vector3.Dot(coreRB.angularVelocity, Vector3.up);

        if (currentForward.sqrMagnitude < 1e-6f || targetForward.sqrMagnitude < 1e-6f) return Vector3.up * (-yawAngularVelocity * kd);

        currentForward.Normalize();
        targetForward.Normalize();

        float yawError = Vector3.SignedAngle(currentForward, targetForward, Vector3.up) * Mathf.Deg2Rad;
        yawError = Mathf.Clamp(yawError, -maximumAngleRadians, maximumAngleRadians);
        return Vector3.up * ((yawError * kp) - (yawAngularVelocity * kd));
    }

    private void UpdateCoreMovement()
    {
        int ticksSinceJump = Runner.Tick - LastJumpTick;

        int suspensionBlindTicks = Mathf.CeilToInt(jumpSuspensionDuration / Runner.DeltaTime);
        bool isJumpBlindWindow = ticksSinceJump <= suspensionBlindTicks;

        if (ticksSinceJump == 0)
        {
            coreRB.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }


        Vector3 currentHorizontalVelocity = new Vector3(coreRB.linearVelocity.x, 0, coreRB.linearVelocity.z);
        Vector3 velocityError = _desiredMoveVelocity - currentHorizontalVelocity;

        Vector3 force;
        if (_desiredMoveVelocity.magnitude > 0.01f)
        {
            force = velocityError * acceleration;
        }
        else
        {
            force = velocityError * braking;
        }

        if (IsGrounded)
        {
            coreRB.AddForce(force, ForceMode.Acceleration);
        }

       


        if (!isJumpBlindWindow)
        {
            ApplyCoreSuspention();
        }
    }

   
    private void UpdateAnimatorParameters()
    {
        if (networkAnimator == null || coreRB == null) return;

        Vector3 fwd = Vector3.ProjectOnPlane(_desiredLookDirection, Vector3.up);
        if (fwd.sqrMagnitude < 1e-6f)
        {
            fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }
        fwd.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        Vector3 currentVel = coreRB.linearVelocity;

        float velocityX = Vector3.Dot(currentVel, right);
        float velocityY = Vector3.Dot(currentVel, fwd);
        float verticalVelocity = currentVel.y;

        networkAnimator.SetSimFloat("VelocityX", velocityX);
        networkAnimator.SetSimFloat("VelocityY", velocityY);
        networkAnimator.SetSimFloat("VerticalVelocity", verticalVelocity);

        networkAnimator.SetSimBool("IsGrounded", IsGrounded);
    }

    /*[ContextMenu("PD Ragdoll/ Bake Anchors")]
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
*/
    public void SetLookDir(Vector3 worldDirection)
    {
        //if (!HasStateAuthority) return;
        if (Object.isActiveAndEnabled)
        {
            worldDirection.y = 0;
            _desiredLookDirection = worldDirection.normalized;
        }
    }
    public void SetMoveInput(Vector3 input, float speed) 
    {
        //if (!HasStateAuthority) return;
        if (Object.isActiveAndEnabled)
        {
            _desiredMoveVelocity = input;
            //NetworkedWantsToSprint = speed > 1 ? true: false;
        }
    }

    public NetworkObject GetCoreNetworkObject()
    {
        return coreRB.GetComponent<NetworkObject>();
    }

    public Transform GetCoreTransform(bool smoothedTrans = false)
    {
        return smoothedTrans ? networkedRenderRoot.transform : coreRB.transform;
    }

    public Rigidbody GetCoreRigidbody()
    {
        return coreRB;
    }
}
