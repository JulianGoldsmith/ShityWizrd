using System;
using UnityEngine;
using System.Collections.Generic;
using Fusion.Addons.Physics;
using Newtonsoft.Json.Bson;

public class XpbdConstraintSolver : MonoBehaviour
{
    public List<XpbdJoint> joints = new List<XpbdJoint>();

    public List<XpbdJoint> ragdollJoints = new List<XpbdJoint>();

    const float constraintVelocityScale = 1.0f;

    public float distanceBiasScale = 1f;
    public float distanceBiasScaleRagdoll = 0.2f;

    public int iterations = 4;

    public void Solve(float deltaTime, bool includeRagdollJoints = false, float strength = 1f, float _sizeMult = 1f)
    {
        if (joints.Count == 0 || deltaTime <= 0f)
            return;

        foreach (var j in joints) //get size
        {
            j.tmpSizeMult = 1;
            if (j.po != null)
            j.tmpSizeMult = j.po.physicsObjectProperties.Size;
        }

        var bodySnapshots = StoreRBSnapshot();

        ResetJointState(joints);
        if (includeRagdollJoints)
            ResetJointState(ragdollJoints);

        for (int iter = 0; iter < iterations; iter++)
        {
            foreach (var j in joints)
            {

                if (j == null || !j.jointActive || !j.enableDistanceConstraint)
                    continue;
                float sizeMult = _sizeMult * j.tmpSizeMult / j.bakedScale;
                SolveDistanceConstraintXPBD(j, deltaTime, sizeMult);
            }

            if (!includeRagdollJoints)
                continue;

            foreach (var rj in ragdollJoints)
            {
                if (rj == null || !rj.jointActive || !rj.enableDistanceConstraint)
                    continue;
                float sizeMult = _sizeMult / rj.bakedScale;
                SolveDistanceConstraintXPBD(rj, deltaTime, sizeMult);
            }
        }

        RecomputeVelocities(bodySnapshots, deltaTime);

        for (int iter = 0; iter < iterations; iter++)
        {
            foreach (var j in joints)
            {
                if (j == null || !j.jointActive || !j.enableAngularLimits)
                    continue;
                SolveAngularLimits(j, deltaTime);
            }

            if (!includeRagdollJoints)
                continue;

            foreach (var rj in ragdollJoints)
            {
                if (rj == null || !rj.jointActive || !rj.enableAngularLimits)
                    continue;
                SolveAngularLimits(rj, deltaTime);
            }
        }

    }


    void ResetJointState(List<XpbdJoint> list)
    {
        if (list == null) return;
        foreach (var j in list)
        {
            if (j == null) continue;
            j.lambdaDistance = 0f;
            j.lambdaRotation = 0f;
            j.frameLambdaDistance = 0f;
        }
    }

    Dictionary<Rigidbody, BodySnapshot> StoreRBSnapshot()
    {
        var dict = new Dictionary<Rigidbody, BodySnapshot>();

        void AddFromList(List<XpbdJoint> list)
        {
            if (list == null) return;

            foreach (var j in list)
            {
                if (j == null) continue;

                if (j.parent != null && !dict.ContainsKey(j.parent))
                {
                    var rb = j.parent;
                    dict[rb] = new BodySnapshot
                    {
                        rb = rb,
                        prePosition = rb.position,
                        preRotation = rb.rotation,
                        preLinearVelocity = rb.linearVelocity,
                        preAngularVelocity = rb.angularVelocity,
                    };
                }

                if (j.child != null && !dict.ContainsKey(j.child))
                {
                    var rb = j.child;
                    dict[rb] = new BodySnapshot
                    {
                        rb = rb,
                        prePosition = rb.position,
                        preRotation = rb.rotation,
                        preLinearVelocity = rb.linearVelocity,
                        preAngularVelocity = rb.angularVelocity,
                    };
                }
            }
        }

        AddFromList(joints);
        AddFromList(ragdollJoints);

        return dict;
    }

    void RecomputeVelocities(Dictionary<Rigidbody, BodySnapshot> snapshots, float dt)
    {
        float invDt = 1f / dt;

        foreach (var kvp in snapshots)
        {
            var rb = kvp.Key;
            var snap = kvp.Value;

            Vector3 newPos = rb.position;
            Quaternion newRot = rb.rotation;

            Vector3 deltaPos = newPos - snap.prePosition;
            Vector3 vConstraint = deltaPos * invDt;

       
            Vector3 wConstraint = ComputeAngularVelocity(snap.preRotation, newRot, dt);

          
            if (float.IsNaN(wConstraint.x) || float.IsInfinity(wConstraint.x))
            {
                wConstraint = Vector3.zero;
            }

           
            rb.linearVelocity = snap.preLinearVelocity + vConstraint * constraintVelocityScale;
            rb.angularVelocity = snap.preAngularVelocity + wConstraint * constraintVelocityScale;
        }
    }

