using UnityEngine;
using Fusion;

// --- SHARED DATA CLASSES ---
public class XPBDState
{
    public Rigidbody rb;
    public Vector3 p_prev;
    public Quaternion q_prev;
    public Vector3 p;
    public Quaternion q;
    public Vector3 v;
    public Vector3 w;
    public float invMass;
    public Vector3 invInertiaLocal;
    public Quaternion qInertia;
    public bool isKinematic;
}

[System.Serializable]
public struct NetworkTempJoint : INetworkStruct
{
    public NetworkId parentId;
    public NetworkId childId;

    public Vector3 parentAnchorLocal;
    public Vector3 childAnchorLocal;

    public Quaternion targetLocalRotation;

    public float distanceCompliance;
    public float distanceDamping;
    public float muscleCompliance;
    public float muscleDamping;
}

public class HydratedTempJoint
{
    public NetworkTempJoint networkedData;
    public Rigidbody parentRb;
    public Rigidbody childRb;
    public Vector3 lambdaPosition;
    public Vector3 lambdaRotation;

    public bool IsValid() => parentRb != null && childRb != null;

    public void Clear()
    {
        parentRb = null;
        childRb = null;
        networkedData = default(NetworkTempJoint);
        lambdaPosition = Vector3.zero;
        lambdaRotation = Vector3.zero;
    }
}

[System.Serializable]
public struct NetworkGrabJoint : INetworkStruct
{
    public NetworkId grabberId; // The Player's Network Object
    public NetworkId itemId;    // The Object being grabbed

    public Vector3 localGrabOffset; // Where on the item we grabbed
    public float grabDistance;      // Distance from the camera

    public Quaternion targetLocalRotation; // <-- Added: The snapshot of how the item aligns to the camera

    public float grabStrength;      // Used for Compliance (Stiffness)
    public float grabDamping;       // <-- Added: Prevents the grabbed object from infinitely wobbling
    public float dragResistance;    // Used for Inverse Mass Scaling (How easily the player is dragged)
}

// --- LOCAL RUNTIME DATA (Fast Access) ---
public class HydratedGrabJoint
{
    public NetworkGrabJoint networkedData;

    // Cached References
    public HybridCharacterController grabberController;
    public Rigidbody torsoRb;
    public Rigidbody itemRb;

    // XPBD Memory
    public Vector3 lambdaPosition;
    public Vector3 lambdaRotation; // <-- Added: Needed for 3D orthogonal rotation solver

    public bool IsValid() => grabberController != null && itemRb != null && torsoRb != null;

    public void Clear()
    {
        grabberController = null;
        torsoRb = null;
        itemRb = null;
        networkedData = default(NetworkGrabJoint);
        lambdaPosition = Vector3.zero;
        lambdaRotation = Vector3.zero; // <-- Added: Reset memory
    }
}

public static class XPBDMath
{
    public static Vector3 ApplyInvInertiaWorld(Vector3 v, Quaternion q, Quaternion qInertia, Vector3 invInertiaLocal)
    {
        Quaternion R = q * qInertia;
        Vector3 localV = Quaternion.Inverse(R) * v;
        localV.x *= invInertiaLocal.x;
        localV.y *= invInertiaLocal.y;
        localV.z *= invInertiaLocal.z;
        return R * localV;
    }

