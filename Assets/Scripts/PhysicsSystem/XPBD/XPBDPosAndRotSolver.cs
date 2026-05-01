using System.Collections.Generic;
using UnityEngine;
using Fusion;
using UnityEngine.PlayerLoop;

[System.Serializable]
public class XPBDTestJoint
{
    public Rigidbody parent;
    public Rigidbody child;

    [Header("Distance Constraint")]
    public Vector3 parentAnchorLocal;
    public Vector3 childAnchorLocal;

    [Tooltip("Compliance. 0 = rigid wire, 0.1+ = springy")]
    public float distanceCompliance = 0f;
    [Tooltip("Damping coefficient (Rayleigh). 0 = no friction, 10+ = heavy dampening")]
    public float distanceDamping = 0f;

    [Header("Animation Targets")]
    [Tooltip("The animated ghost bone that matches the Parent Rigidbody (e.g., Torso)")]
    public Transform parentTarget;
    [Tooltip("The animated ghost bone that matches the Child Rigidbody (e.g., Upper Arm)")]
    public Transform childTarget;

    [Header("Muscle Constraint")]
    [Tooltip("Compliance. 0 = rigid robot, 0.1+ = bouncy/soft muscle")]
    public float muscleCompliance = 0.05f;
    [Tooltip("Damping coefficient (Rayleigh). 0 = wobbles, 10+ = smooth stop")]
    public float muscleDamping = 10f;

    // --- THE XPBD MEMORY ---
    [HideInInspector] public float lambdaDistance;
    [HideInInspector] public Vector3 lambdaRotation;

    [HideInInspector] public Vector3 bakedParentScale = Vector3.one;
    [HideInInspector] public Vector3 bakedChildScale = Vector3.one;
}

public class XPBDState
{
    public Rigidbody rb;

    // Previous State (x_n)
    public Vector3 p_prev;
    public Quaternion q_prev;

    // Predicted/Solved State (x_n+1)
    public Vector3 p;
    public Quaternion q;

    public Vector3 v;
    public Vector3 w;

    public float invMass;
    public Vector3 invInertiaLocal;
    public Quaternion qInertia;

    public bool isKinematic;
}

public class XPBDPosAndRotSolver : MonoBehaviour
{
    [Header("Global Settings")]
    public int iterations = 4;
    public bool enableSolver = true;

    [Header("Compliance curve 0 = 0 1 = 180 higher is weaker")]
    public AnimationCurve complianceCurve = new AnimationCurve(
        new Keyframe(0f, 1f),    // At 0 degrees error, multiplier is 1x
        new Keyframe(1f, 10f)    // At 180 degrees error, multiplier is 10x (much softer)
    );

    [Header("Joints")]
    public List<XPBDTestJoint> joints = new List<XPBDTestJoint>();

    private Dictionary<Rigidbody, XPBDState> _states = new Dictionary<Rigidbody, XPBDState>();

    void FixedUpdate()
    {
        if (!enableSolver || joints.Count == 0) return;
        //SolveJointTick(Time.fixedDeltaTime);
    }

    public void SolveJointTick(float dt)
    {
        if (!enableSolver || joints.Count == 0) return;
        if (dt <= 0f) return;

        // 1. PREDICT & RESET LAMBDAS
        InitializeStates(dt);
        foreach (var joint in joints)
        {
            joint.lambdaDistance = 0f;
            joint.lambdaRotation = Vector3.zero;
        }

        /*float lambdaDecay = 0.98f;

        foreach (var joint in joints)
        {
            joint.lambdaDistance *= lambdaDecay;
            joint.lambdaRotation *= lambdaDecay;
        }*/

        // 2. TRUE XPBD IMPLICIT SOLVER (Damping inside the loop)
        for (int i = 0; i < iterations; i++)
        {
            foreach (var joint in joints)
            {
                SolveDistanceConstraint(joint, dt);
                SolveRotationConstraint(joint, dt);
            }
        }

        // 3. DERIVE FINAL VELOCITIES & APPLY TO UNITY
        DeriveVelocities(dt);
        ApplyStatesToRigidbodies();
    }

