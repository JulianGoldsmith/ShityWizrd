using System.Collections.Generic;
using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;
using System;

public class XPBDGlobalManager : NetworkBehaviour
{
    [Header("Global Settings")]
    public int iterations = 4;
    public bool enableSolver = true;
    public List<XPBDPosAndRotSolver> registeredRagdolls = new List<XPBDPosAndRotSolver>();

    [Networked, Capacity(32)]
    public NetworkArray<NetworkTempJoint> NetworkedTempJoints { get; }
    private HydratedTempJoint[] _hydratedTempJoints = new HydratedTempJoint[32];

    [Networked, Capacity(16)]
    public NetworkArray<NetworkGrabJoint> NetworkedGrabJoints { get; }
    private HydratedGrabJoint[] _hydratedGrabJoints = new HydratedGrabJoint[16];

    private int _lastGrabCount = -1;

    private Dictionary<Rigidbody, XPBDState> _globalStates = new Dictionary<Rigidbody, XPBDState>();
    private RunnerSimulatePhysics3D _physicsSimulator;

    [Header("Grab Curve")]
    [Tooltip("0 = 0 distance error, 1 = max dragRange error. Value is Compliance Multiplier (Higher = Softer/Weaker).")]
    public AnimationCurve distanceComplianceCurve = new AnimationCurve(new Keyframe(0f, 1f),new Keyframe(1f, 10f));

    [Header("Grab Reaction")]
    [Tooltip("X is normalized grab load from 0 to 1. Y is the fraction of reciprocal impulse applied to the player.")]
    public AnimationCurve recoilTensionCurve = new AnimationCurve(new Keyframe(0f, 0f),new Keyframe(0.2f, 0f),new Keyframe(0.6f, 0.35f),new Keyframe(1f, 1f));



    [Tooltip("Safety limit for the velocity change applied to the player by one grab during one simulation tick.")]
    public float maxPlayerReactionVelocityChangePerTick = 0.5f;

    [Header("Grab Debug")]
    public float debugTensionForce;
    public float debugNormalizedLoad;
    public float debugReactionMultiplier;
    public Vector3 debugReactionVelocityChange;

    public float dragRange = 10f;

    public event Action AfterXPBDBeforePhysics;
    // public event Action AfterPhysics;

    public override void Spawned()
    {
        Runner.SetIsSimulated(this.Object, true);
        base.Spawned();

        for (int i = 0; i < _hydratedGrabJoints.Length; i++)
            _hydratedGrabJoints[i] = new HydratedGrabJoint();

        for (int i = 0; i < _hydratedTempJoints.Length; i++)
        {
            _hydratedTempJoints[i] = new HydratedTempJoint();
        }
        if (Runner.TryGetComponent(out _physicsSimulator))
        {
            _physicsSimulator.OnBeforeSimulate += BeforePhysicsSimulation;
            _physicsSimulator.OnAfterSimulate += AfterPhysicsSimulation;
        }
        else
        {
            Debug.LogError("[XPBDGlobalManager] RunnerSimulatePhysics3D was not found.", this);
        }
    }

    private void BeforePhysicsSimulation()
    {
        RemoveInvalidRagdollRegistrations();

        foreach (XPBDPosAndRotSolver ragdoll in registeredRagdolls)
        {
            ragdoll.PreparePoseForSolve();
        }

        if (enableSolver)
        {
            SolveRegularConstraintsBeforePhysics();
            PreparePostPhysicsGrabs();
        }

        AfterXPBDBeforePhysics?.Invoke();
    }

    private void SolveRegularConstraintsBeforePhysics()
    {
        float dt = Runner.DeltaTime;

        if (dt <= 0f)
            return;

        SyncHydratedTmpJoints();

        _globalStates.Clear();

        foreach (var ragdoll in registeredRagdolls)
            ragdoll.InitializeStates(dt, _globalStates);

        foreach (var joint in _hydratedTempJoints)
        {
            if (!joint.IsValid())
                continue;

            joint.parentState = GetOrInitializeTempState(joint.parentRb, joint.parentState, dt);
            joint.childState = GetOrInitializeTempState(joint.childRb, joint.childState, dt);
            joint.lambdaPosition = Vector3.zero;
            joint.lambdaRotation = Vector3.zero;
        }

        for (int i = 0; i < iterations; i++)
        {
            foreach (var ragdoll in registeredRagdolls)
                ragdoll.SolveConstraints(dt);

            foreach (var joint in _hydratedTempJoints)
            {
                if (!joint.IsValid())
                    continue;

                SolveTempDistance(joint, dt);
                SolveTempRotation(joint, dt);
            }
        }

        DeriveAllVelocities(dt);
        ApplyAllToUnity();
    }