    float GetInverseInertiaAlongAxis(Rigidbody rb, Vector3 axisWorld)
    {
        if (rb.isKinematic)
            return 0f;

        if (axisWorld.sqrMagnitude < 1e-8f)
            return 0f;

        Quaternion inertiaWorldRot = rb.rotation * rb.inertiaTensorRotation;
        Vector3 axisLocal = Quaternion.Inverse(inertiaWorldRot) * axisWorld;

        Vector3 I = rb.inertiaTensor; 
       
        float Ieff =
            axisLocal.x * axisLocal.x * I.x +
            axisLocal.y * axisLocal.y * I.y +
            axisLocal.z * axisLocal.z * I.z;

        if (Ieff <= 1e-8f)
            return 0f;

        return 1f / Ieff;
    }

    Vector3 ComputeAngularVelocity(Quaternion from, Quaternion to, float dt)
    {
        if (dt <= 0f)
            return Vector3.zero;

        Quaternion dq = to * Quaternion.Inverse(from);
        dq.Normalize();

        dq.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;
        float angleRad = angleDeg * Mathf.Deg2Rad;

        if (Mathf.Abs(angleRad) < 1e-5f || axis.sqrMagnitude < 1e-8f)
            return Vector3.zero;

        axis.Normalize();
        return axis * (angleRad / dt);
    }

    void SolveDistanceConstraintVelocity(XpbdJoint j, float dt, bool ragdoll)
    {
        var A = j.parent;
        var B = j.child;
        if (A == null || B == null || dt <= 0f)
            return;

        Vector3 xA = A.position;
        Vector3 xB = B.position;
        Quaternion qA = A.rotation;
        Quaternion qB = B.rotation;

        Vector3 vA = A.linearVelocity;
        Vector3 vB = B.linearVelocity;
        Vector3 wA = A.angularVelocity;
        Vector3 wB = B.angularVelocity;

        Vector3 anchorWorldA = xA + qA * j.parentAnchorLocal;
        Vector3 anchorWorldB = xB + qB * j.childAnchorLocal;

        Vector3 d = anchorWorldB - anchorWorldA;
        float dist = d.magnitude;
        if (dist < 1e-6f)
            return;

        Vector3 n = d / dist;

        float C = dist - j.restLength;

        Vector3 rA_phys = anchorWorldA - A.worldCenterOfMass;
        Vector3 rB_phys = anchorWorldB - B.worldCenterOfMass;

        Vector3 vAnchorA = vA + Vector3.Cross(wA, rA_phys);
        Vector3 vAnchorB = vB + Vector3.Cross(wB, rB_phys);

  
        float Cdot = Vector3.Dot(vAnchorB - vAnchorA, n);


        float invMassA = A.isKinematic ? 0f : 1f / A.mass;
        float invMassB = B.isKinematic ? 0f : 1f / B.mass;
        if (invMassA + invMassB <= 0f)
            return;

        Vector3 raCrossN = Vector3.Cross(rA_phys, n);
        Vector3 rbCrossN = Vector3.Cross(rB_phys, n);

        Vector3 angA = ApplyInvInertiaWorld(A, raCrossN);
        Vector3 angB = ApplyInvInertiaWorld(B, rbCrossN);

        float k =
            invMassA + invMassB +
            Vector3.Dot(Vector3.Cross(angA, rA_phys), n) +
            Vector3.Dot(Vector3.Cross(angB, rB_phys), n);

        if (k <= 1e-8f)
            return;

        float dtSafe = Mathf.Max(dt, 1e-4f);

        //float beta = distanceBiasScale * j.distanceStiffness;  
        //beta = Mathf.Max(beta, 0f);

        //float damp = Mathf.Clamp01(j.distanceDamping);


        //float bias = beta * C / Mathf.Max(dt, 1e-4f);


        //float CdotDamped = Cdot * (1f + damp);

        float beta = (ragdoll? distanceBiasScaleRagdoll : distanceBiasScale)  * j.distanceStiffness;
        beta = Mathf.Max(beta, 0f);

        float bias = beta * C / dtSafe; // [units: m/s]

        // Proper damping: scale Cdot back toward zero instead of up
        float damp = Mathf.Clamp01(j.distanceDamping);

        //  damp = 0  → CdotDamped = Cdot (no extra damping)
        //  damp = 1  → CdotDamped = 0   (critically clamped in velocity-space)
        float CdotDamped = Cdot * (1f + damp);

        float alpha = j.distanceCompliance / (dt * dt); 

        float denom = k + alpha;
        if (denom <= 1e-8f)
            return;


        float deltaLambda = -(CdotDamped + bias) / denom;


        float maxImpulse = Mathf.Max(j.maxDistanceImpulse, 0f);
        if (maxImpulse > 0f)
            deltaLambda = Mathf.Clamp(deltaLambda, -maxImpulse, maxImpulse);

        j.lambdaDistance += deltaLambda;

        Vector3 impulse = deltaLambda * n;

        float torqueScale = j.leverArmScale; // 0..1 to soften torso swing etc

        if (!A.isKinematic)
        {
            vA -= impulse * invMassA;
            wA -= torqueScale * ApplyInvInertiaWorld(A, Vector3.Cross(rA_phys, impulse));
        }

        if (!B.isKinematic)
        {
            vB += impulse * invMassB;
            wB += torqueScale * ApplyInvInertiaWorld(B, Vector3.Cross(rB_phys, impulse));
        }

        A.linearVelocity = vA;
        A.angularVelocity = wA;
        B.linearVelocity = vB;
        B.angularVelocity = wB;
    }

