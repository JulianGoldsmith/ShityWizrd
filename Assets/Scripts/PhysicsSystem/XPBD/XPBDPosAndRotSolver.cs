using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;
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
    [HideInInspector] public Vector3 bakedParentScale = Vector3.one;
    [HideInInspector] public Vector3 bakedChildScale = Vector3.one;

    [HideInInspector] public Quaternion restChildLocalRotation = Quaternion.identity;
    [HideInInspector] public Vector3 twistAxisParent;
    [HideInInspector] public Vector3 swing1AxisParent;
    [HideInInspector] public Vector3 swing2AxisParent;

    [Range(0f, 2f)] public float leverArmScale = 1f;           
    [Range(0f, 1f)] public float parentRotationInfluence = 1f;

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
        if (parentTarget != null && childTarget != null && parent != null && child != null)
        {
            // 1. Find the constant rotational offset between the Visuals and the Rigidbodies
            Quaternion parentOffset = Quaternion.Inverse(parentTarget.rotation) * parent.rotation;
            Quaternion childOffset = Quaternion.Inverse(childTarget.rotation) * child.rotation;

            // 2. Capture the pristine visual relative rest pose
            Quaternion visRest = Quaternion.Inverse(parentTarget.rotation) * childTarget.rotation;

            // 3. Transform the Visual Rest Pose into pure Rigidbody Local Space!
            restChildLocalRotation = Quaternion.Inverse(parentOffset) * visRest * childOffset;
        }
        else if (parent != null && child != null)
        {
            // Fallback if no visuals are assigned
            restChildLocalRotation = Quaternion.Inverse(parent.rotation) * child.rotation;
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

public class XPBDPosAndRotSolver : MonoBehaviour
{
    [Header("Compliance curve 0 = 0 1 = 180 higher is weaker")]
    public AnimationCurve complianceCurve = new AnimationCurve(
        new Keyframe(0f, 10f),
        new Keyframe(1f, 1f)
    );

    public Transform targetArmatureRoot;

    [Header("Joints")]
    public List<XPBDTestJoint> joints = new List<XPBDTestJoint>();

    public bool isRagdolling = false;

    // 1. POPULATE THE SHARED GLOBAL DICTIONARY
    public void InitializeStates(float dt, Dictionary<Rigidbody, XPBDState> globalStates)
    {
        foreach (var joint in joints)
        {
            joint.lambdaPosition = Vector3.zero;
            joint.lambdaRotation = Vector3.zero;
            joint.lambdaLimits = Vector3.zero;

            AddStateIfMissing(joint.parent, dt, globalStates);
            AddStateIfMissing(joint.child, dt, globalStates);
        }
    }

    private void AddStateIfMissing(Rigidbody rb, float dt, Dictionary<Rigidbody, XPBDState> globalStates)
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
    }

    // 2. SOLVE CONSTRAINTS USING THE GLOBAL DICTIONARY
    public void SolveConstraints(float dt, Dictionary<Rigidbody, XPBDState> globalStates)
    {
        foreach (var joint in joints)
        {
            if (joint.isRagdollJoint && !isRagdolling) continue;
            SolveDistanceConstraint(joint, dt, globalStates);
            if(!isRagdolling)
                SolveRotationConstraint(joint, dt, globalStates);
            SolveAngularLimitsConstraint(joint, dt, globalStates);
        }
    }

    private void SolveDistanceConstraint(XPBDTestJoint joint, float dt, Dictionary<Rigidbody, XPBDState> states)
    {
        if(!joint.enablePosition) return;
        var pState = states[joint.parent];
        var cState = states[joint.child];
        if (pState.isKinematic && cState.isKinematic) return;

        Vector3 pScaleMod = new Vector3(pState.rb.transform.localScale.x / joint.bakedParentScale.x, pState.rb.transform.localScale.y / joint.bakedParentScale.y, pState.rb.transform.localScale.z / joint.bakedParentScale.z);
        Vector3 cScaleMod = new Vector3(cState.rb.transform.localScale.x / joint.bakedChildScale.x, cState.rb.transform.localScale.y / joint.bakedChildScale.y, cState.rb.transform.localScale.z / joint.bakedChildScale.z);

        Vector3 r0 = pState.q * Vector3.Scale(joint.parentAnchorLocal, pScaleMod);
        Vector3 r1 = cState.q * Vector3.Scale(joint.childAnchorLocal, cScaleMod);
        Vector3 dir = (cState.p + r1) - (pState.p + r0);

        float alpha = joint.distanceCompliance / (dt * dt);
        float gamma = (alpha * (0.5f * dt * joint.distanceDamping)) / dt;

        XPBDMath.SolveSphericalPosition(pState, cState, r0, r1, dir, alpha, gamma, ref joint.lambdaPosition, isRagdolling ? 1: joint.leverArmScale);
    }

    private void SolveRotationConstraint(XPBDTestJoint joint, float dt, Dictionary<Rigidbody, XPBDState> states)
    {
        if (!joint.enableRotation) return;
        var pState = states[joint.parent];
        var cState = states[joint.child];
        if (pState.isKinematic && cState.isKinematic) return;
        if (joint.parentTarget == null || joint.childTarget == null) return;

        Quaternion targetLocalRotation = Quaternion.Inverse(joint.parentTarget.rotation) * joint.childTarget.rotation;
        Quaternion targetQ = pState.q * targetLocalRotation;

        Quaternion qError = targetQ * Quaternion.Inverse(cState.q);
        if (qError.w < 0f) { qError.x = -qError.x; qError.y = -qError.y; qError.z = -qError.z; qError.w = -qError.w; }
        float angleRad = 2f * Mathf.Atan2(new Vector3(qError.x, qError.y, qError.z).magnitude, qError.w);

        float curveMultiplier = complianceCurve.Evaluate(Mathf.Clamp01(angleRad / Mathf.PI));
        float alpha = (joint.muscleCompliance * curveMultiplier) / (dt * dt);
        float gamma = (alpha * (0.5f * dt * joint.muscleDamping)) / dt;

        XPBDMath.SolveSphericalRotation(pState, cState, targetQ, alpha, gamma, ref joint.lambdaRotation, isRagdolling ? 1 : joint.parentRotationInfluence);
    }

    private void SolveAngularLimitsConstraint(XPBDTestJoint joint, float dt, Dictionary<Rigidbody, XPBDState> states)
    {
        if (!joint.enableAngularLimits) return;
        var pState = states[joint.parent];
        var cState = states[joint.child];
        if (pState.isKinematic && cState.isKinematic) return;

        // 1. Calculate how much the bone has bent away from the T-Pose Rest State
        Quaternion relRotNow = Quaternion.Inverse(pState.q) * cState.q;
        Quaternion rotDiff = relRotNow * Quaternion.Inverse(joint.restChildLocalRotation);
        rotDiff = XPBDMath.NormalizeQuaternion(rotDiff);

        // 2. Test Twist Limits (X)
        if (joint.twistAxisParent.sqrMagnitude > 1e-6f)
            XPBDMath.SolveAngularLimit(pState, cState, rotDiff, joint.twistAxisParent, joint.twistLimits.x, joint.twistLimits.y, joint.limitCompliance, joint.limitDamping, dt, ref joint.lambdaLimits.x, joint.parentRotationInfluence);

        if (joint.swing1AxisParent.sqrMagnitude > 1e-6f)
            XPBDMath.SolveAngularLimit(pState, cState, rotDiff, joint.swing1AxisParent, joint.swing1Limits.x, joint.swing1Limits.y, joint.limitCompliance, joint.limitDamping, dt, ref joint.lambdaLimits.y, joint.parentRotationInfluence);

        if (joint.swing2AxisParent.sqrMagnitude > 1e-6f)
            XPBDMath.SolveAngularLimit(pState, cState, rotDiff, joint.swing2AxisParent, joint.swing2Limits.x, joint.swing2Limits.y, joint.limitCompliance, joint.limitDamping, dt, ref joint.lambdaLimits.z, joint.parentRotationInfluence);
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
                    rb.rotation = joint.childTarget.rotation;

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