    private void PreparePostPhysicsGrabs()
    {
        SyncHydratedGrabs();

        foreach (var grab in _hydratedGrabJoints)
        {
            grab.preparedForPostPhysics = false;

            if (!grab.IsValid() || grab.itemRb.isKinematic)
                continue;

            Rigidbody itemRb = grab.itemRb;
            XPBDState itemState = grab.itemState;
            XPBDKinematicTargetState targetState = grab.targetState;

            itemState.rb = itemRb;
            itemState.isKinematic = false;
            itemState.invMass = 1f / itemRb.mass;
            itemState.invInertiaLocal = new Vector3(1f / itemRb.inertiaTensor.x, 1f / itemRb.inertiaTensor.y, 1f / itemRb.inertiaTensor.z);
            itemState.qInertia = itemRb.inertiaTensorRotation;
            grab.centerOfMassLocal = itemRb.centerOfMass;
            itemState.p_prev = itemRb.worldCenterOfMass;
            itemState.q_prev = itemRb.rotation;
            itemState.p = itemState.p_prev;
            itemState.q = itemState.q_prev;
            itemState.v = itemRb.linearVelocity;
            itemState.w = itemRb.angularVelocity;

            grab.torsoPositionBeforePhysics = grab.torsoRb.position;
            grab.tetherAnchorPositionBeforePhysics = grab.torsoRb.worldCenterOfMass;
            grab.tetherLengthBeforePhysics = grab.grabberController.GrabTetherLength;

            Quaternion previousLookRotation = grab.grabberController.previousLookRot;
            Vector3 previousEyePosition = grab.grabberController.GetEyePosSim(grab.torsoPositionBeforePhysics, previousLookRotation);

            float previousGrabDistance = grab.networkedData.grabDistance;
            Quaternion previousGrabRotationOffset = Quaternion.identity;
            bool hasGrabControl = grab.grabberController.GrabControlItemId == grab.networkedData.itemId;

            if (hasGrabControl)
            {
                bool observingProxy = !grab.grabberController.HasStateAuthority && !grab.grabberController.HasInputAuthority;

                if (observingProxy)
                {
                    previousGrabDistance = grab.grabberController.GrabTargetDistance;
                    previousGrabRotationOffset = grab.grabberController.GrabRotationOffset;
                }
                else
                {
                    previousGrabDistance = grab.grabberController.PreviousGrabTargetDistance;
                    previousGrabRotationOffset = grab.grabberController.PreviousGrabRotationOffset;
                }
            }

            Vector3 previousDesiredTarget = previousEyePosition + previousLookRotation * Vector3.forward * previousGrabDistance;

            targetState.p_prev = previousDesiredTarget;
            targetState.q_prev = previousLookRotation * previousGrabRotationOffset * grab.networkedData.targetLocalRotation;
            targetState.p = targetState.p_prev;
            targetState.q = targetState.q_prev;

            grab.lambdaPosition = Vector3.zero;
            grab.lambdaRotation = Vector3.zero;
            grab.lambdaTether = 0f;
            grab.tetherDirection = Vector3.zero;
            grab.preparedForPostPhysics = true;
        }
    }