    void SolveDistanceConstraintXPBD(XpbdJoint joint, float dt, float sizeMult = 1f, bool ragdoll = false)
    {
        var parent = joint.parent;
        var child = joint.child;
        if (parent == null || child == null || dt <= 0f)
            return;

        float dtSafe = Mathf.Max(dt, 1e-4f);

        Vector3 parentPos = parent.position;
        Vector3 childPos = child.position;
        Quaternion parentRot = parent.rotation;
        Quaternion childRot = child.rotation;

        Vector3 parentAnchorLocalScaled = joint.parentAnchorLocal * sizeMult;
        Vector3 childAnchorLocalScaled = joint.childAnchorLocal * sizeMult;

        Vector3 anchorWorldA = parentPos + parentRot * parentAnchorLocalScaled;
        Vector3 anchorWorldB = childPos + childRot * childAnchorLocalScaled;

        Vector3 vectorDistance = anchorWorldB - anchorWorldA;
        float dist = vectorDistance.magnitude;
        if (dist < 1e-6f)
            return;

        Vector3 dir = vectorDistance / dist;

        float scaledRestLength = joint.restLength * sizeMult;
        float distError = dist - scaledRestLength;
        if (Mathf.Abs(distError) < 1e-5f)
            return;

        float invMassP = parent.isKinematic ? 0f : 1f / parent.mass;
        float invMassC = child.isKinematic ? 0f : 1f / child.mass;

        Vector3 localPAnchor = anchorWorldA - parent.worldCenterOfMass;
        Vector3 localCAnchor = anchorWorldB - child.worldCenterOfMass;

        Vector3 pAnchorCrossDir = Vector3.Cross(localPAnchor, dir);
        Vector3 cAnchorCorssDir = Vector3.Cross(localCAnchor, dir);

        Vector3 angA = ApplyInvInertiaWorld(parent, pAnchorCrossDir);
        Vector3 angB = ApplyInvInertiaWorld(child, cAnchorCorssDir);

        float k = invMassP + invMassC + Vector3.Dot(Vector3.Cross(angA, localPAnchor), dir) + Vector3.Dot(Vector3.Cross(angB, localCAnchor), dir);

        if (k <= 1e-8f)
            return;


        float scaledCompliance = joint.distanceCompliance * (sizeMult * sizeMult);
        float frameCompliance = scaledCompliance / (dtSafe * dtSafe);

        float denom = k + frameCompliance;
        if (denom <= 1e-8f)
            return;

        float deltaLambda = -(distError + frameCompliance * joint.lambdaDistance) / denom;

        float maxImpulse = Mathf.Max(joint.maxDistanceImpulse, 0f);
        if (maxImpulse > 0f)
        {
            float newLambda = Mathf.Clamp(joint.lambdaDistance + deltaLambda, -maxImpulse, maxImpulse);
            deltaLambda = newLambda - joint.lambdaDistance;
            joint.lambdaDistance = newLambda;
        }
        else
        {
            joint.lambdaDistance += deltaLambda;
        }

        Vector3 corr = deltaLambda * dir;
        float lever = Mathf.Clamp01(joint.leverArmScale); 

        if (!parent.isKinematic)
        {
            parentPos -= invMassP * corr;

            if (lever > 0f && angA.sqrMagnitude > 1e-8f)
            {
                Vector3 torque = -lever * angA * deltaLambda;
                ApplyRotationDelta(ref parentRot, torque);
            }

            parent.position = parentPos;
            parent.rotation = parentRot;
        }

        if (!child.isKinematic)
        {
            childPos += invMassC * corr;

            if (lever > 0f && angB.sqrMagnitude > 1e-8f)
            {
                Vector3 torque = lever * angB * deltaLambda;
                ApplyRotationDelta(ref childRot, torque);
            }

            child.position = childPos;
            child.rotation = childRot;
        }
    }

