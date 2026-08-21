using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

#if UNITY_EDITOR
using UnityEditor; 
#endif



[System.Serializable]
public class XPBDTestJoint
{
    public Rigidbody parent;
    public Rigidbody child;

    //[Header("Distance Constraint")]
    public Vector3 parentAnchorLocal;
    public Vector3 childAnchorLocal;
    public float distanceCompliance = 0.001f;
    public float distanceDamping = 1f;

    //[Header("Animation Targets")]
    public Transform parentTarget;
    public Transform childTarget;

    public bool isRagdollJoint = false;

    //[Header("Muscle Constraint")]
    public float muscleCompliance = 0.0005f;
    public float muscleDamping = 1f;

    public bool enablePosition = true;
    public bool enableRotation = true;
    public bool enableAngularLimits = false;

    //[HideInInspector] public bool expandLimitsUI = true;

    //[Header("Angular Limits")]
    public JointAxisDirection twistAxis = JointAxisDirection.X;
    //[Tooltip("The reference axis for bending (must not be parallel to Twist).")]
    public JointAxisDirection forwardAxis = JointAxisDirection.Z;

    public Vector2 twistLimits = new Vector2(-45f, 45f);
    public Vector2 swing1Limits = new Vector2(-90f, 0f); // Up/Down bend
    public Vector2 swing2Limits = new Vector2(-10f, 10f);
    [HideInInspector] public bool isMirroredBasis = false;

    public float limitCompliance = 0.001f;
    public float limitDamping = 10f;

    [Range(0,100)]
    public bool drawLimitGizmos = true;
    public float gizmoSize = 0.25f;


    [HideInInspector] public Vector3 lambdaPosition;
    [HideInInspector] public Vector3 lambdaRotation;
    [HideInInspector] public Vector3 lambdaLimits;
    [System.NonSerialized] public XPBDState parentState;
    [System.NonSerialized] public XPBDState childState;

    [System.NonSerialized] public Vector3 scaledParentAnchorLocal;
    [System.NonSerialized] public Vector3 scaledChildAnchorLocal;
    [System.NonSerialized] public Quaternion targetLocalRotation;
    [System.NonSerialized] public Quaternion inverseRestChildLocalRotation;
    [System.NonSerialized] public float distanceAlpha;
    [System.NonSerialized] public float distanceGamma;
    [System.NonSerialized] public float limitAlpha;
    [System.NonSerialized] public float limitGamma;
    [System.NonSerialized] public float muscleAlphaBase;
    [System.NonSerialized] public float muscleDampingBase;

    [HideInInspector] public Vector3 bakedParentScale = Vector3.one;
    [HideInInspector] public Vector3 bakedChildScale = Vector3.one;

    [HideInInspector] public Quaternion restChildLocalRotation = Quaternion.identity;
    [HideInInspector] public Quaternion parentTargetToBodyRotation = Quaternion.identity;
    [HideInInspector] public Quaternion childTargetToBodyRotation = Quaternion.identity;


    /// <summary>
    /// parentTarget.rotation * parentTargetToBodyRotation = parent Rigidbody rotation
    ////childTarget.rotation* childTargetToBodyRotation = child Rigidbody rotation
    /// </summary>
    /// 

    [HideInInspector] public Vector3 twistAxisParent;
    [HideInInspector] public Vector3 swing1AxisParent;
    [HideInInspector] public Vector3 swing2AxisParent;



    [Range(0f, 2f)] public float leverArmScale = 1f;           
    [Range(0f, 1f)] public float parentRotationInfluence = 1f;
    [Range(0f, 1f)] public float parentPositionInfluence = 1f;