    private void AfterPhysicsSimulation()
    {
        if (!enableSolver) return;


        float dt = Runner.DeltaTime;

        if (dt <= 0f)
            return;

        debugTensionForce = 0f;
        debugNormalizedLoad = 0f;
        debugReactionMultiplier = 0f;
        debugReactionVelocityChange = Vector3.zero;

        // Update the item and hand target to their post-PhysX poses.
        foreach (var grab in _hydratedGrabJoints)
        {
            if (!grab.preparedForPostPhysics || !grab.IsValid())
                continue;

            XPBDState itemState = grab.itemState;
            XPBDKinematicTargetState targetState = grab.targetState;

            itemState.p = grab.itemRb.worldCenterOfMass;
            itemState.q = grab.itemRb.rotation;

            Quaternion currentLookRotation = grab.grabberController.lookRot;
            Vector3 currentEyePosition = grab.grabberController.GetEyePosSim(grab.torsoRb.position, currentLookRotation);

            float currentGrabDistance = grab.networkedData.grabDistance;
            Quaternion currentGrabRotationOffset = Quaternion.identity;
            bool hasGrabControl = grab.grabberController.GrabControlItemId == grab.networkedData.itemId;

            if (hasGrabControl)
            {
                currentGrabDistance = grab.grabberController.GrabTargetDistance;
                currentGrabRotationOffset = grab.grabberController.GrabRotationOffset;

                float aimDistanceChange = currentGrabDistance - grab.networkedData.grabDistance;
                float desiredTetherLength = grab.networkedData.initialTetherLength + aimDistanceChange;
                desiredTetherLength = Mathf.Max(0.1f, desiredTetherLength);

                float maximumReelMovement = Mathf.Max(0f, grab.grabberController.grabTetherReelSpeed) * dt;

                grab.grabberController.GrabTetherLength = Mathf.MoveTowards(grab.grabberController.GrabTetherLength, desiredTetherLength, maximumReelMovement);
            }

            targetState.p = currentEyePosition + currentLookRotation * Vector3.forward * currentGrabDistance;
            targetState.q = currentLookRotation * currentGrabRotationOffset * grab.networkedData.targetLocalRotation;
        }

        // Solve the grab constraint.
        for (int i = 0; i < iterations; i++)
        {
            foreach (var grab in _hydratedGrabJoints)
            {
                if (!grab.preparedForPostPhysics || !grab.IsValid())
                    continue;

                SolvePostPhysicsGrabRotation(grab, dt);
                SolvePostPhysicsGrabPosition(grab, dt);
                SolvePostPhysicsGrabTether(grab, dt);
            }
        }

        // Apply the solved item pose and reciprocal tether reaction.
        foreach (var grab in _hydratedGrabJoints)
        {
            if (!grab.preparedForPostPhysics || !grab.IsValid())
                continue;

            XPBDState itemState = grab.itemState;

            itemState.q = XPBDMath.NormalizeQuaternion(itemState.q);
            itemState.v = (itemState.p - itemState.p_prev) / dt;
            itemState.w = XPBDMath.GetDeltaTheta(itemState.q_prev, itemState.q) / dt;

            grab.itemRb.position = itemState.p - itemState.q * grab.centerOfMassLocal;
            grab.itemRb.rotation = itemState.q;
            grab.itemRb.linearVelocity = itemState.v;
            grab.itemRb.angularVelocity = itemState.w;

            float tetherForce = Mathf.Max(0f, -grab.lambdaTether / (dt * dt));
            float maxTetherForce = Mathf.Max(0.01f, grab.networkedData.maxTetherForce);
            float normalizedLoad = Mathf.Clamp01(tetherForce / maxTetherForce);
            float reactionMultiplier = Mathf.Clamp01(recoilTensionCurve.Evaluate(normalizedLoad) * grab.networkedData.reactionScale);

            Vector3 itemTetherImpulse = grab.lambdaTether * grab.tetherDirection / dt;
            Vector3 playerReactionImpulse = -itemTetherImpulse * reactionMultiplier;

            float playerMass = grab.grabberController.totalMass > 0f ? grab.grabberController.totalMass : grab.torsoRb.mass;
            Vector3 reactionVelocityChange = playerReactionImpulse / Mathf.Max(0.01f, playerMass);

            if (maxPlayerReactionVelocityChangePerTick > 0f)
                reactionVelocityChange = Vector3.ClampMagnitude(reactionVelocityChange, maxPlayerReactionVelocityChangePerTick);

            grab.torsoRb.linearVelocity += reactionVelocityChange;

            debugTensionForce = tetherForce;
            debugNormalizedLoad = normalizedLoad;
            debugReactionMultiplier = reactionMultiplier;
            debugReactionVelocityChange = reactionVelocityChange;

            grab.preparedForPostPhysics = false;
        }

    }

    private void SyncHydratedTmpJoints()
    {
        for (int i = 0; i < NetworkedTempJoints.Length; i++)
        {
            var netJoint = NetworkedTempJoints[i];
            var localJoint = _hydratedTempJoints[i];

            if (!netJoint.parentId.IsValid || !netJoint.childId.IsValid)
            {
                if (localJoint.IsValid())
                    localJoint.Clear();
                continue;
            }

            if (localJoint.networkedData.parentId != netJoint.parentId ||
                localJoint.networkedData.childId != netJoint.childId)
            {
                localJoint.networkedData = netJoint;

                if (Runner.TryFindObject(netJoint.parentId, out var pObj))
                    localJoint.parentRb = pObj.GetComponent<Rigidbody>();
                else
                    localJoint.parentRb = null;

                if (Runner.TryFindObject(netJoint.childId, out var cObj))
                    localJoint.childRb = cObj.GetComponent<Rigidbody>();
                else
                    localJoint.childRb = null;

                localJoint.lambdaPosition = Vector3.zero;
                localJoint.lambdaRotation = Vector3.zero;
                localJoint.parentState = null;
                localJoint.childState = null;
            }
            else
            {
                localJoint.networkedData = netJoint;
            }
        }
        
    }

