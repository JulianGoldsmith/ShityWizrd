using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class XPBDTestJoint
{
    public Rigidbody parent;
    public Rigidbody child;

    [Header("Distance Constraint")]
    public Vector3 parentAnchorLocal;
    public Vector3 childAnchorLocal;
    public float distanceCompliance = 0f;
    public float distanceDamping = 0f;

    [Header("Animation Targets")]
    public Transform parentTarget;
    public Transform childTarget;

    [Header("Muscle Constraint")]
    public float muscleCompliance = 0.05f;
    public float muscleDamping = 10f;

    [HideInInspector] public Vector3 lambdaPosition;
    [HideInInspector] public Vector3 lambdaRotation;
    [HideInInspector] public Vector3 bakedParentScale = Vector3.one;
    [HideInInspector] public Vector3 bakedChildScale = Vector3.one;
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
        }
    }

    private void SolveDistanceConstraint(XPBDTestJoint joint, float dt, Dictionary<Rigidbody, XPBDState> states)
    {
        var pState = states[joint.parent];
        var cState = states[joint.child];

        if (pState.isKinematic && cState.isKinematic) return;

        Vector3 pScaleMod = new Vector3(
            pState.rb.transform.localScale.x / joint.bakedParentScale.x,
            pState.rb.transform.localScale.y / joint.bakedParentScale.y,
            pState.rb.transform.localScale.z / joint.bakedParentScale.z);

        Vector3 cScaleMod = new Vector3(
            cState.rb.transform.localScale.x / joint.bakedChildScale.x,
            cState.rb.transform.localScale.y / joint.bakedChildScale.y,
            cState.rb.transform.localScale.z / joint.bakedChildScale.z);

        Vector3 r0 = pState.q * Vector3.Scale(joint.parentAnchorLocal, pScaleMod);
        Vector3 r1 = cState.q * Vector3.Scale(joint.childAnchorLocal, cScaleMod);

        Vector3 p0 = pState.p + r0;
        Vector3 p1 = cState.p + r1;
        Vector3 dir = p1 - p0;

        float alpha = joint.distanceCompliance / (dt * dt);
        float betaTilde = 0.5f * dt * joint.distanceDamping;
        float gamma = (alpha * betaTilde) / dt;

        Vector3 dx0 = pState.p - pState.p_prev, dw0 = XPBDMath.GetDeltaTheta(pState.q_prev, pState.q);
        Vector3 dx1 = cState.p - cState.p_prev, dw1 = XPBDMath.GetDeltaTheta(cState.q_prev, cState.q);

        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = axes[i];
            float C = Vector3.Dot(dir, cAxis);

            Vector3 gradP0 = -cAxis;
            Vector3 gradP1 = cAxis;
            Vector3 gradQ0 = Vector3.Cross(r0, gradP0);
            Vector3 gradQ1 = Vector3.Cross(r1, gradP1);

            float w0 = pState.isKinematic ? 0f : pState.invMass + Vector3.Dot(gradQ0, XPBDMath.ApplyInvInertiaWorld(gradQ0, pState.q, pState.qInertia, pState.invInertiaLocal));
            float w1 = cState.isKinematic ? 0f : cState.invMass + Vector3.Dot(gradQ1, XPBDMath.ApplyInvInertiaWorld(gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));

            float wSum = w0 + w1;
            if (wSum < 1e-6f) continue;

            float dC = Vector3.Dot(gradP0, dx0) + Vector3.Dot(gradP1, dx1) + Vector3.Dot(gradQ0, dw0) + Vector3.Dot(gradQ1, dw1);

            float currentLambda = i == 0 ? joint.lambdaPosition.x : (i == 1 ? joint.lambdaPosition.y : joint.lambdaPosition.z);
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * wSum + alpha);

            if (i == 0) joint.lambdaPosition.x += deltaLambda;
            else if (i == 1) joint.lambdaPosition.y += deltaLambda;
            else joint.lambdaPosition.z += deltaLambda;

            if (!pState.isKinematic)
            {
                pState.p += pState.invMass * deltaLambda * gradP0;
                XPBDMath.ApplyDeltaRotation(pState, XPBDMath.ApplyInvInertiaWorld(deltaLambda * gradQ0, pState.q, pState.qInertia, pState.invInertiaLocal));
            }
            if (!cState.isKinematic)
            {
                cState.p += cState.invMass * deltaLambda * gradP1;
                XPBDMath.ApplyDeltaRotation(cState, XPBDMath.ApplyInvInertiaWorld(deltaLambda * gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));
            }
        }
    }

    private void SolveRotationConstraint(XPBDTestJoint joint, float dt, Dictionary<Rigidbody, XPBDState> states)
    {
        var pState = states[joint.parent];
        var cState = states[joint.child];

        if (pState.isKinematic && cState.isKinematic) return;
        if (joint.parentTarget == null || joint.childTarget == null) return;

        Quaternion targetLocalRotation = Quaternion.Inverse(joint.parentTarget.rotation) * joint.childTarget.rotation;
        Quaternion targetQ = pState.q * targetLocalRotation;
        Quaternion qError = targetQ * Quaternion.Inverse(cState.q);

        if (qError.w < 0f) { qError.x = -qError.x; qError.y = -qError.y; qError.z = -qError.z; qError.w = -qError.w; }

        Vector3 v = new Vector3(qError.x, qError.y, qError.z);
        float sinHalfAngle = v.magnitude;
        if (sinHalfAngle < 1e-6f) return;

        Vector3 axis = v / sinHalfAngle;
        float angleRad = 2f * Mathf.Atan2(sinHalfAngle, qError.w);

        float curveMultiplier = complianceCurve.Evaluate(Mathf.Clamp01(angleRad / Mathf.PI));
        float alpha = (joint.muscleCompliance * curveMultiplier) / (dt * dt);
        float betaTilde = 0.5f * dt * joint.muscleDamping;
        float gamma = (alpha * betaTilde) / dt;

        Vector3 rotVec = axis * angleRad;
        Vector3[] orthogonalAxes = { Vector3.right, Vector3.up, Vector3.forward };

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = orthogonalAxes[i];
            float C = Vector3.Dot(rotVec, cAxis);

            float w0 = pState.isKinematic ? 0f : Vector3.Dot(cAxis, XPBDMath.ApplyInvInertiaWorld(cAxis, pState.q, pState.qInertia, pState.invInertiaLocal));
            float w1 = cState.isKinematic ? 0f : Vector3.Dot(-cAxis, XPBDMath.ApplyInvInertiaWorld(-cAxis, cState.q, cState.qInertia, cState.invInertiaLocal));
            float wSum = w0 + w1;
            if (wSum < 1e-6f) continue;

            float dC = Vector3.Dot(cAxis, XPBDMath.GetDeltaTheta(pState.q_prev, pState.q)) + Vector3.Dot(-cAxis, XPBDMath.GetDeltaTheta(cState.q_prev, cState.q));
            float currentLambda = i == 0 ? joint.lambdaRotation.x : (i == 1 ? joint.lambdaRotation.y : joint.lambdaRotation.z);

            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * wSum + alpha);

            if (i == 0) joint.lambdaRotation.x += deltaLambda; else if (i == 1) joint.lambdaRotation.y += deltaLambda; else joint.lambdaRotation.z += deltaLambda;

            if (!pState.isKinematic) XPBDMath.ApplyDeltaRotation(pState, XPBDMath.ApplyInvInertiaWorld(deltaLambda * cAxis, pState.q, pState.qInertia, pState.invInertiaLocal));
            if (!cState.isKinematic) XPBDMath.ApplyDeltaRotation(cState, XPBDMath.ApplyInvInertiaWorld(deltaLambda * -cAxis, cState.q, cState.qInertia, cState.invInertiaLocal));
        }
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
}