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
    public float sizeMult = 1;

    [Header("Grounded Settings")]
    public float extraRideHeight = 0f;
    public float extraGroundCheckDistance = 1.0f;
    public LayerMask groundLayer;
    public float suspensionCastRadius = 0.25f;

    public float rideSpringStrength = 100f;
    public float rideSpringDampingRatio = 1.0f;
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




    public XpbdConstraintSolver xpbdSolver;

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
        if(TryGetComponent<NPCPhysicsObject>(out NPCPhysicsObject NPCPO))
        {
            PhysicsObjectProperties props = NPCPO.physicsObjectProperties;
            props.Size = sizeMult;
            NPCPO.physicsObjectProperties = props;
        }
        Runner.SetIsSimulated(this.Object, true);
        foreach (NetworkRigidbody3D nrb in rbComponents)
        {
            Runner.SetIsSimulated(nrb.Object, true);
        }
       
        characterBonkController = this.GetComponent<CharacterBonkController>();
    }

    public void Tick()
    {
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

            if (useRootMotionXZ && rmDeltaPos.sqrMagnitude > 0.0001f)
            {
                // Convert Delta to Velocity, and scale it by the NPC's size!
                Vector3 rmVelocity = (rmDeltaPos * sizeMult) / Runner.DeltaTime;

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


        UpdatePDDrives();

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
        //get the y (differecne between armeture and root from the animator (this is already scaled in the animator)
        float rootMotionVerticalDelta = _currentAbsoluteRM_Y * rootYMult; //this is our ride height now

        //float targetHeight = extraRideHeight + sizeMult + rootMotionVerticalDelta;

        float extraHeightToCastFrom = 0.1f * sizeMult;

        //float distanceToCast =  extraHeightToCastFrom + (suspensionCastRadius * sizeMult) + (extraGroundCheckDistance * sizeMult);
        float distanceToCast = rootMotionVerticalDelta + extraHeightToCastFrom + (suspensionCastRadius*sizeMult) + (extraGroundCheckDistance*sizeMult)+ extraRideHeight;

        if (Physics.SphereCast(coreRB.position + (Vector3.up * extraHeightToCastFrom), (suspensionCastRadius * sizeMult), Vector3.down, out RaycastHit hit, distanceToCast, groundLayer, QueryTriggerInteraction.Ignore))
        {
            IsGrounded = true;

            float targetHeight = useRootMotionY? rootMotionVerticalDelta + extraRideHeight : extraRideHeight;
            float currentHeight = (coreRB.transform.position - hit.point).magnitude;
            float compression = targetHeight - currentHeight;

            //if (compression <= 0)
            //{
            //    return;
            //}

            float springForce = 0f;

            if (compression > 0)
            {
                springForce = rideSpringStrength * compression /** Mathf.Min(ragDollStrength, 1)*/;
            }

            float kp = rideSpringStrength /* Mathf.Min(ragDollStrength, 1)*/;
            float kd = 2 * Mathf.Sqrt(kp * coreRB.mass);

            //springForce = rideSpringStrength * compression;

            float verticalVelocity = coreRB.linearVelocity.y;

            float damperForce = (kd * rideSpringDampingRatio) * verticalVelocity;

            Vector3 suspensionForce = Vector3.up * (springForce - damperForce) ;

            coreRB.AddForce(suspensionForce, ForceMode.Acceleration);
        }
        else
        {
            IsGrounded = false;
        }
    }

    private void ApplyUprightStabilization()
    {

        Vector3 flatLook = Vector3.ProjectOnPlane(_desiredLookDirection, Vector3.up);
        if (flatLook.sqrMagnitude < 1e-6f)
            flatLook = Vector3.ProjectOnPlane(coreRB.transform.forward, Vector3.up);
        flatLook.Normalize();

        Quaternion qBase = Quaternion.LookRotation(flatLook, Vector3.up);
        Quaternion qAnimDelta = _currentAbsoluteRM_Rot;
        Quaternion targetRot = qBase * qAnimDelta;

        float springDrive = uprightSpringStrength * Mathf.Min(ragDollStrength, 1) * Mathf.Min(ragDollStrength, 1);
        float springDamp = uprightSpringDamper * Mathf.Min(ragDollStrength, 1);

        // THE FIX: Reduce rotational stiffness by 50% when picked up / in the air!
        if (!IsGrounded)
        {
          
            springDrive *= 0.2f;
            springDamp *= 0.2f;
        }

        float maxAngleRad = 60f * Mathf.Deg2Rad;
        float maxAccel = 4000f;

        ApplyPdRotationLikeBone(childRB: coreRB, targetRotationWorld: targetRot, parentAngularVelocityWorld: Vector3.zero, kp: springDrive, kd: springDamp,
            maximumAngleRadians: maxAngleRad, maximumTorqueAcceleration: maxAccel, rotationErrorCurve: null);
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

    private void UpdatePDDrives()
    {
        //for (int i = 0; i < pdBones.Count; i++)
        //{
        //    var bone = pdBones[i];
        //    bone.Step(Runner.DeltaTime, ragDollStrength, sizeMult);
        //}
        if (xpbdSolver == null) return;

        float dt = Runner.DeltaTime;
        xpbdSolver.ApplyRotationalPD(characterBonkController.BonkedState == BONKEDSTATE.ALIVE? ragDollStrength : 0f, dt);
        xpbdSolver.Solve(dt, false,1, sizeMult);
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

    private void ApplyPdRotationLikeBone(Rigidbody childRB, Quaternion targetRotationWorld, Vector3 parentAngularVelocityWorld, float kp, 
        float kd, float maximumAngleRadians, float maximumTorqueAcceleration, AnimationCurve rotationErrorCurve = null ) {

        Quaternion rotationError = targetRotationWorld * Quaternion.Inverse(childRB.rotation);

        rotationError.ToAngleAxis(out float angleDegrees, out Vector3 errorAxisWorld);
        if (angleDegrees > 180f) angleDegrees -= 360f;
        float angleRadians = angleDegrees * Mathf.Deg2Rad;

        if (errorAxisWorld.sqrMagnitude < 1e-8f)
            errorAxisWorld = Vector3.zero;
        else
            errorAxisWorld.Normalize();

        angleRadians = Mathf.Clamp(angleRadians, -maximumAngleRadians, maximumAngleRadians);

        Vector3 angularVelocityErrorWorld = childRB.angularVelocity - parentAngularVelocityWorld;

        float rotationMult = 1f;
        if (rotationErrorCurve != null && maximumAngleRadians > 1e-4f)
        {
            float normalizedAngle = Mathf.Clamp01(Mathf.Abs(angleRadians) / maximumAngleRadians);
            rotationMult = rotationErrorCurve.Evaluate(normalizedAngle);
        }

        Vector3 torqueAccelerationWorld =
            (kp * angleRadians) * errorAxisWorld - (kd * angularVelocityErrorWorld);

        torqueAccelerationWorld *= rotationMult;

        if (torqueAccelerationWorld.sqrMagnitude > maximumTorqueAcceleration * maximumTorqueAcceleration)
            torqueAccelerationWorld = torqueAccelerationWorld.normalized * maximumTorqueAcceleration;

        childRB.maxAngularVelocity = Mathf.Max(childRB.maxAngularVelocity, 50f);

        childRB.AddTorque(torqueAccelerationWorld, ForceMode.Acceleration);
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