    private void SyncHydratedGrabs()
    {
        // Loop through the fixed size arrays (Capacity 16)
        for (int i = 0; i < NetworkedGrabJoints.Length; i++)
        {
            var netGrab = NetworkedGrabJoints[i];
            var localGrab = _hydratedGrabJoints[i];

            // 1. If the network slot is empty, clear our local slot
            if (!netGrab.grabberId.IsValid || !netGrab.itemId.IsValid)
            {
                if (localGrab.IsValid())
                    localGrab.Clear();
                continue;
            }

            // 2. Mismatch detected! (New Grab, or Rollback Correction)
            if (localGrab.networkedData.grabberId != netGrab.grabberId ||
                localGrab.networkedData.itemId != netGrab.itemId)
            {
                localGrab.networkedData = netGrab;

                // Lookup the Player/Controller
                if (Runner.TryFindObject(netGrab.grabberId, out var grabberObj))
                {
                    localGrab.grabberController = grabberObj.GetComponent<HybridCharacterController>();
                    localGrab.torsoRb = localGrab.grabberController?.hipsRb;
                }
                else
                {
                    localGrab.grabberController = null;
                    localGrab.torsoRb = null;
                }

                // Lookup the Item
                if (Runner.TryFindObject(netGrab.itemId, out var itemObj))
                    localGrab.itemRb = itemObj.GetComponent<Rigidbody>();
                else
                    localGrab.itemRb = null;

                // Reset XPBD lambdas for the fresh grab
                localGrab.lambdaPosition = Vector3.zero;
                localGrab.lambdaRotation = Vector3.zero;
                localGrab.lambdaTether = 0f;
                localGrab.tetherDirection = Vector3.zero;
            }
            else
            {
                // 3. Exact Match. Just update the struct variables (Strength, Distance, etc.)
                localGrab.networkedData = netGrab;
            }
        }
    }

    private XPBDState GetOrInitializeTempState(Rigidbody rb, XPBDState cachedState, float dt)
    {
        if (rb == null) return null;
        if (_globalStates.TryGetValue(rb, out XPBDState existingState)) return existingState;

        XPBDState state = cachedState != null && cachedState.rb == rb
            ? cachedState
            : new XPBDState { rb = rb };

        bool isKinematic = rb.isKinematic;
        Vector3 position = rb.worldCenterOfMass;
        Quaternion rotation = rb.rotation;
        Vector3 linearVelocity = rb.linearVelocity;
        Vector3 angularVelocity = rb.angularVelocity;

        state.rb = rb;
        state.isKinematic = isKinematic;
        state.invMass = isKinematic ? 0f : 1f / rb.mass;
        state.invInertiaLocal = isKinematic
            ? Vector3.zero
            : new Vector3(1f / rb.inertiaTensor.x, 1f / rb.inertiaTensor.y, 1f / rb.inertiaTensor.z);
        state.qInertia = rb.inertiaTensorRotation;
        state.centerOfMassOffsetLocal = Quaternion.Inverse(rotation) * (position - rb.position);
        state.p_prev = position;
        state.q_prev = rotation;
        state.v = linearVelocity;
        state.w = angularVelocity;

        if (!isKinematic)
        {
            state.p = position + linearVelocity * dt;

            float angularSpeed = angularVelocity.magnitude;
            state.q = angularSpeed > 1e-6f
                ? Quaternion.AngleAxis(angularSpeed * Mathf.Rad2Deg * dt, angularVelocity / angularSpeed) * rotation
                : rotation;
        }
        else
        {
            state.p = position;
            state.q = rotation;
        }

        _globalStates.Add(rb, state);
        return state;
    }

    // --- TEMPORARY JOINT MATH (Matches Ragdoll Math exactly) ---

    #region solver              /////////////////////////////SOLVER///////////////////////////////

    private void SolveTempDistance(HydratedTempJoint grab, float dt)
    {
        XPBDState pState = grab.parentState;
        XPBDState cState = grab.childState;
        if (pState.isKinematic && cState.isKinematic) return;

        Vector3 r0 = pState.q * (grab.networkedData.parentAnchorLocal - pState.centerOfMassOffsetLocal);
        Vector3 r1 = cState.q * (grab.networkedData.childAnchorLocal - cState.centerOfMassOffsetLocal);
        Vector3 dir = (cState.p + r1) - (pState.p + r0);

        float alpha = grab.networkedData.distanceCompliance / (dt * dt);
        float gamma = (alpha * (0.5f * dt * grab.networkedData.distanceDamping)) / dt;

        XPBDMath.SolveSphericalPosition(pState, cState, r0, r1, dir, alpha, gamma, ref grab.lambdaPosition);
    }