    void ApplyRotationDelta(ref Quaternion q, Vector3 torque)
    {
        float angle = torque.magnitude;
        if (angle < 1e-6f)
            return;

        const float maxStepRad = 0.5f; 
        if (angle > maxStepRad)
        {
            torque *= maxStepRad / angle;
            angle = maxStepRad;
        }

        Vector3 axis = torque / angle;
        float angleDeg = angle * Mathf.Rad2Deg;

        Quaternion dq = Quaternion.AngleAxis(angleDeg, axis);
        q = dq * q;
    }


    Vector3 ApplyInvInertiaWorld(Rigidbody rb, Vector3 torqueWorld)
    {
        if (rb.isKinematic)
            return Vector3.zero;

        Quaternion R = rb.rotation * rb.inertiaTensorRotation;

        Vector3 torqueLocal = Quaternion.Inverse(R) * torqueWorld;

        Vector3 I = rb.inertiaTensor;
        Vector3 angVelLocal = new Vector3(
            I.x > 1e-8f ? torqueLocal.x / I.x : 0f,
            I.y > 1e-8f ? torqueLocal.y / I.y : 0f,
            I.z > 1e-8f ? torqueLocal.z / I.z : 0f
        );

        return R * angVelLocal;
    }
    public void ApplyRotationalPD(float strength, float deltaTime)
    {
        if (strength <= 0f || joints == null || joints.Count == 0)
            return;

        float s = Mathf.Max(strength, 0f);

        foreach (var j in joints)
        {
            if (j == null || !j.enableRotationPD)
                continue;

            SolveRotationPD(j, deltaTime, s);
        }
    }