    public bool scaleForceByTension = false;
    public float minTensionDistance = 0.05f;
    public float maxTensionDistance = 0.25f;
    public AnimationCurve tensionReleaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);


    public Vector3 GetAxisVector(JointAxisDirection axis)
    {
        switch (axis)
        {
            case JointAxisDirection.X: return Vector3.right;
            case JointAxisDirection.Y: return Vector3.up;
            case JointAxisDirection.Z: return Vector3.forward;
            case JointAxisDirection.NegativeX: return -Vector3.right;
            case JointAxisDirection.NegativeY: return -Vector3.up;
            case JointAxisDirection.NegativeZ: return -Vector3.forward;
            default: return Vector3.right;
        }
    }

    public JointAxisDirection GetNegativeAxis(JointAxisDirection axis)
    {
        switch (axis)
        {
            case JointAxisDirection.X: return JointAxisDirection.NegativeX;
            case JointAxisDirection.Y: return JointAxisDirection.NegativeY;
            case JointAxisDirection.Z: return JointAxisDirection.NegativeZ;
            case JointAxisDirection.NegativeX: return JointAxisDirection.X;
            case JointAxisDirection.NegativeY: return JointAxisDirection.Y;
            case JointAxisDirection.NegativeZ: return JointAxisDirection.Z;
            default: return JointAxisDirection.NegativeX;
        }
    }

    public JointAxisDirection GetClosestAxis(Vector3 localDir, bool excludeTwist = false, JointAxisDirection twistToExclude = JointAxisDirection.X)
    {
        JointAxisDirection bestAxis = JointAxisDirection.X;
        float maxDot = -Mathf.Infinity;

        JointAxisDirection[] allAxes = (JointAxisDirection[])System.Enum.GetValues(typeof(JointAxisDirection));
        JointAxisDirection excludeNegative = excludeTwist ? GetNegativeAxis(twistToExclude) : twistToExclude;

        foreach (var axis in allAxes)
        {
            // If we are calculating Forward, we CANNOT pick the Twist axis (or its exact opposite)
            if (excludeTwist && (axis == twistToExclude || axis == excludeNegative)) continue;

            Vector3 axisVec = GetAxisVector(axis);

            // Dot product tells us how perfectly aligned the two vectors are (1 = perfect match)
            float dot = Vector3.Dot(localDir.normalized, axisVec);
            if (dot > maxDot)
            {
                maxDot = dot;
                bestAxis = axis;
            }
        }
        return bestAxis;
    }

    public void BakeRestPose()
    {
        if (parent == null || child == null) return;

        restChildLocalRotation = Quaternion.Inverse(parent.rotation) * child.rotation;

        if (parentTarget != null && childTarget != null)
        {
            parentTargetToBodyRotation = Quaternion.Inverse(parentTarget.rotation) * parent.rotation;
            childTargetToBodyRotation = Quaternion.Inverse(childTarget.rotation) * child.rotation;
        }
        else
        {
            parentTargetToBodyRotation = Quaternion.identity;
            childTargetToBodyRotation = Quaternion.identity;
        }

        RecalculateAxes();
    }

    public void RecalculateAxes()
    {
        if (parent == null || child == null) return;

        Vector3 twistLocal = GetAxisVector(twistAxis);
        Vector3 forwardLocal = GetAxisVector(forwardAxis);

        if (Mathf.Abs(Vector3.Dot(twistLocal, forwardLocal)) > 0.99f)
        {
            forwardLocal = twistLocal.x == 0 ? Vector3.right : Vector3.up;
        }

        Vector3 swing1Local = Vector3.Cross(twistLocal, forwardLocal).normalized;

        if (isMirroredBasis)
        {
            swing1Local = -swing1Local;
        }

        Vector3 swing2Local = Vector3.Cross(twistLocal, swing1Local).normalized;


        twistAxisParent = (restChildLocalRotation * twistLocal).normalized;
        swing1AxisParent = (restChildLocalRotation * swing1Local).normalized;
        swing2AxisParent = (restChildLocalRotation * swing2Local).normalized;
    }
}

public interface IXPBDPoseProvider
{
    void PrepareXPBDPose();
}

[RequireComponent(typeof(NetworkObject))]
public class XPBDPosAndRotSolver : NetworkBehaviour
{
    private Dictionary<Rigidbody, XPBDState> _stateByBody = new Dictionary<Rigidbody, XPBDState>();
    private List<XPBDState> _bodyStates = new List<XPBDState>();

    public IReadOnlyList<XPBDState> BodyStates => _bodyStates;

    [Header("Compliance curve 0 = 0 1 = 180 higher is weaker")]
    public AnimationCurve complianceCurve = new AnimationCurve(
        new Keyframe(0f, 10f),
        new Keyframe(1f, 1f)
    );

