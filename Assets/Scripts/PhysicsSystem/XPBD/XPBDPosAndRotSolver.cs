using System.Collections.Generic;
using UnityEngine;




[System.Serializable]
public class XPBDTestJoint
{
    public Rigidbody parent;
    public Rigidbody child;

    //[Header("Distance Constraint")]
    public Vector3 parentAnchorLocal;
    public Vector3 childAnchorLocal;
    public float distanceCompliance = 0f;
    public float distanceDamping = 0f;

    //[Header("Animation Targets")]
    public Transform parentTarget;
    public Transform childTarget;

    //[Header("Muscle Constraint")]
    public float muscleCompliance = 0.05f;
    public float muscleDamping = 10f;

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
        new Keyframe(0f, 1f),
        new Keyframe(1f, 10f)
    );

    [Header("Joints")]
    public List<XPBDTestJoint> joints = new List<XPBDTestJoint>();

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
            SolveDistanceConstraint(joint, dt, globalStates);
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

        XPBDMath.SolveSphericalPosition(pState, cState, r0, r1, dir, alpha, gamma, ref joint.lambdaPosition);
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

        XPBDMath.SolveSphericalRotation(pState, cState, targetQ, alpha, gamma, ref joint.lambdaRotation);
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
            XPBDMath.SolveAngularLimit(pState, cState, rotDiff, joint.twistAxisParent, joint.twistLimits.x, joint.twistLimits.y, joint.limitCompliance, joint.limitDamping, dt, ref joint.lambdaLimits.x);

        // 3. Test Swing 1 Limits (Y)
        if (joint.swing1AxisParent.sqrMagnitude > 1e-6f)
            XPBDMath.SolveAngularLimit(pState, cState, rotDiff, joint.swing1AxisParent, joint.swing1Limits.x, joint.swing1Limits.y, joint.limitCompliance, joint.limitDamping, dt, ref joint.lambdaLimits.y);

        // 4. Test Swing 2 Limits (Z)
        if (joint.swing2AxisParent.sqrMagnitude > 1e-6f)
            XPBDMath.SolveAngularLimit(pState, cState, rotDiff, joint.swing2AxisParent, joint.swing2Limits.x, joint.swing2Limits.y, joint.limitCompliance, joint.limitDamping, dt, ref joint.lambdaLimits.z);
    }

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

    public void AutoAlignAllAxes()
    {
        if (joints == null || joints.Count == 0) return;

        Vector3 globalForwardRef = joints[0].child.transform.forward;

        foreach (var joint in joints)
        {
            if (joint.parent == null || joint.child == null) continue;

            // 2. Calculate Twist Direction (General direction from Parent to Child)
            Vector3 boneDirWorld = (joint.child.position - joint.parent.position).normalized;
            if (boneDirWorld.sqrMagnitude < 0.001f) boneDirWorld = joint.child.transform.up; // Fallback for zero-length bones

            // Convert world direction to the child's local space to find the matching Enum
            Vector3 twistLocal = joint.child.transform.InverseTransformDirection(boneDirWorld);
            joint.twistAxis = joint.GetClosestAxis(twistLocal);

            // 3. Calculate Forward Direction
            // Convert the first bone's global forward into THIS bone's local space
            Vector3 fwdLocal = joint.child.transform.InverseTransformDirection(globalForwardRef);

            // Find the closest axis, explicitly excluding the Twist axis we just picked!
            joint.forwardAxis = joint.GetClosestAxis(fwdLocal, true, joint.twistAxis);

            // 4. Immediately apply to update Gizmos!
            joint.RecalculateAxes();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (joints == null) return;

        foreach (var joint in joints)
        {
            if (!joint.enableAngularLimits || !joint.drawLimitGizmos) continue;
            if (joint.parent == null || joint.child == null) continue;

            // ---> INSTANT UPDATE: Recalculate the axes live based on inspector enums!
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

            Gizmos.color = Color.red;
            Gizmos.DrawLine(pivotWorld, pivotWorld + twistWorld * size * 1.3f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(pivotWorld, pivotWorld + swing1World * size);

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(pivotWorld, pivotWorld + swing2World * size);

            DrawArc(pivotWorld, twistWorld, swing1World, joint.twistLimits.x, joint.twistLimits.y, size * 0.9f, Color.red);
            DrawArc(pivotWorld, swing1World, twistWorld, joint.swing1Limits.x, joint.swing1Limits.y, size * 0.8f, Color.green);
            DrawArc(pivotWorld, swing2World, twistWorld, joint.swing2Limits.x, joint.swing2Limits.y, size * 0.8f, Color.blue);
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