    private void SolveTempRotation(HydratedTempJoint grab, float dt)
    {
        XPBDState pState = grab.parentState;
        XPBDState cState = grab.childState;
        if (pState.isKinematic && cState.isKinematic) return;

        Quaternion targetQ = pState.q * grab.networkedData.targetLocalRotation;
        Vector3 rotationError = XPBDMath.GetRotationErrorVector(targetQ, cState.q, out _);

        float alpha = grab.networkedData.muscleCompliance / (dt * dt);
        float gamma = (alpha * (0.5f * dt * grab.networkedData.muscleDamping)) / dt;

        XPBDMath.SolveSphericalRotation(pState, cState, rotationError, alpha, gamma, ref grab.lambdaRotation);
    }

    private void SolvePostPhysicsGrabPosition(HydratedGrabJoint grab, float dt)
    {
        XPBDState itemState = grab.itemState;
        XPBDKinematicTargetState targetState = grab.targetState;
        Vector3 itemAnchorFromCenterOfMassLocal = grab.networkedData.localGrabOffset - grab.centerOfMassLocal;

        Vector3 currentGrabPoint = itemState.p + itemState.q * itemAnchorFromCenterOfMassLocal;
        float errorDistance = Vector3.Distance(currentGrabPoint, targetState.p);
        float normalizedDistance = Mathf.Clamp01(errorDistance / Mathf.Max(0.01f, dragRange));

        float stiffness = Mathf.Max(0.01f, grab.networkedData.aimStiffness);
        float stretchMultiplier = distanceComplianceCurve.Evaluate(normalizedDistance);
        float alpha = (1f / stiffness) * stretchMultiplier / (dt * dt);
        float gamma = alpha * (0.5f * dt * grab.networkedData.aimDamping) / dt;

        Vector3 positionBeforeSolve = itemState.p;
        Vector3 lambdaBeforeSolve = grab.lambdaPosition;

        XPBDMath.SolveKinematicGrabPosition(targetState, itemState, itemAnchorFromCenterOfMassLocal, alpha, gamma, ref grab.lambdaPosition);

        float maxHorizontalLambda = Mathf.Max(0f, grab.networkedData.maxAimHorizontalForce) * dt * dt;
        float maxLiftLambda = Mathf.Max(0f, grab.networkedData.maxAimLiftForce) * dt * dt;

        Vector3 clampedLambda = grab.lambdaPosition;
        Vector3 horizontalLambda = new Vector3(clampedLambda.x, 0f, clampedLambda.z);
        horizontalLambda = Vector3.ClampMagnitude(horizontalLambda, maxHorizontalLambda);

        clampedLambda.x = horizontalLambda.x;
        clampedLambda.z = horizontalLambda.z;
        clampedLambda.y = Mathf.Clamp(clampedLambda.y, -maxHorizontalLambda, maxLiftLambda);

        Vector3 appliedDeltaLambda = clampedLambda - lambdaBeforeSolve;

        itemState.p = positionBeforeSolve + itemState.invMass * appliedDeltaLambda;
        grab.lambdaPosition = clampedLambda;
    }

    private void SolvePostPhysicsGrabTether(HydratedGrabJoint grab, float dt)
    {
        XPBDState itemState = grab.itemState;

        Vector3 tetherAnchorPosition = grab.torsoRb.worldCenterOfMass;
        Vector3 itemAnchorFromCenterOfMassLocal = grab.networkedData.localGrabOffset - grab.centerOfMassLocal;
        Vector3 leverArm = itemState.q * itemAnchorFromCenterOfMassLocal;
        Vector3 currentGrabPoint = itemState.p + leverArm;
        Vector3 previousGrabPoint = itemState.p_prev + itemState.q_prev * itemAnchorFromCenterOfMassLocal;

        Vector3 tetherOffset = currentGrabPoint - tetherAnchorPosition;
        float tetherDistance = tetherOffset.magnitude;

        if (tetherDistance < 0.0001f)
            return;

        Vector3 tetherDirection = tetherOffset / tetherDistance;
        float constraintError = tetherDistance - grab.grabberController.GrabTetherLength;

        if (constraintError <= 0f && grab.lambdaTether >= 0f)
            return;

        Vector3 grabPointDisplacement = currentGrabPoint - previousGrabPoint;
        Vector3 tetherAnchorDisplacement = tetherAnchorPosition - grab.tetherAnchorPositionBeforePhysics;
        float tetherLengthDisplacement = grab.grabberController.GrabTetherLength - grab.tetherLengthBeforePhysics;

        float radialDisplacement = Vector3.Dot(grabPointDisplacement - tetherAnchorDisplacement, tetherDirection) - tetherLengthDisplacement;

        Vector3 angularGradient = Vector3.Cross(leverArm, tetherDirection);
        Vector3 inverseInertiaAngularGradient = XPBDMath.ApplyInvInertiaWorld(angularGradient, itemState.q, itemState.qInertia, itemState.invInertiaLocal);

        float linearInverseMass = itemState.invMass;
        float angularInverseMass = Vector3.Dot(angularGradient, inverseInertiaAngularGradient);
        float effectiveInverseMass = linearInverseMass + angularInverseMass;

        if (effectiveInverseMass < 0.000001f)
            return;

        float stiffness = Mathf.Max(0.01f, grab.networkedData.tetherStiffness);
        float alpha = (1f / stiffness) / (dt * dt);
        float gamma = alpha * (0.5f * dt * grab.networkedData.tetherDamping) / dt;

        float deltaLambda = -(constraintError + alpha * grab.lambdaTether + gamma * radialDisplacement) / ((1f + gamma) * effectiveInverseMass + alpha);

        float previousLambda = grab.lambdaTether;
        float maximumLambda = Mathf.Max(0f, grab.networkedData.maxTetherForce) * dt * dt;
        float newLambda = Mathf.Clamp(previousLambda + deltaLambda, -maximumLambda, 0f);
        float appliedDeltaLambda = newLambda - previousLambda;

        itemState.p += linearInverseMass * appliedDeltaLambda * tetherDirection;

        Vector3 angularCorrection = XPBDMath.ApplyInvInertiaWorld(appliedDeltaLambda * angularGradient, itemState.q, itemState.qInertia, itemState.invInertiaLocal);
        XPBDMath.ApplyDeltaRotation(itemState, angularCorrection);

        grab.lambdaTether = newLambda;
        grab.tetherDirection = tetherDirection;
    }