    [Header("Velocity Handoff")]
    [Min(0.01f)] public float maximumSolverAngularVelocity = 50f;
    [Header("Ragdoll Passive Resistance")]
    [Min(0f)] public float ragdollAngularResistance = 5f;

    [Header("Ragdoll Scale")]
    [Min(0.01f)] public float authoredScale = 1f;
    [Networked] private float NetworkedStartScale { get; set; }
    [Networked] private float NetworkedCurrentScale { get; set; }
    public float StartScale => _hasSpawned && NetworkedStartScale > 0f ? NetworkedStartScale : Mathf.Max(0.01f, authoredScale);
    public float CurrentScale => _hasSpawned && NetworkedCurrentScale > 0f ? NetworkedCurrentScale : StartScale;

    public float MuscleStrengthMultiplier { get; set; } = 1f;

    public Transform targetArmatureRoot;

    public bool showAngularLimits = false;

    [Header("Joints")]
    public List<XPBDTestJoint> joints = new List<XPBDTestJoint>();

    public bool isRagdolling = false;

    private XPBDGlobalManager _registeredManager;
    private IXPBDPoseProvider _poseProvider;
    private bool _hasSpawned;

    private float _inverseDeltaTimeSquared;
    private float _distanceComplianceScale;
    private float _angularComplianceScale;
    private float _distanceDampingScale;
    private float _angularDampingScale;

    public override void Spawned()
    {
        base.Spawned();

        _hasSpawned = true;
        CachePoseProvider();
        if (HasStateAuthority)
        {
            NetworkedStartScale = Mathf.Max(0.01f, authoredScale);
            NetworkedCurrentScale = NetworkedStartScale;
        }

        EnableNetworkSimulation();

        XPBDGlobalManager manager = GameController.Instance != null
            ? GameController.Instance.xPBDGlobalManager
            : null;

        if (manager == null)
        {
            Debug.LogError(
                $"[{nameof(XPBDPosAndRotSolver)}] Cannot register '{name}' because no " +
                $"{nameof(XPBDGlobalManager)} is available.",
                this);
            return;
        }
        BuildBodyStateCache();
        manager.RegisterRagdoll(this);
        _registeredManager = manager;

        foreach (XPBDTestJoint joint in joints)
        {
            if (joint.parent != null && joint.parent.TryGetComponent(out PhysicsObject parentObject)) parentObject.ragdollController = this;
            if (joint.child != null && joint.child.TryGetComponent(out PhysicsObject childObject)) childObject.ragdollController = this;

            if (joint.parent == null || joint.child == null) continue;

            Collider parentCollider = joint.parent.GetComponent<Collider>();
            Collider childCollider = joint.child.GetComponent<Collider>();

            if (parentCollider != null && childCollider != null) Physics.IgnoreCollision(parentCollider, childCollider, true);
        }
    }

    private void BuildBodyStateCache()
    {
        _bodyStates.Clear();
        _stateByBody.Clear();

        foreach (XPBDTestJoint joint in joints)
        {
            if (joint.parent != null)
            {
                if (!_stateByBody.TryGetValue(joint.parent, out XPBDState parentState))
                {
                    parentState = new XPBDState { rb = joint.parent };
                    _stateByBody.Add(joint.parent, parentState);
                    _bodyStates.Add(parentState);
                }

                joint.parentState = parentState;
            }

            if (joint.child != null)
            {
                if (!_stateByBody.TryGetValue(joint.child, out XPBDState childState))
                {
                    childState = new XPBDState { rb = joint.child };
                    _stateByBody.Add(joint.child, childState);
                    _bodyStates.Add(childState);
                }

                joint.childState = childState;
            }
        }
    }

    public bool TryGetBodyState(Rigidbody rb, out XPBDState state)
    {
        return _stateByBody.TryGetValue(rb, out state);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _hasSpawned = false;

        if (_registeredManager != null)
        {
            _registeredManager.UnregisterRagdoll(this);
            _registeredManager = null;
        }

        base.Despawned(runner, hasState);
    }