    public static void ApplyDeltaRotation(XPBDState state, Vector3 deltaRot)
    {
        float angle = deltaRot.magnitude;
        if (angle < 1e-6f) return;

        Quaternion qRot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, deltaRot / angle);
        state.q = qRot * state.q;
    }

    public static Vector3 GetDeltaTheta(Quaternion qPrev, Quaternion qCurr)
    {
        Quaternion dq = qCurr * Quaternion.Inverse(qPrev);
        dq.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (Mathf.Abs(angle) < 1e-6f || axis.sqrMagnitude < 1e-6f) return Vector3.zero;
        return axis.normalized * (angle * Mathf.Deg2Rad);
    }

    public static Quaternion NormalizeQuaternion(Quaternion q)
    {
        float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        if (mag < 1e-6f) return Quaternion.identity;
        return new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
    }

    public static float GetAngleAroundAxis(Quaternion rotation, Vector3 axis)
    {
        axis = axis.normalized;
        if (axis.sqrMagnitude < 1e-8f) return 0f;

        Vector3 vectorRot = new Vector3(rotation.x, rotation.y, rotation.z);
        Vector3 proj = Vector3.Dot(vectorRot, axis) * axis;

        Quaternion twist = new Quaternion(proj.x, proj.y, proj.z, rotation.w);
        float sqMag = twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w;
        if (sqMag < 1e-8f) return 0f;

        twist = NormalizeQuaternion(twist);
        twist.ToAngleAxis(out float angleDeg, out Vector3 outAxis);

        if (angleDeg > 180f) angleDeg -= 360f;

        // Check if the axis flipped
        Vector3 twistVec = new Vector3(twist.x, twist.y, twist.z);
        float sign = Vector3.Dot(axis, twistVec) >= 0f ? 1f : -1f;

        return angleDeg * sign;
    }

    public static void SolveSphericalPosition(XPBDState pState, XPBDState cState, Vector3 r0, Vector3 r1, Vector3 dir,
        float alpha, float gamma, ref Vector3 lambdaPosition)
    {
        Vector3 dx0 = pState.p - pState.p_prev, dw0 = GetDeltaTheta(pState.q_prev, pState.q);
        Vector3 dx1 = cState.p - cState.p_prev, dw1 = GetDeltaTheta(cState.q_prev, cState.q);

        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = axes[i];
            float C = Vector3.Dot(dir, cAxis);

            Vector3 gradP0 = -cAxis;
            Vector3 gradP1 = cAxis;
            Vector3 gradQ0 = Vector3.Cross(r0, gradP0);
            Vector3 gradQ1 = Vector3.Cross(r1, gradP1);

            float w0 = pState.isKinematic ? 0f : pState.invMass + Vector3.Dot(gradQ0, ApplyInvInertiaWorld(gradQ0, pState.q, pState.qInertia, pState.invInertiaLocal));
            float w1 = cState.isKinematic ? 0f : cState.invMass + Vector3.Dot(gradQ1, ApplyInvInertiaWorld(gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));

            float wSum = w0 + w1;
            if (wSum < 1e-6f) continue;

            float dC = Vector3.Dot(gradP0, dx0) + Vector3.Dot(gradP1, dx1) + Vector3.Dot(gradQ0, dw0) + Vector3.Dot(gradQ1, dw1);

            float currentLambda = i == 0 ? lambdaPosition.x : (i == 1 ? lambdaPosition.y : lambdaPosition.z);
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * wSum + alpha);

            if (i == 0) lambdaPosition.x += deltaLambda; else if (i == 1) lambdaPosition.y += deltaLambda; else lambdaPosition.z += deltaLambda;

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
    }

    public static void SolveSphericalRotation(XPBDState pState, XPBDState cState, Quaternion targetQ,
        float alpha, float gamma, ref Vector3 lambdaRotation)
    {
        Quaternion qError = targetQ * Quaternion.Inverse(cState.q);

        if (qError.w < 0f) { qError.x = -qError.x; qError.y = -qError.y; qError.z = -qError.z; qError.w = -qError.w; }

        Vector3 v = new Vector3(qError.x, qError.y, qError.z);
        float sinHalfAngle = v.magnitude;
        if (sinHalfAngle < 1e-6f) return;

        Vector3 axis = v / sinHalfAngle;
        float angleRad = 2f * Mathf.Atan2(sinHalfAngle, qError.w);

        Vector3 rotVec = axis * angleRad;
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = axes[i];
            float C = Vector3.Dot(rotVec, cAxis);

            float w0 = pState.isKinematic ? 0f : Vector3.Dot(cAxis, ApplyInvInertiaWorld(cAxis, pState.q, pState.qInertia, pState.invInertiaLocal));
            float w1 = cState.isKinematic ? 0f : Vector3.Dot(-cAxis, ApplyInvInertiaWorld(-cAxis, cState.q, cState.qInertia, cState.invInertiaLocal));
            float wSum = w0 + w1;
            if (wSum < 1e-6f) continue;

            float dC = Vector3.Dot(cAxis, GetDeltaTheta(pState.q_prev, pState.q)) + Vector3.Dot(-cAxis, GetDeltaTheta(cState.q_prev, cState.q));
            float currentLambda = i == 0 ? lambdaRotation.x : (i == 1 ? lambdaRotation.y : lambdaRotation.z);

            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * wSum + alpha);

            if (i == 0) lambdaRotation.x += deltaLambda; else if (i == 1) lambdaRotation.y += deltaLambda; else lambdaRotation.z += deltaLambda;

            if (!pState.isKinematic) ApplyDeltaRotation(pState, ApplyInvInertiaWorld(deltaLambda * cAxis, pState.q, pState.qInertia, pState.invInertiaLocal));
            if (!cState.isKinematic) ApplyDeltaRotation(cState, ApplyInvInertiaWorld(deltaLambda * -cAxis, cState.q, cState.qInertia, cState.invInertiaLocal));
        }
    }

    public static void SolveOneWayGrabDistance(XPBDState pState, XPBDState cState, Vector3 r1, Vector3 dir, Vector3 dxTarget,
        float alpha, float gamma, float dragResist, float recoilMultiplier, float dt, ref Vector3 lambdaPosition)
    {
        Vector3 dx1 = cState.p - cState.p_prev;
        Vector3 dw1 = GetDeltaTheta(cState.q_prev, cState.q);

        float w0_solver = 0f;
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = axes[i];
            float C = Vector3.Dot(dir, cAxis);

            Vector3 gradP1 = cAxis;
            Vector3 gradQ1 = Vector3.Cross(r1, cAxis);

            float w1 = cState.isKinematic ? 0f : cState.invMass + Vector3.Dot(gradQ1, ApplyInvInertiaWorld(gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));
            float wSum = w0_solver + w1;
            if (wSum < 1e-6f) continue;

            float dC = Vector3.Dot(-cAxis, dxTarget) + Vector3.Dot(gradP1, dx1) + Vector3.Dot(gradQ1, dw1);

            float currentLambda = i == 0 ? lambdaPosition.x : (i == 1 ? lambdaPosition.y : lambdaPosition.z);
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * wSum + alpha);

            if (i == 0) lambdaPosition.x += deltaLambda; else if (i == 1) lambdaPosition.y += deltaLambda; else lambdaPosition.z += deltaLambda;

            if (!cState.isKinematic)
            {
                cState.p += cState.invMass * deltaLambda * gradP1;
                ApplyDeltaRotation(cState, ApplyInvInertiaWorld(deltaLambda * gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal));
            }
        }

        if (!pState.isKinematic)
        {
            float effectiveInvMass0 = pState.invMass / Mathf.Max(1f, dragResist);
            Vector3 rawRecoilShift = -lambdaPosition * effectiveInvMass0;
            Vector3 finalRecoilShift = rawRecoilShift * recoilMultiplier;

            float maxSafeShift = 5f * dt;
            if (finalRecoilShift.sqrMagnitude > maxSafeShift * maxSafeShift)
            {
                finalRecoilShift = finalRecoilShift.normalized * maxSafeShift;
            }
            pState.p += finalRecoilShift;
        }
    }

    public static void SolveOneWayGrabRotation(XPBDState cState,Quaternion targetQ, Quaternion targetQ_prev,
        float alpha, float gamma, ref Vector3 lambdaRotation)
    {
        Quaternion qError = targetQ * Quaternion.Inverse(cState.q);
        if (qError.w < 0f) { qError.x = -qError.x; qError.y = -qError.y; qError.z = -qError.z; qError.w = -qError.w; }

        Vector3 v = new Vector3(qError.x, qError.y, qError.z);
        float sinHalfAngle = v.magnitude;
        if (sinHalfAngle < 1e-6f) return;

        Vector3 axis = v / sinHalfAngle;
        float angleRad = 2f * Mathf.Atan2(sinHalfAngle, qError.w);

        Vector3 rotVec = axis * angleRad;
        Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = axes[i];
            float C = Vector3.Dot(rotVec, cAxis);

            float w1 = cState.isKinematic ? 0f : Vector3.Dot(-cAxis, ApplyInvInertiaWorld(-cAxis, cState.q, cState.qInertia, cState.invInertiaLocal));
            if (w1 < 1e-6f) continue;

            float dC = Vector3.Dot(cAxis, GetDeltaTheta(targetQ_prev, targetQ)) + Vector3.Dot(-cAxis, GetDeltaTheta(cState.q_prev, cState.q));

            float currentLambda = i == 0 ? lambdaRotation.x : (i == 1 ? lambdaRotation.y : lambdaRotation.z);
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * w1 + alpha);

            if (i == 0) lambdaRotation.x += deltaLambda; else if (i == 1) lambdaRotation.y += deltaLambda; else lambdaRotation.z += deltaLambda;

            if (!cState.isKinematic) ApplyDeltaRotation(cState, ApplyInvInertiaWorld(deltaLambda * -cAxis, cState.q, cState.qInertia, cState.invInertiaLocal));
        }
    }




    public static void SolveAngularLimit(
        XPBDState pState, XPBDState cState,
        Quaternion rotDiff, Vector3 axisParentNorm,
        float minDeg, float maxDeg,
        float compliance, float damping, float dt,
        ref float lambdaLimit)
    {
        float angleDeg = GetAngleAroundAxis(rotDiff, axisParentNorm);

        // Swap if designer entered them backwards
        if (minDeg > maxDeg) { float tmp = minDeg; minDeg = maxDeg; maxDeg = tmp; }

        float clamped = Mathf.Clamp(angleDeg, minDeg, maxDeg);
        float violationDeg = angleDeg - clamped;

        // INEQUALITY CHECK: If we are inside the limits, do absolutely nothing!
        if (Mathf.Abs(violationDeg) < 0.01f) return;

        float violationRad = violationDeg * Mathf.Deg2Rad;

        Vector3 axisWorld = pState.q * axisParentNorm;
        if (axisWorld.sqrMagnitude < 1e-6f) return;
        axisWorld.Normalize();

        // THE FIX: Correcting the Gradients!
        // To reduce a positive angle violation, the Child must rotate negatively around the axis,
        // and the Parent must rotate positively.
        Vector3 gradP = -axisWorld;
        Vector3 gradC = axisWorld;

        float alpha = compliance / (dt * dt);
        float gamma = (alpha * (0.5f * dt * damping)) / dt;

        float w0 = pState.isKinematic ? 0f : Vector3.Dot(gradP, ApplyInvInertiaWorld(gradP, pState.q, pState.qInertia, pState.invInertiaLocal));
        float w1 = cState.isKinematic ? 0f : Vector3.Dot(gradC, ApplyInvInertiaWorld(gradC, cState.q, cState.qInertia, cState.invInertiaLocal));
        float wSum = w0 + w1;

        if (wSum < 1e-6f) return;

        float dC = Vector3.Dot(gradP, GetDeltaTheta(pState.q_prev, pState.q)) + Vector3.Dot(gradC, GetDeltaTheta(cState.q_prev, cState.q));

        float deltaLambda = -(violationRad + alpha * lambdaLimit + gamma * dC) / ((1f + gamma) * wSum + alpha);

        lambdaLimit += deltaLambda;

        // Apply the corrected gradients to the bodies!
        if (!pState.isKinematic) ApplyDeltaRotation(pState, ApplyInvInertiaWorld(deltaLambda * gradP, pState.q, pState.qInertia, pState.invInertiaLocal));
        if (!cState.isKinematic) ApplyDeltaRotation(cState, ApplyInvInertiaWorld(deltaLambda * gradC, cState.q, cState.qInertia, cState.invInertiaLocal));
    }
}