    private void SolvePostPhysicsGrabRotation(HydratedGrabJoint grab, float dt)
    {
        XPBDState itemState = grab.itemState;
        XPBDKinematicTargetState targetState = grab.targetState;

        float alpha = 1f / Mathf.Max(0.01f, grab.networkedData.aimStiffness) / (dt * dt);
        float gamma = alpha * (0.5f * dt * grab.networkedData.aimDamping) / dt;

        Quaternion rotationBeforeSolve = itemState.q;
        Vector3 lambdaBeforeSolve = grab.lambdaRotation;

        XPBDMath.SolveKinematicGrabRotation(targetState, itemState, alpha, gamma, ref grab.lambdaRotation);

        float maxRotationLambda = Mathf.Max(0f, grab.networkedData.maxAimTorque) * dt * dt;
        Vector3 clampedLambda = Vector3.ClampMagnitude(grab.lambdaRotation, maxRotationLambda);
        Vector3 appliedDeltaLambda = clampedLambda - lambdaBeforeSolve;

        itemState.q = rotationBeforeSolve;

        Vector3 angularCorrection = XPBDMath.ApplyInvInertiaWorld(-appliedDeltaLambda, itemState.q, itemState.qInertia, itemState.invInertiaLocal);
        XPBDMath.ApplyDeltaRotation(itemState, angularCorrection);

        grab.lambdaRotation = clampedLambda;
    }
    // --- FINAL VELOCITY DERIVATION ---

    private void DeriveAllVelocities(float dt)
    {
        foreach (var kvp in _globalStates)
        {
            var state = kvp.Value;
            if (state.isKinematic) continue;

            state.v = (state.p - state.p_prev) / dt;
            state.w = XPBDMath.GetDeltaTheta(state.q_prev, state.q) / dt;
        }
    }

    private void ApplyAllToUnity()
    {
        foreach (var kvp in _globalStates)
        {
            var state = kvp.Value;
            if (state.isKinematic) continue;

            state.rb.linearVelocity = state.v;
            state.rb.angularVelocity = state.w;
            state.rb.position = state.p - state.q * state.centerOfMassOffsetLocal;
            state.rb.rotation = state.q;
        }
    }

    #endregion

    #region registration   /////////////////////////////Registration///////////////////////////////


    public void RegisterRagdoll(XPBDPosAndRotSolver solver)
    {
        RemoveInvalidRagdollRegistrations();

        if (IsInvalidRagdoll(solver) || registeredRagdolls.Contains(solver))
            return;

        registeredRagdolls.Add(solver);

        // Network IDs are replicated, so this produces the same ragdoll order
        // on every peer regardless of local Spawned() callback order.
        registeredRagdolls.Sort((a, b) =>
            a.Object.Id.Raw.CompareTo(b.Object.Id.Raw));
    }

    public void UnregisterRagdoll(XPBDPosAndRotSolver solver)
    {
        // List.Remove is idempotent: removing an absent solver is harmless.
        registeredRagdolls.Remove(solver);
    }