    private void CachePoseProvider()
    {
        _poseProvider = null;

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IXPBDPoseProvider provider)
            {
                _poseProvider = provider;
                return;
            }
        }
    }

    public void PreparePoseForSolve()
    {
        if (_poseProvider == null || _poseProvider as MonoBehaviour == null) CachePoseProvider();
        _poseProvider?.PrepareXPBDPose();
    }

    public bool TrySetCurrentScale(float scale)
    {
        if (!_hasSpawned || !HasStateAuthority) return false;

        NetworkedCurrentScale = Mathf.Max(0.01f, scale);
        return true;
    }

    private void EnableNetworkSimulation()
    {
        Runner.SetIsSimulated(Object, true);

        var simulatedObjects = new HashSet<NetworkObject>();

        foreach (var joint in joints)
        {
            SetBodySimulated(joint.parent, simulatedObjects);
            SetBodySimulated(joint.child, simulatedObjects);
        }
    }

    private void SetBodySimulated(Rigidbody rb, HashSet<NetworkObject> simulatedObjects)
    {
        if (rb == null || !rb.TryGetComponent(out NetworkObject networkObject))
            return;

        if (simulatedObjects.Add(networkObject))
            Runner.SetIsSimulated(networkObject, true);
    }



    public void InitializeStates(float dt, Dictionary<Rigidbody, XPBDState> globalStates)
    {
        float startScale = StartScale;

        _inverseDeltaTimeSquared = 1f / (dt * dt);
        _distanceComplianceScale = 1f / CustomPhysicsFormulas.CalculateScalePower(startScale, CustomPhysicsFormulas.DistanceConstraintStrengthExponent);
        _angularComplianceScale = 1f / CustomPhysicsFormulas.CalculateScalePower(startScale, CustomPhysicsFormulas.AngularConstraintStrengthExponent);
        _distanceDampingScale = CustomPhysicsFormulas.CalculateScalePower(startScale, CustomPhysicsFormulas.DistanceDampingExponent);
        _angularDampingScale = CustomPhysicsFormulas.CalculateScalePower(startScale, CustomPhysicsFormulas.AngularDampingExponent);

        for (int i = 0; i < _bodyStates.Count; i++)
        {
            XPBDState state = _bodyStates[i];
            Rigidbody rb = state.rb;
            rb.maxAngularVelocity = maximumSolverAngularVelocity;

            bool isKinematic = rb.isKinematic;
            Vector3 position = rb.worldCenterOfMass;
            Quaternion rotation = rb.rotation;
            Vector3 linearVelocity = rb.linearVelocity;
            Vector3 angularVelocity = rb.angularVelocity;

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
            globalStates[rb] = state;
        }

        foreach (var joint in joints)
        {
            joint.lambdaPosition = Vector3.zero;
            joint.lambdaRotation = Vector3.zero;
            joint.lambdaLimits = Vector3.zero;

            Vector3 parentScale = joint.parent.transform.localScale;
            Vector3 childScale = joint.child.transform.localScale;

            Vector3 parentScaleMultiplier = new Vector3(parentScale.x / joint.bakedParentScale.x, parentScale.y / joint.bakedParentScale.y,
                parentScale.z / joint.bakedParentScale.z);

            Vector3 childScaleMultiplier = new Vector3(childScale.x / joint.bakedChildScale.x, childScale.y / joint.bakedChildScale.y,
                childScale.z / joint.bakedChildScale.z);

            joint.scaledParentAnchorLocal = Vector3.Scale(joint.parentAnchorLocal, parentScaleMultiplier);
            joint.scaledChildAnchorLocal = Vector3.Scale(joint.childAnchorLocal, childScaleMultiplier);

            joint.targetLocalRotation = Quaternion.identity;

            if (joint.parentTarget != null && joint.childTarget != null)
            {
                Quaternion animatedTargetLocalRotation = Quaternion.Inverse(joint.parentTarget.rotation) * joint.childTarget.rotation;
                joint.targetLocalRotation = Quaternion.Inverse(joint.parentTargetToBodyRotation) * animatedTargetLocalRotation * joint.childTargetToBodyRotation;
            }

            joint.inverseRestChildLocalRotation = Quaternion.Inverse(joint.restChildLocalRotation);

            joint.distanceAlpha = joint.distanceCompliance * _distanceComplianceScale * _inverseDeltaTimeSquared;
            float scaledDistanceDamping = joint.distanceDamping * _distanceDampingScale;
            joint.distanceGamma = (joint.distanceAlpha * (0.5f * dt * scaledDistanceDamping)) / dt;

            joint.limitAlpha = joint.limitCompliance * _angularComplianceScale * _inverseDeltaTimeSquared;
            float scaledLimitDamping = joint.limitDamping * _angularDampingScale;
            joint.limitGamma = (joint.limitAlpha * (0.5f * dt * scaledLimitDamping)) / dt;

            joint.muscleAlphaBase = joint.muscleCompliance * _angularComplianceScale * _inverseDeltaTimeSquared;
            joint.muscleDampingBase = joint.muscleDamping * _angularDampingScale;
        }
    }

    /*private void AddStateIfMissing(Rigidbody rb, float dt, Dictionary<Rigidbody, XPBDState> globalStates)
    {
        if (rb == null || globalStates.ContainsKey(rb)) return;

        XPBDState state = new XPBDState
        {
            rb = rb,
            isKinematic = rb.isKinematic,
            invMass = rb.isKinematic ? 0f : 1f / rb.mass,
            invInertiaLocal = rb.isKinematic ? Vector3.zero : new Vector3(1f / rb.inertiaTensor.x, 1f / rb.inertiaTensor.y, 1f / rb.inertiaTensor.z),
            qInertia = rb.inertiaTensorRotation
        };

        state.p_prev = rb.position;
        state.q_prev = rb.rotation;

        if (!state.isKinematic)
        {
            state.p = rb.position + rb.linearVelocity * dt;
            Vector3 angVel = rb.angularVelocity;
            float angle = angVel.magnitude;
            state.q = (angle > 1e-6f) ? Quaternion.AngleAxis(angle * Mathf.Rad2Deg * dt, angVel / angle) * rb.rotation : rb.rotation;
        }
        else
        {
            state.p = rb.position;
            state.q = rb.rotation;
        }

        globalStates[rb] = state;
    }*/

    // 2. SOLVE CONSTRAINTS USING THE GLOBAL DICTIONARY
    public void SolveConstraints(float dt)
    {
        foreach (var joint in joints)
        {
            if (joint.isRagdollJoint && !isRagdolling) continue;
            SolveDistanceConstraint(joint);
            if (!isRagdolling) SolveRotationConstraint(joint, dt);
            SolveAngularLimitsConstraint(joint);
        }
    }

    private void SolveDistanceConstraint(XPBDTestJoint joint)
    {
        if (!joint.enablePosition) return;

        XPBDState pState = joint.parentState;
        XPBDState cState = joint.childState;

        if (pState.isKinematic && cState.isKinematic) return;

        Vector3 r0 = pState.q * (joint.scaledParentAnchorLocal - pState.centerOfMassOffsetLocal);
        Vector3 r1 = cState.q * (joint.scaledChildAnchorLocal - cState.centerOfMassOffsetLocal);

        Vector3 dir = (cState.p + r1) - (pState.p + r0);

        

        // --- ------------------------------- DYNAMIC TENSION INFLUENCE --------------------------
        float effectivePosInfluence = isRagdolling ? 1f : joint.parentPositionInfluence;

        if (!isRagdolling && joint.scaleForceByTension)
        {
            float stretch = dir.magnitude;
            if (stretch > joint.minTensionDistance)
            {
                float normalizedTension = Mathf.Clamp01((stretch - joint.minTensionDistance) / Mathf.Max(0.001f, joint.maxTensionDistance - joint.minTensionDistance));

                float curveVal = joint.tensionReleaseCurve.Evaluate(normalizedTension);
                effectivePosInfluence = Mathf.Lerp(joint.parentPositionInfluence, 1.0f, curveVal);
            }
        }
        // --------------------------------------

        XPBDMath.SolveSphericalPosition(pState, cState, r0, r1, dir, joint.distanceAlpha, joint.distanceGamma, ref joint.lambdaPosition,
    isRagdolling ? 1f : joint.leverArmScale, effectivePosInfluence);
    }

    private void SolveRotationConstraint(XPBDTestJoint joint, float dt)
    {
        if (!joint.enableRotation) return;

        XPBDState pState = joint.parentState;
        XPBDState cState = joint.childState;

        if (pState.isKinematic && cState.isKinematic) return;
        if (joint.parentTarget == null || joint.childTarget == null) return;

        float muscleStrength = Mathf.Max(0f, MuscleStrengthMultiplier);
        if (muscleStrength <= 0.0001f) return;


        Quaternion targetQ = pState.q * joint.targetLocalRotation;
        Vector3 rotationError = XPBDMath.GetRotationErrorVector(targetQ, cState.q, out float angleRad);

        float curveMultiplier = Mathf.Max(0.0001f, complianceCurve.Evaluate(Mathf.Clamp01(angleRad / Mathf.PI)));
        float complianceMultiplier = curveMultiplier / muscleStrength;
        float alpha = joint.muscleAlphaBase * complianceMultiplier;
        float scaledDamping = joint.muscleDampingBase / Mathf.Sqrt(Mathf.Max(0.0001f, complianceMultiplier));
        float gamma = (alpha * (0.5f * dt * scaledDamping)) / dt;

        XPBDMath.SolveSphericalRotation(pState, cState, rotationError, alpha, gamma, ref joint.lambdaRotation, isRagdolling ? 1 : joint.parentRotationInfluence);
    }

    private void SolveAngularLimitsConstraint(XPBDTestJoint joint)
    {
        if (!joint.enableAngularLimits) return;

        XPBDState pState = joint.parentState;
        XPBDState cState = joint.childState;

        if (pState.isKinematic && cState.isKinematic) return;

        // 1. Calculate how much the bone has bent away from the T-Pose Rest State
        Quaternion relRotNow = Quaternion.Inverse(pState.q) * cState.q;
        Quaternion rotDiff = relRotNow * joint.inverseRestChildLocalRotation;
        rotDiff = XPBDMath.NormalizeQuaternion(rotDiff);
        


        // 2. Test Twist Limits (X)
        if (joint.twistAxisParent.sqrMagnitude > 1e-6f)
            XPBDMath.SolveAngularLimit(pState, cState, rotDiff, joint.twistAxisParent, joint.twistLimits.x, joint.twistLimits.y, joint.limitAlpha, joint.limitGamma, ref joint.lambdaLimits.x, joint.parentRotationInfluence);

        if (joint.swing1AxisParent.sqrMagnitude > 1e-6f)
            XPBDMath.SolveAngularLimit(pState, cState, rotDiff, joint.swing1AxisParent, joint.swing1Limits.x, joint.swing1Limits.y, joint.limitAlpha, joint.limitGamma, ref joint.lambdaLimits.y, joint.parentRotationInfluence);

        if (joint.swing2AxisParent.sqrMagnitude > 1e-6f)
            XPBDMath.SolveAngularLimit(pState, cState, rotDiff, joint.swing2AxisParent, joint.swing2Limits.x, joint.swing2Limits.y, joint.limitAlpha, joint.limitGamma, ref joint.lambdaLimits.z, joint.parentRotationInfluence);
    }

    public void ApplyRagdollPassiveResistance(float dt)
    {
        float ragdollAmount = isRagdolling ? 1f : 1f - Mathf.Clamp01(MuscleStrengthMultiplier);

        if (ragdollAmount <= 0f || ragdollAngularResistance <= 0f || dt <= 0f) return;

        float dampingFraction = 1f - Mathf.Exp(-ragdollAngularResistance * ragdollAmount * dt);

        foreach (XPBDTestJoint joint in joints)
        {
            XPBDState parentState = joint.parentState;
            XPBDState childState = joint.childState;

            if (parentState.isKinematic && childState.isKinematic) continue;

            Vector3 relativeAngularVelocity = childState.w - parentState.w;
            float relativeSpeed = relativeAngularVelocity.magnitude;

            if (relativeSpeed < 0.0001f) continue;

            Vector3 axis = relativeAngularVelocity / relativeSpeed;
            Vector3 parentResponse = parentState.isKinematic ? Vector3.zero : XPBDMath.ApplyInvInertiaWorld(axis, parentState.q, parentState.qInertia, parentState.invInertiaLocal);
            Vector3 childResponse = childState.isKinematic ? Vector3.zero : XPBDMath.ApplyInvInertiaWorld(axis, childState.q, childState.qInertia, childState.invInertiaLocal);
            float effectiveInverseInertia = Vector3.Dot(axis, parentResponse + childResponse);

            if (effectiveInverseInertia < 0.000001f) continue;

            float angularImpulse = relativeSpeed * dampingFraction / effectiveInverseInertia;

            if (!parentState.isKinematic) parentState.w += parentResponse * angularImpulse;
            if (!childState.isKinematic) childState.w -= childResponse * angularImpulse;
        }
    }


    //Ragdoll logic (ie bonked ragdoll - when character have more bones when ragdolled) 

    public void SetRagdollState(bool active, bool snapToTargets)
    {
        isRagdolling = active;

        foreach (var joint in joints)
        {
            if (!joint.isRagdollJoint) continue;

            var rb = joint.child;
            var nrb = rb.gameObject.GetComponent<NetworkRigidbody3D>();
            var col = rb.GetComponent<Collider>();

            if (active) //if activating ragdoll /////////////////////////////////////////////////////////////////////////
            {
                if (col) col.enabled = true;
                if (nrb) nrb.RBIsKinematic = false;

                // PREDICTION INJECTION: Snap the physics bones to the current animation pose!
                if (snapToTargets && joint.childTarget != null)
                {
                    rb.position = joint.childTarget.position;
                    rb.rotation = joint.childTarget.rotation * joint.childTargetToBodyRotation;

                    if (nrb) nrb.Teleport(rb.position, rb.rotation);

                    if (joint.parent != null)
                    {
                        rb.linearVelocity = joint.parent.linearVelocity;
                        rb.angularVelocity = joint.parent.angularVelocity;
                    }
                }
            }
            else //if de-activating ragdoll /////////////////////////////////////////////////////////////////////////
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                if (col) col.enabled = false;
                if (nrb) nrb.RBIsKinematic = true;
            }
        }
    }

    //Baking and inspector -- find editor script for more inspector stuffs In editor file
    [ContextMenu("Bake All Joints From Targets")]
    public void BakeJointsFromTargets()
    {
        if (joints == null || joints.Count == 0) return;
        foreach (var joint in joints)
        {
            if (joint.parent == null || joint.child == null || joint.parentTarget == null || joint.childTarget == null) continue;
            Vector3 pivotWorld = joint.childTarget.position;
            joint.parentAnchorLocal = Quaternion.Inverse(joint.parent.rotation) * (pivotWorld - joint.parent.position);
            joint.childAnchorLocal = Quaternion.Inverse(joint.child.rotation) * (pivotWorld - joint.child.position);
            joint.bakedParentScale = joint.parent.transform.localScale;
            joint.bakedChildScale = joint.child.transform.localScale;
        }
    }

    public void BakeAllAngularLimits()
    {
        if (joints == null || joints.Count == 0) return;
        foreach (var joint in joints)
        {
            joint.BakeRestPose();
        }
    }

    public void AutoAlignJointAxis(XPBDTestJoint joint)
    {
        if (joint.parent == null || joint.child == null) return;

        Vector3 twistLocal = Vector3.up;

        if (joint.childTarget != null && joint.childTarget.childCount > 0)
        {
            Vector3 boneDirWorld = joint.childTarget.GetChild(0).position - joint.childTarget.position;
            twistLocal = joint.child.transform.InverseTransformDirection(boneDirWorld);
        }
        else if (joint.childTarget != null)
        {
            twistLocal = joint.child.transform.InverseTransformDirection(joint.childTarget.up);
        }

        joint.twistAxis = joint.GetClosestAxis(twistLocal);

        Vector3 fwdLocal = joint.child.transform.InverseTransformDirection(this.transform.forward);

        joint.forwardAxis = joint.GetClosestAxis(fwdLocal, true, joint.twistAxis);

        joint.RecalculateAxes();
    }

    public void AutoAlignAllAxes()
    {
        if (joints == null || joints.Count == 0) return;

        foreach (var joint in joints)
        {
            AutoAlignJointAxis(joint);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (joints == null) return;
        if (showAngularLimits == false) return;

        foreach (var joint in joints)
        {
            if (!joint.enableAngularLimits || !joint.drawLimitGizmos) continue;
            if (joint.parent == null || joint.child == null) continue;

            joint.RecalculateAxes();
            if (joint.twistAxisParent == Vector3.zero) continue;

            Vector3 pivotWorld = joint.child.transform.position;
            if (joint.childAnchorLocal != Vector3.zero)
                pivotWorld = joint.child.transform.TransformPoint(joint.childAnchorLocal);

            Quaternion parentRot = joint.parent.rotation;

            Vector3 twistWorld = (parentRot * joint.twistAxisParent).normalized;
            Vector3 swing1World = (parentRot * joint.swing1AxisParent).normalized;
            Vector3 swing2World = (parentRot * joint.swing2AxisParent).normalized;

            float size = joint.gizmoSize;

            // Base Axes
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pivotWorld, pivotWorld + twistWorld * size * 1.3f);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(pivotWorld, pivotWorld + swing1World * size);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(pivotWorld, pivotWorld + swing2World * size);

            // Limit Arcs
            DrawArc(pivotWorld, twistWorld, swing1World, joint.twistLimits.x, joint.twistLimits.y, size * 0.9f, Color.red);
            DrawArc(pivotWorld, swing1World, twistWorld, joint.swing1Limits.x, joint.swing1Limits.y, size * 0.8f, Color.green);
            DrawArc(pivotWorld, swing2World, twistWorld, joint.swing2Limits.x, joint.swing2Limits.y, size * 0.8f, Color.blue);

            // --- THE FIX: CURRENT POSE NEEDLES ---
#if UNITY_EDITOR
            // Get the child's actual local axes
            Vector3 twistLocal = joint.GetAxisVector(joint.twistAxis);
            Vector3 forwardLocal = joint.GetAxisVector(joint.forwardAxis);
            if (Mathf.Abs(Vector3.Dot(twistLocal, forwardLocal)) > 0.99f) forwardLocal = twistLocal.x == 0 ? Vector3.right : Vector3.up;
            Vector3 swing1Local = Vector3.Cross(twistLocal, forwardLocal).normalized;
            if (joint.isMirroredBasis)
            {
                swing1Local = -swing1Local;
            }
            // Convert to World Space based on the child's CURRENT rotation
            Vector3 childTwistWorld = joint.child.rotation * twistLocal;
            Vector3 childSwing1World = joint.child.rotation * swing1Local;

            // Project them perfectly onto the arc planes
            Vector3 twistNeedle = Vector3.ProjectOnPlane(childSwing1World, twistWorld).normalized;
            Vector3 swing1Needle = Vector3.ProjectOnPlane(childTwistWorld, swing1World).normalized;
            Vector3 swing2Needle = Vector3.ProjectOnPlane(childTwistWorld, swing2World).normalized;

            // Draw bold 5px lines acting as dials!
            Handles.color = Color.red;
            Handles.DrawAAPolyLine(5f, pivotWorld, pivotWorld + twistNeedle * size * 0.9f);

            Handles.color = Color.green;
            Handles.DrawAAPolyLine(5f, pivotWorld, pivotWorld + swing1Needle * size * 0.8f);

            Handles.color = Color.blue;
            Handles.DrawAAPolyLine(5f, pivotWorld, pivotWorld + swing2Needle * size * 0.8f);
#endif
        }
    }
    private void DrawArc(Vector3 center, Vector3 axis, Vector3 startDir, float angleMin, float angleMax, float radius, Color color)
    {
        Gizmos.color = color;
        int segments = 16;
        float step = (angleMax - angleMin) / segments;

        Vector3 prev = center + Quaternion.AngleAxis(angleMin, axis) * (startDir.normalized * radius);

        for (int i = 1; i <= segments; i++)
        {
            float a = angleMin + step * i;
            Vector3 next = center + Quaternion.AngleAxis(a, axis) * (startDir.normalized * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