    void SolveRotationPD(XpbdJoint j, float dt, float globalStrength)
    {
        var parentRigidbody = j.parent;
        var childRigidbody = j.child;
        var targetTransform = j.targetTransform;
        var deltaTime = dt;
        float ragDollRotationStrength = globalStrength;

        float proportionalGainRotation = j.rotationKp;
        float derivativeGainRotation = j.rotationKd;

        if (childRigidbody == null || parentRigidbody == null || targetTransform == null)
            return;



        float safeDeltaTime = Mathf.Max(deltaTime, 1e-4f);
        //float scale = Mathf.Max(designDeltaTime / safeDeltaTime, 0.01f);
        float rorStrength = Mathf.Clamp(ragDollRotationStrength, 0f, 10f);

        //mass Scaling
        float childMassScale = childRigidbody.mass / Mathf.Max(childRigidbody.mass, 1e-3f);
        float parentMassScale = parentRigidbody.mass / Mathf.Max(parentRigidbody.mass, 1e-3f);

        //local and world positions
        Quaternion parentRotationWorld = parentRigidbody.rotation;
        //Quaternion targetRotationWorld = parentRotationWorld * targetTransform.localRotation;

        Quaternion targetParentRotationWorld = targetTransform.parent.rotation;
        Quaternion targetLocalRotation = Quaternion.Inverse(targetParentRotationWorld) * targetTransform.rotation;

        Quaternion targetRotationWorld = parentRotationWorld * targetLocalRotation;





        Quaternion rotationError = targetRotationWorld * Quaternion.Inverse(childRigidbody.rotation);
        rotationError.ToAngleAxis(out float angleDegrees, out Vector3 errorAxisWorld);

        if (angleDegrees > 180f) angleDegrees -= 360f;
        float angleRadians = angleDegrees * Mathf.Deg2Rad;

        if (errorAxisWorld.sqrMagnitude < 1e-8f)
            errorAxisWorld = Vector3.zero;
        else
            errorAxisWorld.Normalize();

        angleRadians = Mathf.Clamp(angleRadians, -PDVariables.maximumAngleRadians, PDVariables.maximumAngleRadians);

        Vector3 angularVelocityErrorWorld = childRigidbody.angularVelocity - parentRigidbody.angularVelocity;


        float childInertiaMag = childRigidbody.inertiaTensor.magnitude;
        float inertiaScale = childInertiaMag / Mathf.Max(j.designRotationInertia, 1e-3f);
        float proportionalRotationScaled = proportionalGainRotation * (rorStrength * rorStrength) * inertiaScale;
        float derivativeRotationScaled = derivativeGainRotation * rorStrength * Mathf.Sqrt(inertiaScale);


        float normalizedAngle = Mathf.Clamp01(Mathf.Abs(angleRadians) / Mathf.Max(PDVariables.maximumAngleRadians, 1e-4f));

        float rotationMult = /*rotationErrorCurve.Evaluate(normalizedAngle);*/ normalizedAngle;

        //needs logic here to increase PD torque based on ragDollRotationStrength
        //Also logic to increase dampening to avoid jitter without making it too sluggish 
        //rotation strength can be default value of 1 or from 0 to 10x, try to make this wihtout jitter

        Vector3 torqueAccelerationWorld = (proportionalRotationScaled * rotationMult * angleRadians) * errorAxisWorld - (derivativeRotationScaled * (Mathf.Sqrt(rotationMult)) * angularVelocityErrorWorld);

        //torqueAccelerationWorld *= rotationMult;

        if (torqueAccelerationWorld.sqrMagnitude > PDVariables.maximumTorqueAcceleration * PDVariables.maximumTorqueAcceleration)
            torqueAccelerationWorld = torqueAccelerationWorld.normalized * PDVariables.maximumTorqueAcceleration;
        if (ragDollRotationStrength > 0.01f)
            childRigidbody.AddTorque(torqueAccelerationWorld, ForceMode.Force);
    }

    float GetAngleAroundAxis(Quaternion rotation, Vector3 axis)
    {
        axis = axis.normalized;
        if (axis.sqrMagnitude < 1e-8f)
            return 0f;

        rotation = Quaternion.Normalize(rotation);

        Vector3 vectorRot = new Vector3(rotation.x, rotation.y, rotation.z);

        Vector3 proj = Vector3.Dot(vectorRot, axis) * axis;

        Quaternion twist = new Quaternion(proj.x, proj.y, proj.z, rotation.w);

        float sqMag = twist.x * twist.x +twist.y * twist.y +twist.z * twist.z + twist.w * twist.w;

        if (sqMag < 1e-8f)
            return 0f;

        twist = Quaternion.Normalize(twist);

        twist.ToAngleAxis(out float angleDeg, out Vector3 outAxis);

        if (angleDeg > 180f)
            angleDeg -= 360f;

        Vector3 twistVec = new Vector3(twist.x, twist.y, twist.z);
        float sign = Vector3.Dot(axis, twistVec) >= 0f ? 1f : -1f;

        return angleDeg * sign;
    }

    void SolveAngularLimits(XpbdJoint joint, float dt)
    {
        if (!joint.enableAngularLimits)
            return;
        if (joint.parent == null || joint.child == null)
            return;
        if (dt <= 0f)
            return;

        var parent = joint.parent;
        var child = joint.child;

        Quaternion relRotNow = Quaternion.Inverse(parent.rotation) * child.rotation;

        
        Quaternion inverseRestRot = Quaternion.Inverse(joint.restChildLocalRotation);
        Quaternion rotDiff = relRotNow * inverseRestRot;
        rotDiff.Normalize();

        if (joint.twistAxisParent == Vector3.zero &&
            joint.swing1AxisParent == Vector3.zero &&
            joint.swing2AxisParent == Vector3.zero)
        {
            Debug.LogWarning($"[XPBD] {joint.child.name}: angular axes NOT baked (all zero)");
        }

        if (joint.twistAxisParent.sqrMagnitude > 1e-6f)
            SolveAngularLimitAxis(joint, dt, rotDiff, joint.twistAxisParent, joint.twistLimits);

        if (joint.swing1AxisParent.sqrMagnitude > 1e-6f)
            SolveAngularLimitAxis(joint, dt, rotDiff, joint.swing1AxisParent, joint.swing1Limits);

        if (joint.swing2AxisParent.sqrMagnitude > 1e-6f)
            SolveAngularLimitAxis(joint, dt, rotDiff, joint.swing2AxisParent, joint.swing2Limits);
    }

