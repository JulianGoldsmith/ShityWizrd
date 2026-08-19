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

public class XPBDKinematicTargetState
{
    public Vector3 p_prev;
    public Quaternion q_prev;
    public Vector3 p;
    public Quaternion q;
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
    public NetworkId grabberId;
    public NetworkId itemId;

    public Vector3 localGrabOffset;
    public float grabDistance;
    public float initialTetherLength;
    public Quaternion targetLocalRotation;

    public float aimStiffness;
    public float aimDamping;
    public float maxAimHorizontalForce;
    public float maxAimLiftForce;
    public float maxAimTorque;

    public float tetherStiffness;
    public float tetherDamping;
    public float maxTetherForce;
    public float reactionScale;
}

// --- LOCAL RUNTIME DATA (Fast Access) ---
public class HydratedGrabJoint
{
    public NetworkGrabJoint networkedData;

    public HybridCharacterController grabberController;
    public Rigidbody torsoRb;
    public Rigidbody itemRb;

    public XPBDState itemState = new XPBDState();
    public XPBDKinematicTargetState targetState = new XPBDKinematicTargetState();
    public Vector3 centerOfMassLocal;

    public Vector3 torsoPositionBeforePhysics;
    public Vector3 lambdaPosition;
    public Vector3 lambdaRotation;
    public bool preparedForPostPhysics;

    public Vector3 tetherAnchorPositionBeforePhysics;
    public float lambdaTether;
    public Vector3 tetherDirection;
    public float tetherLengthBeforePhysics;

    public bool IsValid() => grabberController != null && itemRb != null && torsoRb != null;

    public void Clear()
    {
        grabberController = null;
        torsoRb = null;
        itemRb = null;
        centerOfMassLocal = Vector3.zero;
        networkedData = default(NetworkGrabJoint);
        lambdaPosition = Vector3.zero;
        lambdaRotation = Vector3.zero;
        lambdaTether = 0f;
        tetherDirection = Vector3.zero;
        preparedForPostPhysics = false;
        tetherLengthBeforePhysics = 0f;
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
        return GetRotationErrorVector(qCurr, qPrev, out _);
    }

    public static Quaternion NormalizeQuaternion(Quaternion q)
    {
        float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        if (mag < 1e-6f) return Quaternion.identity;
        return new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
    }

    public static float GetAngleAroundAxis(Quaternion rotation, Vector3 axis)
    {
        float axisSqrMagnitude = axis.sqrMagnitude;
        if (axisSqrMagnitude < 1e-8f) return 0f;

        if (Mathf.Abs(axisSqrMagnitude - 1f) > 1e-6f) axis /= Mathf.Sqrt(axisSqrMagnitude);

        float projectedSinHalfAngle = rotation.x * axis.x + rotation.y * axis.y + rotation.z * axis.z;
        float twistSqrMagnitude = projectedSinHalfAngle * projectedSinHalfAngle + rotation.w * rotation.w;
        if (twistSqrMagnitude < 1e-8f) return 0f;

        float angleDeg = 2f * Mathf.Atan2(projectedSinHalfAngle, rotation.w) * Mathf.Rad2Deg;

        if (angleDeg > 180f) angleDeg -= 360f;
        else if (angleDeg < -180f) angleDeg += 360f;

        return angleDeg;
    }

    public static void SolveSphericalPosition(XPBDState pState, XPBDState cState, Vector3 r0, Vector3 r1, Vector3 dir,
    float alpha, float gamma, ref Vector3 lambdaPosition, float leverArmScale = 1.0f, float parentPositionInfluence = 1.0f)
    {
        Vector3 dx0 = pState.p - pState.p_prev, dw0 = GetDeltaTheta(pState.q_prev, pState.q);
        Vector3 dx1 = cState.p - cState.p_prev, dw1 = GetDeltaTheta(cState.q_prev, cState.q);

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            float C = Vector3.Dot(dir, cAxis);

            Vector3 gradP0 = -cAxis;
            Vector3 gradP1 = cAxis;
            Vector3 gradQ0 = Vector3.Cross(r0, gradP0);
            Vector3 gradQ1 = Vector3.Cross(r1, gradP1);

            // Apply parentPositionInfluence to w0 so the solver views the parent as heavier
            Vector3 inverseInertiaGradQ0 = pState.isKinematic ? Vector3.zero : ApplyInvInertiaWorld(gradQ0, pState.q, pState.qInertia, pState.invInertiaLocal);

            Vector3 inverseInertiaGradQ1 = cState.isKinematic ? Vector3.zero : ApplyInvInertiaWorld(gradQ1, cState.q, cState.qInertia, cState.invInertiaLocal);

            float w0 = pState.isKinematic ? 0f : (pState.invMass + Vector3.Dot(gradQ0, inverseInertiaGradQ0)) * parentPositionInfluence;

            float w1 = cState.isKinematic ? 0f : cState.invMass + Vector3.Dot(gradQ1, inverseInertiaGradQ1);

            float wSum = w0 + w1;
            if (wSum < 1e-6f) continue;

            float dC = Vector3.Dot(gradP0, dx0) + Vector3.Dot(gradP1, dx1) + Vector3.Dot(gradQ0, dw0) + Vector3.Dot(gradQ1, dw1);
            float currentLambda = i == 0 ? lambdaPosition.x : (i == 1 ? lambdaPosition.y : lambdaPosition.z);
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * wSum + alpha);