    private void InitializeStates(float dt)
    {
        _states.Clear();
        foreach (var joint in joints)
        {
            AddStateIfMissing(joint.parent, dt);
            AddStateIfMissing(joint.child, dt);
        }
    }

    private void AddStateIfMissing(Rigidbody rb, float dt)
    {
        if (rb == null || _states.ContainsKey(rb)) return;

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
            if (angle > 1e-6f)
                state.q = Quaternion.AngleAxis(angle * Mathf.Rad2Deg * dt, angVel / angle) * rb.rotation;
            else
                state.q = rb.rotation;
        }
        else
        {
            state.p = rb.position;
            state.q = rb.rotation;
        }

        _states[rb] = state;
    }

    private void SolveDistanceConstraint(XPBDTestJoint joint, float dt)
    {
        var pState = _states[joint.parent];
        var cState = _states[joint.child];

        if (pState.isKinematic && cState.isKinematic) return;

        Vector3 pScaleMod = new Vector3(
            pState.rb.transform.localScale.x / joint.bakedParentScale.x,
            pState.rb.transform.localScale.y / joint.bakedParentScale.y,
            pState.rb.transform.localScale.z / joint.bakedParentScale.z
        );

        Vector3 cScaleMod = new Vector3(
            cState.rb.transform.localScale.x / joint.bakedChildScale.x,
            cState.rb.transform.localScale.y / joint.bakedChildScale.y,
            cState.rb.transform.localScale.z / joint.bakedChildScale.z
        );

        // Apply ONLY the relative modifier to the anchors
        Vector3 r0 = pState.q * Vector3.Scale(joint.parentAnchorLocal, pScaleMod);
        Vector3 r1 = cState.q * Vector3.Scale(joint.childAnchorLocal, cScaleMod);


        Vector3 p0 = pState.p + r0;
        Vector3 p1 = cState.p + r1;

        Vector3 dir = p1 - p0;
        float dist = dir.magnitude;

        if (dist < 1e-6f) return;
        Vector3 n = dir / dist;

        // Gradients
        Vector3 gradP0 = -n;
        Vector3 gradP1 = n;
        Vector3 gradQ0 = Vector3.Cross(r0, gradP0);
        Vector3 gradQ1 = Vector3.Cross(r1, gradP1);

        // Effective weights
        float w0 = 0f;
        if (!pState.isKinematic)
        {
            w0 = pState.invMass +
                 Vector3.Dot(gradQ0, ApplyInvInertiaWorld(gradQ0, pState.q, pState.qInertia, pState.invInertiaLocal));
        }

        float w1 = 0f;
        if (!cState.isKinematic)
        {
            w1 = cState.invMass +
                 Vector3.Dot(gradQ1, ApplyInvInertiaWorld(gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));
        }

        float wSum = w0 + w1;
        if (wSum < 1e-6f) return;

        // Constraint velocity term: Cdot ≈ J (x_i - x_n) / dt
        Vector3 dx0 = pState.p - pState.p_prev;
        Vector3 dw0 = GetDeltaTheta(pState.q_prev, pState.q);
        Vector3 dx1 = cState.p - cState.p_prev;
        Vector3 dw1 = GetDeltaTheta(cState.q_prev, cState.q);

        float dC =
            Vector3.Dot(gradP0, dx0) +
            Vector3.Dot(gradQ0, dw0) +
            Vector3.Dot(gradP1, dx1) +
            Vector3.Dot(gradQ1, dw1);

        // XPBD compliance term
        float alpha = joint.distanceCompliance / (dt * dt);

        // Rayleigh damping stiffness (paper-style scaling)
        float beta = joint.distanceDamping;
        float betaTilde = 0.5f * dt * beta;
        float gamma = (alpha * betaTilde) / dt;

        // Standard XPBD damping form
        float denom = (1f + gamma) * wSum + alpha;
        float deltaLambda = -(dist + alpha * joint.lambdaDistance + gamma * dC) / denom;

        joint.lambdaDistance += deltaLambda;

        if (!pState.isKinematic)
        {
            pState.p += pState.invMass * deltaLambda * gradP0;
            ApplyDeltaRotation(pState, ApplyInvInertiaWorld(deltaLambda * gradQ0, pState.q, pState.qInertia, pState.invInertiaLocal));
        }

        if (!cState.isKinematic)
        {
            cState.p += cState.invMass * deltaLambda * gradP1;
            ApplyDeltaRotation(cState, ApplyInvInertiaWorld(deltaLambda * gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));
        }
    }

    private void SolveRotationConstraint(XPBDTestJoint joint, float dt)
    {
        var pState = _states[joint.parent];
        var cState = _states[joint.child];

        if (pState.isKinematic && cState.isKinematic) return;

        if (joint.parentTarget == null || joint.childTarget == null) return;

        Quaternion targetLocalRotation = Quaternion.Inverse(joint.parentTarget.rotation) * joint.childTarget.rotation;

        Quaternion targetQ = pState.q * targetLocalRotation;
        Quaternion qError = targetQ * Quaternion.Inverse(cState.q);

        if (qError.w < 0f)
        {
            qError.x = -qError.x;
            qError.y = -qError.y;
            qError.z = -qError.z;
            qError.w = -qError.w;
        }

        Vector3 v = new Vector3(qError.x, qError.y, qError.z);
        float sinHalfAngle = v.magnitude;

        if (sinHalfAngle < 1e-6f) return;

        Vector3 axis = v / sinHalfAngle;
        float angleRad = 2f * Mathf.Atan2(sinHalfAngle, qError.w);

        float normalizedError = Mathf.Clamp01(angleRad / Mathf.PI);

        // 2. Evaluate the user's custom curve
        float curveMultiplier = complianceCurve.Evaluate(normalizedError);

        // 3. Apply the multiplier to the base compliance
        float dynamicCompliance = joint.muscleCompliance * curveMultiplier;
        float alpha = dynamicCompliance / (dt * dt);

        // --- 3D ORTHOGONAL SOLVER (Kills the Orbit) ---
        // Instead of 1 arbitrary axis, we project the 3D error onto X, Y, and Z axes
        Vector3 rotVec = axis * angleRad;
        Vector3[] orthogonalAxes = { Vector3.right, Vector3.up, Vector3.forward };

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = orthogonalAxes[i];
            float C = Vector3.Dot(rotVec, cAxis); // The error isolated to this specific world axis

            Vector3 gradQ0 = cAxis;
            Vector3 gradQ1 = -cAxis;

            float w0 = 0f;
            if (!pState.isKinematic) w0 = Vector3.Dot(gradQ0, ApplyInvInertiaWorld(gradQ0, pState.q, pState.qInertia, pState.invInertiaLocal));

            float w1 = 0f;
            if (!cState.isKinematic) w1 = Vector3.Dot(gradQ1, ApplyInvInertiaWorld(gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));

            float wSum = w0 + w1;
            if (wSum < 1e-6f) continue;

            // Recalculate velocity per-axis iteration so the damping sees the newest forces!
            Vector3 dw0 = GetDeltaTheta(pState.q_prev, pState.q);
            Vector3 dw1 = GetDeltaTheta(cState.q_prev, cState.q);
            float dC = Vector3.Dot(gradQ0, dw0) + Vector3.Dot(gradQ1, dw1);

            // Damping logic
            float beta = joint.muscleDamping;
            float betaTilde = 0.5f * dt * beta;
            float gamma = (alpha * betaTilde) / dt;

            // Extract the specific lambda memory for this axis
            float currentLambda = i == 0 ? joint.lambdaRotation.x : (i == 1 ? joint.lambdaRotation.y : joint.lambdaRotation.z);

            float denom = (1f + gamma) * wSum + alpha;
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / denom;

            // Save lambda memory
            if (i == 0) joint.lambdaRotation.x += deltaLambda;
            else if (i == 1) joint.lambdaRotation.y += deltaLambda;
            else joint.lambdaRotation.z += deltaLambda;

            // Apply rotations
            if (!pState.isKinematic)
            {
                ApplyDeltaRotation(pState, ApplyInvInertiaWorld(deltaLambda * gradQ0, pState.q, pState.qInertia, pState.invInertiaLocal));
            }

            if (!cState.isKinematic)
            {
                ApplyDeltaRotation(cState, ApplyInvInertiaWorld(deltaLambda * gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));
            }
        }
    }

    private void DeriveVelocities(float dt)
    {
        foreach (var kvp in _states)
        {
            var state = kvp.Value;
            if (state.isKinematic) continue;

            state.v = (state.p - state.p_prev) / dt;
            state.w = GetDeltaTheta(state.q_prev, state.q) / dt;
        }
    }

    private void ApplyStatesToRigidbodies()
    {
        foreach (var kvp in _states)
        {
            var state = kvp.Value;
            if (state.isKinematic) continue;

            state.rb.linearVelocity = state.v;
            state.rb.angularVelocity = state.w;
            state.rb.position = state.p;
            state.rb.rotation = state.q;
        }
    }

    // --- MATH HELPERS ---

    private Vector3 ApplyInvInertiaWorld(Vector3 v, Quaternion q, Quaternion qInertia, Vector3 invInertiaLocal)
    {
        Quaternion R = q * qInertia;
        Vector3 localV = Quaternion.Inverse(R) * v;
        localV.x *= invInertiaLocal.x;
        localV.y *= invInertiaLocal.y;
        localV.z *= invInertiaLocal.z;
        return R * localV;
    }

    private void ApplyDeltaRotation(XPBDState state, Vector3 deltaRot)
    {
        float angle = deltaRot.magnitude;
        if (angle < 1e-6f) return;

        Quaternion qRot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, deltaRot / angle);
        state.q = qRot * state.q;
    }

    private Vector3 GetDeltaTheta(Quaternion qPrev, Quaternion qCurr)
    {
        Quaternion dq = qCurr * Quaternion.Inverse(qPrev);
        dq.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (Mathf.Abs(angle) < 1e-6f || axis.sqrMagnitude < 1e-6f) return Vector3.zero;
        return axis.normalized * (angle * Mathf.Deg2Rad);
    }

    [ContextMenu("Bake All Joints From Targets")]
    public void BakeJointsFromTargets()
    {
        if (joints == null || joints.Count == 0) return;

        foreach (var joint in joints)
        {
            if (joint.parent == null || joint.child == null || joint.parentTarget == null || joint.childTarget == null) continue;

            // Use the child target's origin as the physical pivot point
            Vector3 pivotWorld = joint.childTarget.position;

            joint.parentAnchorLocal = Quaternion.Inverse(joint.parent.rotation) * (pivotWorld - joint.parent.position);
            joint.childAnchorLocal = Quaternion.Inverse(joint.child.rotation) * (pivotWorld - joint.child.position);

            // SAVE THE SCALE DURING BAKE!
            Vector3 pScale = joint.parent.transform.localScale;
            Vector3 cScale = joint.child.transform.localScale;

            joint.bakedParentScale = new Vector3(
                pScale.x == 0 ? 1e-6f : pScale.x,
                pScale.y == 0 ? 1e-6f : pScale.y,
                pScale.z == 0 ? 1e-6f : pScale.z);

            joint.bakedChildScale = new Vector3(
                cScale.x == 0 ? 1e-6f : cScale.x,
                cScale.y == 0 ? 1e-6f : cScale.y,
                cScale.z == 0 ? 1e-6f : cScale.z);
        }

        Debug.Log("Successfully baked all XPBD anchors and scales!");
    }
}