    void SolveAngularLimitAxis(XpbdJoint joint, float dt, Quaternion qDelta, Vector3 axisParent, Vector2 limitsDeg)
    {
        var parent = joint.parent;
        var child = joint.child;
        if (parent == null || child == null)
            return;
        if (joint.angularLimitProfile == null)
            return;

        Vector3 axisParentNorm = axisParent.normalized;
        if (axisParentNorm.sqrMagnitude < 1e-6f)
            return;

        float angleDeg = GetAngleAroundAxis(qDelta, axisParentNorm);

        float min = limitsDeg.x;
        float max = limitsDeg.y;
        if (min > max)
        {
            float tmp = min; min = max; max = tmp;
        }

        float clamped = Mathf.Clamp(angleDeg, min, max);
        float violationDeg = angleDeg - clamped;

        if (Mathf.Abs(violationDeg) < 0.01f)
            return;

        float violationRad = violationDeg * Mathf.Deg2Rad;
        float error = violationRad;

        
        const float maxCorrectionRad = 30f * Mathf.Deg2Rad;
        error = Mathf.Clamp(error, -maxCorrectionRad, maxCorrectionRad);

        Vector3 axisWorld = parent.rotation * axisParentNorm;
        if (axisWorld.sqrMagnitude < 1e-6f)
            return;
        axisWorld.Normalize();

        Vector3 wA = parent.angularVelocity;
        Vector3 wB = child.angularVelocity;
        float Cdot = Vector3.Dot(wB - wA, axisWorld);

        const float maxCdot = 50f; 
        Cdot = Mathf.Clamp(Cdot, -maxCdot, maxCdot);

        float stiffness01 = Mathf.Max(joint.angularLimitProfile.limitStiffness, 0f); 
        float damping01 = Mathf.Max(joint.angularLimitProfile.limitDamping, 0f);   
        float parentInf = Mathf.Clamp01(joint.angularLimitProfile.parentLimitInfluence);
        float maxImpulse = Mathf.Max(joint.angularLimitProfile.maxLimitImpulse, 0f);
        float compliance = joint.angularLimitProfile.limitCompliance;

        float dtSafe = Mathf.Max(dt, 1e-4f);


        float stiffness = stiffness01 * 0.5f;
        float bias = stiffness * error / dtSafe;      

        float damp = damping01;
        float CdotDamped = Cdot * (1f + damp);

        float kA = GetInverseInertiaAlongAxis(parent, axisWorld) * parentInf;
        float kB = GetInverseInertiaAlongAxis(child, axisWorld);
        float k = kA + kB;
        if (k <= 1e-8f)
            return;

        float alpha = compliance / (dtSafe * dtSafe);
        float denom = k + alpha;
        if (denom <= 1e-8f)
            return;

        float deltaLambda = -(CdotDamped + bias) / denom;

        if (maxImpulse > 0f)
            deltaLambda = Mathf.Clamp(deltaLambda, -maxImpulse, maxImpulse);
        else
            deltaLambda = Mathf.Clamp(deltaLambda, -50f, 50f); 

        Vector3 impulse = deltaLambda * axisWorld;

        if (!parent.isKinematic && kA > 0f)
        {
            Vector3 deltaWA = ApplyInvInertiaWorld(parent, -impulse);
            parent.angularVelocity += deltaWA;
        }

        if (!child.isKinematic && kB > 0f)
        {
            Vector3 deltaWB = ApplyInvInertiaWorld(child, impulse);
            child.angularVelocity += deltaWB;
        }
    }

    [ContextMenu("Bake All Joints From Current Pose")]
    void BakeAllFromCurrentPose()
    {
        foreach (var j in joints)
        {
            if (j == null || j.parent == null || j.child == null)
                continue;


            Vector3 pivot = j.child.transform.position;
            j.BakeAnchorsFromWorldPivot(pivot);
        }
        foreach (var j in ragdollJoints)
        {
            if (j == null || j.parent == null || j.child == null)
                continue;


            Vector3 pivot = j.child.transform.position;
            j.BakeAnchorsFromWorldPivot(pivot);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (joints == null)
            return;

        foreach (var j in joints)
        {
            if (j == null) continue;
            j.DrawAngularLimitGizmos();
        }

        if (ragdollJoints == null)
            return;

        foreach (var rj in ragdollJoints)
        {
            if (rj == null) continue;
            rj.DrawAngularLimitGizmos();
        }
    }
}


[Serializable]
public class XpbdJoint
{
    [Header("Bodies")]
    public Rigidbody parent;
    public Rigidbody child;

