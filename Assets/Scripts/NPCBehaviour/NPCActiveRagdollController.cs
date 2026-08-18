using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;


public class NPCActiveRagdollController : NetworkBehaviour, IHasPhysicalCore, IXPBDPoseProvider
{
    [Header("Components")]
    public Rigidbody coreRB;
    public XPBDPosAndRotSolver xpbdPosAndRotSolver;
    public float CurrentRagdollScale => xpbdPosAndRotSolver != null ? xpbdPosAndRotSolver.CurrentScale : 1f;

    [Header("RagDoll Strength"), Range(0,2f)]
    [Networked] public float ragDollStrength { get; set; } = 1;
    [Networked] public NetworkBool IsGrounded { get; set; }

    [Networked] public int LastJumpTick { get; set; }
    public float jumpSuspensionDuration = 0.2f;


    [Header("Grounded Settings")]
    public float extraRideHeight = 0f;
    public float extraGroundCheckDistance = 1.0f;
    public LayerMask groundLayer;
    public float suspensionCastRadius = 0.25f;

    public float rideSpringStrength = 100f;
    public float rideSpringDampingRatio = 1.0f;
    [Networked] public NetworkBool HasStartingRagdollSupport { get; set; }
    [Networked] public float StartingRagdollSupportForce { get; set; }
    [Networked] public float StartingRagdollMass { get; set; }

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
    [Min(0f)] public float maxLocomotiveForce = 80f;
    [Min(0f)] public float maxBrakingForce = 80f;

    [Header("Network Pos")]
    [SerializeField] public Transform networkedRenderRoot;
    public Transform smoothedNetworkRoot;

    [Header("PD Bones")]
    //public List<PdBone> pdBones = new List<PdBone>();
    private float pdDesignDt = 1f / 64f;

    public List<NetworkRigidbody3D> rbComponents = new List<NetworkRigidbody3D>();

    [Header("Animation")]
    public NetworkAnimator networkAnimator;
    public ArmatureRetargeter armatureRetargeter;
    [SerializeField] private float controllerAnimScale = 1f;
    public Vector3 hipsOffset;

    private Vector3 _lastRenderedPosition;
    private Vector3 _renderedVelocity;
    private bool _hasRenderedPosition;
    private int _xpbdPoseRequestTick = -1;

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


    [Header("Bonk Response")]
    [SerializeField] private BonkManager bonkManager;

    private float CurrentBonkNormalized => bonkManager != null ? bonkManager.CurrentBonkNormalized : 0f;

    private float EvaluateBonkCurve(AnimationCurve curve, float normalizedBonk)
    {
        if (curve == null || curve.length == 0) return 1f;
        return Mathf.Clamp01(curve.Evaluate(normalizedBonk));
    }

    [Tooltip("X: normalized bonk 0-1. Y: available horizontal force 0-1.")]
    [SerializeField] private AnimationCurve locomotiveForceByBonk = AnimationCurve.Linear(0f, 1f, 0.7f, 0f);

    [Tooltip("X: normalized bonk 0-1. Y: desired movement speed 0-1.")]
    [SerializeField] private AnimationCurve movementSpeedByBonk = AnimationCurve.Linear(0f, 1f, 0.7f, 0f);

    [Tooltip("Controls upright and turning stabilization.")]
    [SerializeField] private AnimationCurve uprightPowerByBonk = AnimationCurve.Linear(0f, 1f, 0.7f, 0f);

    [Tooltip("Controls active suspension and body support.")]
    [SerializeField] private AnimationCurve suspensionPowerByBonk = AnimationCurve.Linear(0f, 1f, 0.7f, 0f);

    [Tooltip("Controls the XPBD animation muscle strength.")]
    [SerializeField] private AnimationCurve poseStrengthByBonk = AnimationCurve.Linear(0f, 1f, 0.95f, 0f);

    public float GetMovementSpeed(NPCMovementMode movementMode)
    {
        float speed = 0f;

        if (movementMode == NPCMovementMode.Walk) speed = maxWalkSpeed;
        else if (movementMode == NPCMovementMode.Run) speed = maxSprintSpeed;

        return speed * Mathf.Sqrt(CurrentRagdollScale) * EvaluateBonkCurve(movementSpeedByBonk, CurrentBonkNormalized);
    }

