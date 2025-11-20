using System;
using UnityEngine;
using System.Collections.Generic;

public class XpbdConstraintSolver : MonoBehaviour
{
    [Tooltip("All joints this solver should handle.")]
    public List<XpbdJoint> joints = new List<XpbdJoint>();

    const float constraintVelocityScale = 1.0f;

    [Tooltip("Global scale for how strongly distance constraints pull (Baumgarte bias).")]
    public float distanceBiasScale = 1f;

    [Tooltip("Number of XPBD iterations per tick.")]
    public int iterations = 4;

    public void Solve(float deltaTime)
    {
        if (joints.Count == 0 || deltaTime <= 0f)
            return;


        var bodySnapshots = BuildBodySnapshots();

        foreach (var joint in joints)
        {
            if (joint != null)
            {
                joint.lambdaDistance = 0f;
                joint.lambdaRotation = 0f;
                joint.frameLambdaDistance = 0f;
            }
        }


        for (int iter = 0; iter < iterations; iter++)
        {
            foreach (var joint in joints)
            {
                if (joint == null)
                    continue;

                if (joint.enableDistanceConstraint)
                    SolveDistanceConstraintVelocity(joint, deltaTime);

            }
        }

    }

    Dictionary<Rigidbody, BodySnapshot> BuildBodySnapshots()
    {
        var dict = new Dictionary<Rigidbody, BodySnapshot>();

        foreach (var j in joints)
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

    void SolveDistanceConstraintVelocity(XpbdJoint j, float dt)
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

        float beta = distanceBiasScale * j.distanceStiffness;  
        beta = Mathf.Max(beta, 0f);

        float damp = Mathf.Clamp01(j.distanceDamping);


        float bias = beta * C / Mathf.Max(dt, 1e-4f);

   
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
        float ragDollRotationStrength = 1;

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
    }
}


[Serializable]
public class XpbdJoint
{
    [Header("Bodies")]
    public Rigidbody parent;
    public Rigidbody child;

    [Header("Anchors (local to each body)")]
    public Vector3 parentAnchorLocal = Vector3.zero;
    public Vector3 childAnchorLocal = Vector3.zero;

    [Header("Pose target (world / parent-space)")]
    public Transform targetTransform;

    [Header("Distance constraint")]
    public bool enableDistanceConstraint = true;
    public float restLength = 0f;         
    [Range(0f, 1f)] public float distanceStiffness = 1f;  
    public float distanceCompliance = 0f;   
    [Range(0f, 1f)]
    [Tooltip("Damping for distance constraint. 0 = no velocity correction, 1 = full.")]
    public float distanceDamping = 0.5f;
    [Range(0f, 2f)]
    [Tooltip("Scales the effective lever arm from COM -> anchor. <1 = less torque from movement.")]
    public float leverArmScale = 0.2f;
    [Tooltip("Max impulse magnitude along the constraint per iteration (safety clamp). 0 = unlimited.")]
    public float maxDistanceImpulse = 20f;

    [Header("Rotation PD (pose follow)")]
    public bool enableRotationPD = true;

    [Tooltip("Proportional gain (stiffness). Higher = snappier.")]
    public float rotationKp = 150f;

    [Tooltip("Derivative gain (damping). Higher = less overshoot.")]
    public float rotationKd = 20f;

    [Tooltip("Torque clamp per joint (N·m). 0 = unlimited (not recommended).")]
    public float maxTorque = 8000f;

    [Range(0f, 1f)]
    [Tooltip("0 = only child rotates, 1 = parent and child share correction physically.")]
    public float parentRotationInfluence = 0.5f;

    [Tooltip("Child's rotation relative to parent at bake time (optional reference).")]
    public Quaternion restChildLocalRotation = Quaternion.identity;

    [Tooltip("Reference magnitude of child's inertia tensor at bake time (optional).")]
    public float designRotationInertia = 1f;

    [Tooltip("Offset between target rotation and child at bake time: child = target * worldTargetToChild")]
    public Quaternion worldTargetToChild = Quaternion.identity;

    

    [Range(0f, 1f)]
    public float anchorParentInfluence = 0.5f;

    [NonSerialized] public float frameLambdaDistance;   
    [NonSerialized] public Vector3 lastDistanceDir;
    [NonSerialized] public float lambdaDistance;
    [NonSerialized] public float lambdaRotation;

    public void BakeAnchorsFromWorldPivot(Vector3 jointPivotWorld)
    {
        if (parent == null || child == null)
        {
            Debug.LogWarning("[XpbdJoint] Cannot bake anchors: missing rigidbodies.");
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

        designRotationInertia = child.inertiaTensor.magnitude;

        restChildLocalRotation = Quaternion.Inverse(parent.rotation) * child.rotation;

        lambdaDistance = 0f;
        lambdaRotation = 0f;

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