    public NetworkRigidbody3D rb3d;

    [Header("Anchors")]
    public Vector3 parentAnchorLocal = Vector3.zero;
    public Vector3 childAnchorLocal = Vector3.zero;

    [Header("Pose target transform / bone (on animator)")]
    public Transform targetTransform;

    public bool jointActive = true;

    public PhysicsObject po;
    public float tmpSizeMult;
    // /////// position constraint


    [Header("Distance constraint")]
    public bool enableDistanceConstraint = true;
    public float restLength = 0f;         
    [Range(0f, 1f)] public float distanceStiffness = 1f;  
    public float distanceCompliance = 0f;   
    [Range(0f, 1f)]
    public float distanceDamping = 0.5f;
    [Range(0f, 2f)]
    public float leverArmScale = 0.2f;
    public float maxDistanceImpulse = 20f;


    // /////// rotation PD

    [Header("Rotation Drive")]
    public bool enableRotationPD = true;

    public float rotationKp = 150f;

    public float rotationKd = 20f;

    public float maxTorque = 8000f;

    [Range(0f, 1f)]
    public float parentRotationInfluence = 0.5f;

    public Quaternion restChildLocalRotation = Quaternion.identity;

    public float designRotationInertia = 1f;

    public Quaternion worldTargetToChild = Quaternion.identity;



    // ///////angular constraint


    [Header("Angular limits")]
    public bool enableAngularLimits = false;
    public XpbdAngularLimitProfile angularLimitProfile;

    public JointAxisDirection twistAxis = JointAxisDirection.X;

    public Vector2 twistLimits = new Vector2(-45f, 45f);

    public Vector2 swing1Limits = new Vector2(-30f, 30f);

    public Vector2 swing2Limits = new Vector2(-30f, 30f);

    [Header("Angular limit gizmos")]
    public bool drawAngularLimitGizmos = true;
    public float angularLimitGizmoSize = 0.25f;

    [Range(0f, 1f)]
    public float anchorParentInfluence = 0.5f;

    [HideInInspector] public float bakedScale = 1f;

    [HideInInspector] public float frameLambdaDistance;   
    [HideInInspector] public Vector3 lastDistanceDir;
    [HideInInspector] public float lambdaDistance;
    [HideInInspector] public float lambdaRotation;

    // Precomputed parent-space axes at rest (for solver)
    [HideInInspector] public Vector3 twistAxisParent;
    [HideInInspector] public Vector3 swing1AxisParent;
    [HideInInspector] public Vector3 swing2AxisParent;

    public enum JointAxisDirection
    {
        X,
        Y,
        Z,
        NegativeX,
        NegativeY,
        NegativeZ
    }