    public bool AddTempJoint(NetworkTempJoint newJoint)
    {
        for (int i = 0; i < NetworkedTempJoints.Length; i++)
        {
            // An empty slot is one where the parentId hasn't been set (or was cleared)
            if (!NetworkedTempJoints[i].parentId.IsValid)
            {
                NetworkedTempJoints.Set(i, newJoint);
                return true; // Successfully added
            }
        }

        Debug.LogWarning("XPBDGlobalManager: NetworkedTempJoints array is full! Cannot add new joint.");
        return false;
    }
    public bool RemoveTempJoint(NetworkId parentId, NetworkId childId)
    {
        for (int i = 0; i < NetworkedTempJoints.Length; i++)
        {
            var joint = NetworkedTempJoints[i];

            // Check if this slot contains the exact joint we want to remove
            if (joint.parentId == parentId && joint.childId == childId)
            {
                // Overwriting with default struct resets IDs, making .IsValid false
                NetworkedTempJoints.Set(i, default(NetworkTempJoint));
                return true; // Successfully removed
            }
        }

        Debug.LogWarning($"XPBDGlobalManager: Could not find joint between Parent {parentId} and Child {childId} to remove.");
        return false;
    }

    public bool AddGrabJoint(NetworkGrabJoint newJoint)
    {
        for (int i = 0; i < NetworkedGrabJoints.Length; i++)
        {
            // An empty slot is one where the parentId hasn't been set (or was cleared)
            if (!NetworkedGrabJoints[i].grabberId.IsValid)
            {
                NetworkedGrabJoints.Set(i, newJoint);
                return true; // Successfully added
            }
        }

        Debug.LogWarning("XPBDGlobalManager: NetworkedGrabJoints array is full! Cannot add new joint.");
        return false;
    }

    public bool RemoveGrabJoint(NetworkId grabberId, NetworkId itemId)
    {
        for (int i = 0; i < NetworkedGrabJoints.Length; i++)
        {
            var joint = NetworkedGrabJoints[i];

            // Check if this slot contains the exact joint we want to remove
            if (joint.grabberId == grabberId && joint.itemId == itemId)
            {
                // Overwriting with default struct resets IDs, making .IsValid false
                NetworkedGrabJoints.Set(i, default(NetworkGrabJoint));
                return true; // Successfully removed
            }
        }

        Debug.LogWarning($"XPBDGlobalManager: Could not find joint between Parent {grabberId} and Child {itemId} to remove.");
        return false;
    }


    private static bool IsInvalidRagdoll(XPBDPosAndRotSolver ragdoll)
    {
        return ragdoll == null ||
               ragdoll.Object == null ||
               !ragdoll.Object.IsValid;
    }

    private void RemoveInvalidRagdollRegistrations()
    {
        for (int i = registeredRagdolls.Count - 1; i >= 0; i--)
        {
            if (IsInvalidRagdoll(registeredRagdolls[i]))
            {
                registeredRagdolls.RemoveAt(i);
            }
        }
    }


    private HashSet<NetworkId> CollectDepartingPlayerIds(NetworkObject playerRoot)
    {
        var departingIds = new HashSet<NetworkId>();

        AddNetworkObjectId(departingIds, playerRoot);

        // Covers objects which are still beneath the player hierarchy.
        foreach (var networkObject in playerRoot.GetComponentsInChildren<NetworkObject>(true))
        {
            AddNetworkObjectId(departingIds, networkObject);
        }

        // The ragdoll root is detached during NetworkedRagDoll.Spawned(),
        // so collect every body referenced by the XPBD solver as well.
        if (playerRoot.TryGetComponent(out XPBDPosAndRotSolver solver))
        {
            foreach (var joint in solver.joints)
            {
                AddRigidbodyNetworkId(departingIds, joint.parent);
                AddRigidbodyNetworkId(departingIds, joint.child);
            }
        }

        // Additional protection for bodies explicitly stored by the
        // HybridCharacterController.
        if (playerRoot.TryGetComponent(out HybridCharacterController controller))
        {
            foreach (var nrb in controller.networkRigidbody3Ds)
            {
                if (nrb != null)
                    AddNetworkObjectId(departingIds, nrb.Object);
            }

            foreach (var nrb in controller.networkRagdollRigidbody3Ds)
            {
                if (nrb != null)
                    AddNetworkObjectId(departingIds, nrb.Object);
            }
        }

        return departingIds;
    }

    private static void AddNetworkObjectId(HashSet<NetworkId> ids,NetworkObject networkObject)
    {
        if (networkObject != null && networkObject.IsValid)
            ids.Add(networkObject.Id);
    }

    private static void AddRigidbodyNetworkId(HashSet<NetworkId> ids,Rigidbody rigidbody)
    {
        if (rigidbody == null)
            return;

        if (rigidbody.TryGetComponent(out NetworkObject networkObject))
            AddNetworkObjectId(ids, networkObject);
    }