    public void SetMovementTarget(Vector3 direction, NPCMovementMode movementMode)
    {
        direction.y = 0f;

        if (movementMode == NPCMovementMode.Stop || direction.sqrMagnitude < 0.0001f)
        {
            _desiredMoveVelocity = Vector3.zero;
            return;
        }

        _desiredMoveVelocity = direction.normalized * GetMovementSpeed(movementMode);
    }

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
        if (xpbdPosAndRotSolver == null) xpbdPosAndRotSolver = GetComponent<XPBDPosAndRotSolver>();
        if (armatureRetargeter == null) armatureRetargeter = GetComponentInChildren<ArmatureRetargeter>();

        Runner.SetIsSimulated(this.Object, true);
        foreach (NetworkRigidbody3D nrb in rbComponents)
        {
            Runner.SetIsSimulated(nrb.Object, true);
        }

        if (bonkManager == null) bonkManager = GetComponent<BonkManager>();
        if (HasStateAuthority) TryInitializeStartingRagdollSupport();
    }

    public void Tick()
    {
        if (HasStateAuthority && !HasStartingRagdollSupport) TryInitializeStartingRagdollSupport();

        float bonk = CurrentBonkNormalized;
        float locomotivePower = EvaluateBonkCurve(locomotiveForceByBonk, bonk);
        float uprightPower = EvaluateBonkCurve(uprightPowerByBonk, bonk);
        float suspensionPower = EvaluateBonkCurve(suspensionPowerByBonk, bonk);

        ApplyUprightStabilization(uprightPower);
        UpdateCoreMovement(locomotivePower, suspensionPower);
        UpdateAnimatorParameters(true);
        _xpbdPoseRequestTick = Runner.Tick;



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
        if (networkAnimator == null) return;

        UpdateRenderedVelocity();
        UpdateAnimatorParameters(false);
        UpdateAnimatorPosition(false);

        networkAnimator.UpdateVisualAnimator(out Vector3 visualPos, out Quaternion visualRot);

        if (armatureRetargeter != null && armatureRetargeter.readRootMotion)
        {
            armatureRetargeter.animatedHipRootMotion = visualPos * CurrentRagdollScale;
            armatureRetargeter.animatedHipRotation = visualRot;
        }
    }

    public void PrepareXPBDPose()
    {
        float bonkPoseStrength = EvaluateBonkCurve(poseStrengthByBonk, CurrentBonkNormalized);
        xpbdPosAndRotSolver.MuscleStrengthMultiplier = Mathf.Clamp01(ragDollStrength) * bonkPoseStrength;

        if (_xpbdPoseRequestTick != Runner.Tick) return;

        if (networkAnimator == null)
        {
            _currentAbsoluteRM_Y = 0f;
            _currentAbsoluteRM_Rot = Quaternion.identity;
            return;
        }

        UpdateAnimatorPosition(true);
        networkAnimator.UpdatePhysicsAnimator(out Vector3 rmDeltaPos, out Quaternion rmDeltaRot, out Vector3 absRmPos, out Quaternion absRmRot);

        float ragdollScale = CurrentRagdollScale;
        rmDeltaPos *= ragdollScale;
        absRmPos *= ragdollScale;

        _currentAbsoluteRM_Y = useRootMotionY ? absRmPos.y : 0f;
        _currentAbsoluteRM_Rot = absRmRot;
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

    private void ApplyCoreSuspention(float power, float forceScale)
    {
        float ragdollScale = CurrentRagdollScale;
        float rootMotionRideHeight = useRootMotionY ? _currentAbsoluteRM_Y * rootYMult : 0f;
        float targetHeight = Mathf.Max(0.01f, rootMotionRideHeight + (extraRideHeight * ragdollScale));
        float castOriginOffset = 0.1f * ragdollScale;
        float castRadius = suspensionCastRadius * ragdollScale;
        float groundCheckExtension = extraGroundCheckDistance * ragdollScale;
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
                if (HasStartingRagdollSupport) gravitySupportAcceleration = StartingRagdollSupportForce * forceScale / Mathf.Max(0.01f, coreRB.mass);
            }

            float verticalVelocity = Vector3.Dot(coreRB.linearVelocity, Vector3.up);
            float dampingAcceleration = kd * verticalVelocity;
            Vector3 suspensionAcceleration = Vector3.up * (gravitySupportAcceleration + springAcceleration - dampingAcceleration) * power;
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

        float startingMass = 0f;
        float startingSupportForce = 0f;
        foreach (Rigidbody body in startingBodies)
        {
            PhysicsObjectProperties properties = body.GetComponent<PhysicsObjectProperties>();
            if (properties != null && properties.CurrentSimData.Mass <= 0f) return false;

            float bodyMass = properties != null ? properties.CurrentSimData.Mass : body.mass;
            float gravityMultiplier = properties != null ? properties.CurrentSimData.GravityMultiplier : body.useGravity ? 1f : 0f;
            float downwardGravityAcceleration = Mathf.Max(0f, -Vector3.Dot(Physics.gravity * gravityMultiplier, Vector3.up));
            startingMass += Mathf.Max(0.01f, bodyMass);
            startingSupportForce += Mathf.Max(0.01f, bodyMass) * downwardGravityAcceleration;
        }

        if (float.IsNaN(startingMass) || float.IsInfinity(startingMass) || float.IsNaN(startingSupportForce) || float.IsInfinity(startingSupportForce)) return false;

        StartingRagdollMass = startingMass;
        StartingRagdollSupportForce = startingSupportForce;
        HasStartingRagdollSupport = true;
        return true;
    }

    private void ApplyUprightStabilization(float power)
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

        float uprightKp = uprightSpringStrength * strengthSquared * groundedMultiplier * power;
        float uprightKd = uprightSpringDamper * strength * groundedMultiplier * power;
        float turnKp = turnSpringStrength * strengthSquared * groundedMultiplier * power;
        float turnKd = turnSpringDamper * strength * groundedMultiplier * power;

        float maxAngleRad = 60f * Mathf.Deg2Rad;
        float maxAccel = 4000f * power;

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

    private void UpdateCoreMovement(float locomotivePower, float suspensionPower)
    {
        int ticksSinceJump = Runner.Tick - LastJumpTick;
        int suspensionBlindTicks = Mathf.CeilToInt(jumpSuspensionDuration / Runner.DeltaTime);
        bool isJumpBlindWindow = ticksSinceJump <= suspensionBlindTicks;
        float startScale = xpbdPosAndRotSolver != null ? xpbdPosAndRotSolver.StartScale : 1f;
        float scaleRatio = CurrentRagdollScale / Mathf.Max(0.01f, startScale);
        float forceScale = CustomPhysicsFormulas.CalculateScalePower(scaleRatio, CustomPhysicsFormulas.DistanceConstraintStrengthExponent);

        if (ticksSinceJump == 0) coreRB.AddForce(Vector3.up * jumpForce * forceScale * locomotivePower, ForceMode.Impulse);

        Vector3 currentHorizontalVelocity = Vector3.ProjectOnPlane(coreRB.linearVelocity, Vector3.up);
        Vector3 desiredHorizontalVelocity = Vector3.ProjectOnPlane(_desiredMoveVelocity, Vector3.up);
        Vector3 velocityError = desiredHorizontalVelocity - currentHorizontalVelocity;
        bool isTryingToMove = desiredHorizontalVelocity.sqrMagnitude > 0.0001f;
        float responseGain = isTryingToMove ? acceleration : braking;
        float startingMass = StartingRagdollMass > 0f ? StartingRagdollMass : coreRB.mass;
        Vector3 requestedForce = velocityError * responseGain * startingMass * forceScale;
        float maximumForce = (isTryingToMove ? maxLocomotiveForce : maxBrakingForce) * forceScale * locomotivePower;
        Vector3 force = Vector3.ClampMagnitude(requestedForce, maximumForce);

        if (IsGrounded) coreRB.AddForce(force, ForceMode.Force);
        if (!isJumpBlindWindow) ApplyCoreSuspention(suspensionPower, forceScale);
    }

   
    private void UpdateRenderedVelocity()
    {
        Transform renderRoot = smoothedNetworkRoot != null ? smoothedNetworkRoot : networkedRenderRoot;
        if (renderRoot == null || Time.deltaTime <= 1e-6f) return;

        Vector3 currentPosition = renderRoot.position;
        if (!_hasRenderedPosition)
        {
            _hasRenderedPosition = true;
            _lastRenderedPosition = currentPosition;
            _renderedVelocity = Vector3.zero;
            return;
        }

        Vector3 rawVelocity = (currentPosition - _lastRenderedPosition) / Time.deltaTime;
        float smoothing = 1f - Mathf.Exp(-12f * Time.deltaTime);
        _renderedVelocity = Vector3.Lerp(_renderedVelocity, rawVelocity, smoothing);
        if (_renderedVelocity.magnitude < 0.05f) _renderedVelocity = Vector3.zero;

        _lastRenderedPosition = currentPosition;
    }

    private void UpdateAnimatorPosition(bool isSim)
    {
        if (networkAnimator == null || networkAnimator.UnityAnimator == null || coreRB == null) return;

        Transform renderRoot = smoothedNetworkRoot != null ? smoothedNetworkRoot : networkedRenderRoot;
        if (!isSim && renderRoot == null) return;

        Vector3 targetPosition = isSim ? coreRB.position + hipsOffset : renderRoot.position + hipsOffset;
        Quaternion targetRotation = isSim ? coreRB.rotation : renderRoot.rotation;
        networkAnimator.UnityAnimator.transform.SetPositionAndRotation(targetPosition, targetRotation);
    }

    private void UpdateAnimatorParameters(bool isSim)
    {
        if (networkAnimator == null || coreRB == null) return;

        Transform renderRoot = smoothedNetworkRoot != null ? smoothedNetworkRoot : networkedRenderRoot;
        Vector3 facingDirection = isSim ? _desiredLookDirection : renderRoot != null ? renderRoot.forward : coreRB.transform.forward;
        Vector3 fwd = Vector3.ProjectOnPlane(facingDirection, Vector3.up);
        if (fwd.sqrMagnitude < 1e-6f)
        {
            fwd = Vector3.ProjectOnPlane(coreRB.transform.forward, Vector3.up);
        }
        fwd.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        Vector3 currentVel = isSim ? coreRB.linearVelocity : _renderedVelocity;
        Vector2 localPlanarVelocity = new Vector2(Vector3.Dot(currentVel, right), Vector3.Dot(currentVel, fwd));
        float planarSpeed = localPlanarVelocity.magnitude;
        float scaledWalkSpeed = Mathf.Max(0.01f, GetMovementSpeed(NPCMovementMode.Walk));
        float scaledRunSpeed = Mathf.Max(scaledWalkSpeed, GetMovementSpeed(NPCMovementMode.Run));
        float animationSpeed = GetNormalizedAnimationSpeed(planarSpeed, scaledWalkSpeed, scaledRunSpeed);
        Vector2 animationVelocity = planarSpeed > 0.001f ? localPlanarVelocity / planarSpeed * animationSpeed : Vector2.zero;
        float verticalVelocity = currentVel.y;

        float movementSizeScale = Mathf.Sqrt(Mathf.Max(0.01f, CurrentRagdollScale));
        controllerAnimScale = 1f / movementSizeScale;

        networkAnimator.SetControllerAnimScale(controllerAnimScale);
        if (isSim)
        {
            networkAnimator.SetSimFloat("VelocityX", animationVelocity.x);
            networkAnimator.SetSimFloat("VelocityY", animationVelocity.y);
            networkAnimator.SetSimFloat("VerticalVelocity", verticalVelocity);
            networkAnimator.SetSimBool("IsGrounded", IsGrounded);
        }
        else
        {
            networkAnimator.SetRenderFloat("VelocityX", animationVelocity.x, 0.05f, Time.deltaTime);
            networkAnimator.SetRenderFloat("VelocityY", animationVelocity.y, 0.05f, Time.deltaTime);
            networkAnimator.SetRenderFloat("VerticalVelocity", verticalVelocity, 0.05f, Time.deltaTime);
            networkAnimator.SetRenderBool("IsGrounded", IsGrounded);
        }
    }

    private float GetNormalizedAnimationSpeed(float currentSpeed, float walkSpeed, float runSpeed)
    {
        if (currentSpeed <= walkSpeed) return currentSpeed / walkSpeed;
        if (runSpeed <= walkSpeed) return 1f;

        return 1f + Mathf.Clamp01((currentSpeed - walkSpeed) / (runSpeed - walkSpeed));
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
            NPCMovementMode movementMode = speed <= 0f ? NPCMovementMode.Stop : speed <= 1f ? NPCMovementMode.Walk : NPCMovementMode.Run;
            SetMovementTarget(input, movementMode);
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