    public void BakeAnchorsFromWorldPivot(Vector3 jointPivotWorld)
    {
        if (parent == null || child == null)
        {
            Debug.LogWarning("[Joint] Cannot bake anchors: missing rigidbodies.");
            return;
        }

        parentAnchorLocal = parent.transform.InverseTransformPoint(jointPivotWorld);
        childAnchorLocal = child.transform.InverseTransformPoint(jointPivotWorld);

        restLength = 0f;

        if (targetTransform != null)
        {

            worldTargetToChild = child.rotation * Quaternion.Inverse(targetTransform.rotation);
        }
        else
        {
            worldTargetToChild = Quaternion.identity;
        }


        restChildLocalRotation = Quaternion.Inverse(parent.rotation) * child.rotation;

        Vector3 twistLocal = AxisToVector(twistAxis);
        Vector3 swing1Local, swing2Local;
        GetSwingAxes(twistAxis, out swing1Local, out swing2Local);


        twistAxisParent = (restChildLocalRotation * twistLocal).normalized;
        swing1AxisParent = (restChildLocalRotation * swing1Local).normalized;
        swing2AxisParent = (restChildLocalRotation * swing2Local).normalized;

        if (twistAxisParent == Vector3.zero &&
            swing1AxisParent == Vector3.zero &&
            swing2AxisParent == Vector3.zero)
        {
            Debug.LogError($"[Joint] {child.name}: baked angular axes are all zero – check parent/child rotations at bake time");
        }


        lambdaDistance = 0f;
        lambdaRotation = 0f;
        rb3d = child.GetComponent<NetworkRigidbody3D>();
        // Debug.Log($"baked Ragdoll joint for {this.child.name}");

        bakedScale = parent.transform.lossyScale.y;
        po = parent.GetComponent<PhysicsObject>();
    }
    public void DrawAngularLimitGizmos()
    {
        if (!enableAngularLimits || !drawAngularLimitGizmos)
            return;
        if (parent == null || child == null)
            return;

        if (twistAxisParent == Vector3.zero &&
            swing1AxisParent == Vector3.zero &&
            swing2AxisParent == Vector3.zero)
            return;

        Vector3 pivotWorld =
            parentAnchorLocal != Vector3.zero
            ? parent.transform.TransformPoint(parentAnchorLocal)
            : child.worldCenterOfMass;

        Quaternion parentRot = parent.rotation;

        Vector3 twistWorld = (parentRot * twistAxisParent).normalized;
        Vector3 swing1World = (parentRot * swing1AxisParent).normalized;
        Vector3 swing2World = (parentRot * swing2AxisParent).normalized;

        float size = angularLimitGizmoSize;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(pivotWorld, pivotWorld + twistWorld * size * 1.3f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(pivotWorld, pivotWorld + swing1World * size);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(pivotWorld, pivotWorld + swing2World * size);

       
        DrawArc(pivotWorld, twistWorld, swing1World, twistLimits.x, twistLimits.y,size * 0.9f, Color.red);

        DrawArc(pivotWorld, swing1World, twistWorld, swing1Limits.x, swing1Limits.y,size * 0.8f, Color.green);

        DrawArc(pivotWorld, swing2World, twistWorld, swing2Limits.x, swing2Limits.y,size * 0.8f, Color.blue);
    }

    private static Vector3 AxisToVector(JointAxisDirection axis)
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

    private static void GetSwingAxes(JointAxisDirection twist, out Vector3 swing1, out Vector3 swing2)
    {
        JointAxisDirection absTwist = twist;
        if (twist == JointAxisDirection.NegativeX) absTwist = JointAxisDirection.X;
        if (twist == JointAxisDirection.NegativeY) absTwist = JointAxisDirection.Y;
        if (twist == JointAxisDirection.NegativeZ) absTwist = JointAxisDirection.Z;

        switch (absTwist)
        {
            case JointAxisDirection.X:
                swing1 = Vector3.up;
                swing2 = Vector3.forward;
                break;
            case JointAxisDirection.Y:
                swing1 = Vector3.forward;
                swing2 = Vector3.right;
                break;
            case JointAxisDirection.Z:
            default:
                swing1 = Vector3.right;
                swing2 = Vector3.up;
                break;
        }
    }

    private static void DrawArc(Vector3 center,Vector3 axis,Vector3 startDir, float angleMin,float angleMax, float radius, Color color)
    {
        Gizmos.color = color;

        int segments = 16;
        float startA = angleMin;
        float endA = angleMax;
        float step = (endA - startA) / segments;

        Vector3 prev = center + Quaternion.AngleAxis(startA, axis) * (startDir.normalized * radius);

        for (int i = 1; i <= segments; i++)
        {
            float a = startA + step * i;
            Vector3 next = center + Quaternion.AngleAxis(a, axis) * (startDir.normalized * radius);
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }


    public void SleepBone(bool _hasStateAuth)
    {
        if (child != null)
        {
            child.angularVelocity = Vector3.zero;
            child.linearVelocity = Vector3.zero;
        }

      

        jointActive = false;
    }

    public void AddForcesAndApplyPhycis(bool _hasStateAuth)
    {
        if (child == null || targetTransform == null)
            return;

        Vector3 pos = targetTransform.position;
        Quaternion rot = targetTransform.rotation;

        child.position = pos;
        child.rotation = rot;

        if (rb3d != null && rb3d.Object != null && rb3d.Object.HasStateAuthority)
        {
            rb3d.Teleport(pos, rot);
        }

        if (parent != null)
        {
            child.linearVelocity = parent.linearVelocity;
            child.angularVelocity = parent.angularVelocity;
        }

        jointActive = true;
    }



}

struct BodySnapshot
{
    public Rigidbody rb;
    public Vector3 prePosition;
    public Quaternion preRotation;

    public Vector3 preLinearVelocity;
    public Vector3 preAngularVelocity;
}