    public bool CleanupDepartingPlayer(NetworkObject playerRoot)
    {
        if (!HasStateAuthority)
            return false;


        if (playerRoot == null || !playerRoot.IsValid)
            return false;
        

        HashSet<NetworkId> departingIds = CollectDepartingPlayerIds(playerRoot);

        var heldItemIds = new HashSet<NetworkId>();

        NetworkedInventoryManager inventory = null;

        if (playerRoot.TryGetComponent(out inventory))
        {
            AddNetworkObjectId(heldItemIds, inventory.DraggedItem);

            for (int slot = 0; slot < NetworkedInventoryManager.InventoryCapacity; slot++)
            {
                NetworkId equippedItemId = inventory.EquippedItemIds[slot];
                if (equippedItemId.IsValid) heldItemIds.Add(equippedItemId);
            }
        }

        // Also reconcile equipable items through their replicated holder.
        // This catches an item even if the inventory reference became stale.
        foreach (var equipable in FindObjectsByType<EquipableItem>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if (equipable == null || equipable.Object == null ||!equipable.Object.IsValid)
            {
                continue;
            }

            if (equipable.HoldingPlayer == playerRoot)
                heldItemIds.Add(equipable.Object.Id);
        }

        // 1. Remove every grab owned by the departing player.
        // Capture its item first so it can be restored afterward.
        for (int i = 0; i < NetworkedGrabJoints.Length; i++)
        {
            NetworkGrabJoint grab = NetworkedGrabJoints[i];

            if (!grab.grabberId.IsValid)
                continue;

            if (!departingIds.Contains(grab.grabberId))
                continue;

            if (grab.itemId.IsValid)
                heldItemIds.Add(grab.itemId);

            NetworkedGrabJoints.Set(i, default);

            _hydratedGrabJoints[i]?.Clear();
        }

        // 2. Remove temporary joints touching the root or any bone.
        for (int i = 0; i < NetworkedTempJoints.Length; i++)
        {
            NetworkTempJoint joint = NetworkedTempJoints[i];

            bool parentIsDeparting = joint.parentId.IsValid && departingIds.Contains(joint.parentId);

            bool childIsDeparting = joint.childId.IsValid && departingIds.Contains(joint.childId);

            if (!parentIsDeparting && !childIsDeparting)
                continue;

            NetworkedTempJoints.Set(i, default);

            _hydratedTempJoints[i]?.Clear();
        }

        var sortedHeldItemIds = new List<NetworkId>(heldItemIds);

        sortedHeldItemIds.Sort((a, b) => a.Raw.CompareTo(b.Raw));

        foreach (NetworkId itemId in sortedHeldItemIds)
        {
            // Do not attempt to preserve an object which belongs to the departing player's own ragdoll.
            if (departingIds.Contains(itemId))
                continue;

            if (!Runner.TryFindObject(itemId, out NetworkObject itemObject))
                continue;

            if (itemObject.TryGetComponent(out InteractableItem item))
            {
                item.ForceReleaseForDisconnect(playerRoot);
            }
            else if (itemObject.HasStateAuthority)
            {

                itemObject.RemoveInputAuthority();

                if (itemObject.TryGetComponent(out Rigidbody rigidbody))
                {
                    rigidbody.isKinematic = false;
                    rigidbody.detectCollisions = true;
                    rigidbody.WakeUp();
                }
            }
        }

        // Clear player inventory references while the root remains valid.
        if (inventory != null)
        {
            inventory.DraggedItem = null;
            inventory.potentialItemToPickup = null;
            inventory.ActiveSlot = NetworkedInventoryManager.NoActiveSlot;

            for (int slot = 0; slot < NetworkedInventoryManager.InventoryCapacity; slot++)
            {
                inventory.EquippedItemIds.Set(slot, default);
            }
        }

        // 4. Remove this player's solver from the local ordered registry.
        // Despawned() will attempt this again, which is safe and idempotent.
        for (int i = registeredRagdolls.Count - 1; i >= 0; i--)
        {
            XPBDPosAndRotSolver ragdoll = registeredRagdolls[i];

            if (IsInvalidRagdoll(ragdoll) ||
                departingIds.Contains(ragdoll.Object.Id))
            {
                registeredRagdolls.RemoveAt(i);
            }
        }

        return true;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_physicsSimulator != null)
        {
            _physicsSimulator.OnBeforeSimulate -= BeforePhysicsSimulation;
            _physicsSimulator.OnAfterSimulate -= AfterPhysicsSimulation;
            _physicsSimulator = null;
        }

        base.Despawned(runner, hasState);
    }

    #endregion
}