            if (i == 0) lambdaPosition.x += deltaLambda; else if (i == 1) lambdaPosition.y += deltaLambda; else lambdaPosition.z += deltaLambda;

            if (!pState.isKinematic)
            {
                // Apply influence to the positional shift
                pState.p += pState.invMass * deltaLambda * gradP0 * parentPositionInfluence;
                // Multiply the rotation by the leverArmScale AND the positional influence!
                ApplyDeltaRotation(pState, inverseInertiaGradQ0 * (deltaLambda * leverArmScale * parentPositionInfluence));
            }
            if (!cState.isKinematic)
            {
                cState.p += cState.invMass * deltaLambda * gradP1;
                //Multiply ONLY the rotation by the leverArmScale!
                ApplyDeltaRotation(cState, inverseInertiaGradQ1 * (deltaLambda * leverArmScale));
            }
        }
    }

    public static Vector3 GetRotationErrorVector(Quaternion targetQ, Quaternion currentQ, out float angleRadians)
    {
        Quaternion qError = targetQ * Quaternion.Inverse(currentQ);

        if (qError.w < 0f) { qError.x = -qError.x; qError.y = -qError.y; qError.z = -qError.z; qError.w = -qError.w; }

        Vector3 quaternionVector = new Vector3(qError.x, qError.y, qError.z);
        float sinHalfAngle = quaternionVector.magnitude;

        if (sinHalfAngle < 1e-6f)
        {
            angleRadians = 0f;
            return Vector3.zero;
        }

        angleRadians = 2f * Mathf.Atan2(sinHalfAngle, qError.w);
        return quaternionVector * (angleRadians / sinHalfAngle);
    }

    public static void SolveSphericalRotation(XPBDState pState, XPBDState cState, Vector3 rotationError, float alpha, float gamma,
    ref Vector3 lambdaRotation, float parentRotationInfluence = 1.0f)
    {
        if (rotationError.sqrMagnitude == 0f) return;

        for (int i = 0; i < 3; i++)
        {
            Vector3 cAxis = i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
            float C = Vector3.Dot(rotationError, cAxis);

            Vector3 gradP = cAxis;
            Vector3 gradC = -cAxis;

            Vector3 inverseInertiaGradP = pState.isKinematic ? Vector3.zero :
                ApplyInvInertiaWorld(gradP, pState.q, pState.qInertia, pState.invInertiaLocal);

            Vector3 inverseInertiaGradC = cState.isKinematic ? Vector3.zero :
                ApplyInvInertiaWorld(gradC, cState.q, cState.qInertia, cState.invInertiaLocal);

            float w0 = pState.isKinematic ? 0f : Vector3.Dot(gradP, inverseInertiaGradP) * parentRotationInfluence;
            float w1 = cState.isKinematic ? 0f : Vector3.Dot(gradC, inverseInertiaGradC);
            float wSum = w0 + w1;
            if (wSum < 1e-6f) continue;

            float dC = Vector3.Dot(gradP, GetDeltaTheta(pState.q_prev, pState.q)) +
                Vector3.Dot(gradC, GetDeltaTheta(cState.q_prev, cState.q));

            float currentLambda = i == 0 ? lambdaRotation.x : i == 1 ? lambdaRotation.y : lambdaRotation.z;
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * wSum + alpha);

            if (i == 0) lambdaRotation.x += deltaLambda;
            else if (i == 1) lambdaRotation.y += deltaLambda;
            else lambdaRotation.z += deltaLambda;

            if (!pState.isKinematic) ApplyDeltaRotation(pState, inverseInertiaGradP * (deltaLambda * parentRotationInfluence));
            if (!cState.isKinematic) ApplyDeltaRotation(cState, inverseInertiaGradC * deltaLambda);
        }
    }

    public static void SolveKinematicGrabPosition(XPBDKinematicTargetState targetState, XPBDState itemState, Vector3 itemAnchorFromCenterOfMassLocal, float alpha, float gamma, ref Vector3 lambdaPosition)
    {
        Vector3 targetDisplacement = targetState.p - targetState.p_prev;

        for (int i = 0; i < 3; i++)
        {
            Vector3 axis = i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

            Vector3 currentGrabPoint = itemState.p + itemState.q * itemAnchorFromCenterOfMassLocal;
            Vector3 previousGrabPoint = itemState.p_prev + itemState.q_prev * itemAnchorFromCenterOfMassLocal;
            Vector3 grabPointDisplacement = currentGrabPoint - previousGrabPoint;

            float C = Vector3.Dot(currentGrabPoint - targetState.p, axis);
            float w = itemState.invMass;

            if (w < 1e-6f)
                continue;

            float dC = Vector3.Dot(axis, grabPointDisplacement - targetDisplacement);
            float currentLambda = i == 0 ? lambdaPosition.x : i == 1 ? lambdaPosition.y : lambdaPosition.z;
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * w + alpha);

            if (i == 0)
                lambdaPosition.x += deltaLambda;
            else if (i == 1)
                lambdaPosition.y += deltaLambda;
            else
                lambdaPosition.z += deltaLambda;

            itemState.p += itemState.invMass * deltaLambda * axis;
        }
    }

    public static void SolveKinematicGrabRotation(XPBDKinematicTargetState targetState, XPBDState itemState, float alpha, float gamma, ref Vector3 lambdaRotation)
    {
        Vector3 targetAngularDisplacement = GetDeltaTheta(targetState.q_prev, targetState.q);

        for (int i = 0; i < 3; i++)
        {
            Vector3 axis = i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

            Quaternion qError = targetState.q * Quaternion.Inverse(itemState.q);

            if (qError.w < 0f)
            {
                qError.x = -qError.x;
                qError.y = -qError.y;
                qError.z = -qError.z;
                qError.w = -qError.w;
            }

            Vector3 errorVector = new Vector3(qError.x, qError.y, qError.z);
            float sinHalfAngle = errorVector.magnitude;

            if (sinHalfAngle < 1e-6f)
                return;

            Vector3 errorAxis = errorVector / sinHalfAngle;
            float angleRadians = 2f * Mathf.Atan2(sinHalfAngle, qError.w);
            Vector3 rotationError = errorAxis * angleRadians;

            float C = Vector3.Dot(rotationError, axis);
            float w = Vector3.Dot(axis, ApplyInvInertiaWorld(axis, itemState.q, itemState.qInertia, itemState.invInertiaLocal));

            if (w < 1e-6f)
                continue;

            Vector3 itemAngularDisplacement = GetDeltaTheta(itemState.q_prev, itemState.q);
            float dC = Vector3.Dot(axis, targetAngularDisplacement - itemAngularDisplacement);
            float currentLambda = i == 0 ? lambdaRotation.x : i == 1 ? lambdaRotation.y : lambdaRotation.z;
            float deltaLambda = -(C + alpha * currentLambda + gamma * dC) / ((1f + gamma) * w + alpha);

            if (i == 0)
                lambdaRotation.x += deltaLambda;
            else if (i == 1)
                lambdaRotation.y += deltaLambda;
            else
                lambdaRotation.z += deltaLambda;

            Vector3 angularCorrection = ApplyInvInertiaWorld(-deltaLambda * axis, itemState.q, itemState.qInertia, itemState.invInertiaLocal);
            ApplyDeltaRotation(itemState, angularCorrection);
        }
    }
    /* public static void SolveOneWayGrabDistance(XPBDState pState, XPBDState cState, Vector3 r1, Vector3 dir, Vector3 dxTarget,
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

 */


    public static void SolveAngularLimit(
        XPBDState pState, XPBDState cState,
        Quaternion rotDiff, Vector3 axisParentNorm,
        float minDeg, float maxDeg,
        float alpha, float gamma,
        ref float lambdaLimit, float parentRotationInfluence = 1.0f)
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

        Vector3 inverseInertiaGradP = pState.isKinematic ? Vector3.zero :
            ApplyInvInertiaWorld(gradP, pState.q, pState.qInertia, pState.invInertiaLocal);

        Vector3 inverseInertiaGradC = cState.isKinematic ? Vector3.zero :
            ApplyInvInertiaWorld(gradC, cState.q, cState.qInertia, cState.invInertiaLocal);

        float w0 = pState.isKinematic ? 0f : Vector3.Dot(gradP, inverseInertiaGradP) * parentRotationInfluence;
        float w1 = cState.isKinematic ? 0f : Vector3.Dot(gradC, inverseInertiaGradC);
        float wSum = w0 + w1;
        if (wSum < 1e-6f) return;

        float dC = Vector3.Dot(gradP, GetDeltaTheta(pState.q_prev, pState.q)) +
            Vector3.Dot(gradC, GetDeltaTheta(cState.q_prev, cState.q));

        float deltaLambda = -(violationRad + alpha * lambdaLimit + gamma * dC) / ((1f + gamma) * wSum + alpha);
        lambdaLimit += deltaLambda;

        if (!pState.isKinematic) ApplyDeltaRotation(pState, inverseInertiaGradP * (deltaLambda * parentRotationInfluence));
        if (!cState.isKinematic) ApplyDeltaRotation(cState, inverseInertiaGradC * deltaLambda);
    }
}
