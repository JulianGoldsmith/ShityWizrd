using Fusion;
using Fusion.Addons.Physics;
using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;


[Serializable]
public class PdBone
{
    [Header("Targets and RB's")]
    public Rigidbody childRigidbody;
    private NetworkRigidbody3D rb3d => childRigidbody.GetComponent<NetworkRigidbody3D>();
    public Rigidbody parentRigidbody;

    public Transform targetTransform;
    public Transform smoothingTransform;
    // ---------------- ROTATION ----------------

    [Header("Rotation")]
    [Tooltip("Base rotation stiffness. Think: how strongly this joint pulls towards the target rotation at strength = 1.")]
    public float proportionalGainRotation = 20f;

    //[Range(0f, 2f)]
    //[Tooltip("Rotation damping amount. 0 = no damping, ~0.3-0.6 is usually good, 1 is very heavy damping.")]
    //public float rotationDampingAmount = 0.35f;
    public float derivativeGainRotation = 1f;

    [Tooltip("Rotation error curve based on normalized angle error (0..1).")]
    public AnimationCurve rotationErrorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Rotation design inertia")]
    [Tooltip("Design-time scalar inertia for this bone. Baked from the child rigidbody and can be overridden.")]
    [HideInInspector] public float designRotationInertia = 1f;

    // ---------------- POSITION ----------------

    [Header("Position")]
    public bool usePositionDrive = false;
    [HideInInspector] public Vector3 parentAnchorLocal = Vector3.zero;
    [HideInInspector] public Vector3 childAnchorLocal = Vector3.zero;
    [HideInInspector] public Quaternion worldTargetToChild;

    [Tooltip("Design-time positional stiffness. Think: how strongly the anchors pull together at tightness = 1.")]
    public float proportionalGainPosition = 700f;

    public float derivativeGainPosition = 15f;

    public AnimationCurve positionErrorCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("ReactionForce")]
    [Tooltip("If true, apply equal and opposite forces at the parent anchor to share reaction.")]
    public bool applyEqualAndOppositeForce = false;

    [Tooltip("How much of the child force is mirrored onto the parent. 1 = full, 0 = none.")]
    public float forceTransfereRatio = 1f;

    [Header("Design masses")]
    [Tooltip("Design-time mass for the child rigidbody. Baked from childRigidbody.mass and can be overridden.")]
    [HideInInspector] public float designChildMass = 1f;

    [Tooltip("Design-time mass for the parent rigidbody. Baked from parentRigidbody.mass and can be overridden.")]
    [HideInInspector] public float designParentMass = 1f;

    public float childLeverArmRatio = 0.2f;
    public float parentLeverArmRatio = 0.2f;

    const float snapDistance = 4f;


    [Header("Angular Limits (Ragdoll)")]
    [Tooltip("If true, this joint has twist/swing limits in its local joint frame (mainly used in full ragdoll).")]
    public bool useAngularLimits = false;

    [Tooltip("Local axis of the CHILD that is treated as the twist axis (spin axis) for this joint.")]
    public JointAxisDirection twistAxis = JointAxisDirection.X;

    [Tooltip("Twist limits (degrees) around the twist axis, relative to rest pose. x = min, y = max.")]
    public Vector2 twistLimits = new Vector2(-45f, 45f);

    [Tooltip("Swing limits (degrees) around the first perpendicular axis, relative to rest. x = min, y = max.")]
    public Vector2 swing1Limits = new Vector2(-30f, 60f);

    [Tooltip("Swing limits (degrees) around the second perpendicular axis, relative to rest. x = min, y = max.")]
    public Vector2 swing2Limits = new Vector2(-30f, 30f);

    [Tooltip("Draw gizmos for joint frame + angular limits in the Scene view.")]
    public bool drawAngularLimitGizmos = true;

    [Tooltip("Size of the gizmo shapes drawn at the joint pivot.")]
    public float angularLimitGizmoSize = 0.25f;

    [HideInInspector] public Quaternion restChildLocalRotation = Quaternion.identity;

    public PDAngularLimitSettings limitSettings;


    public void Step(float deltaTime, float ragDollRotationStrength = 1, float sizeMult = 1f)
    {
        if (childRigidbody == null || parentRigidbody == null || targetTransform == null)
            return;



        float safeDeltaTime = Mathf.Max(deltaTime, 1e-4f);
        //float scale = Mathf.Max(designDeltaTime / safeDeltaTime, 0.01f);
        float rorStrength = Mathf.Clamp(ragDollRotationStrength, 0f, 10f);

        //mass Scaling
        float childMassScale = childRigidbody.mass / Mathf.Max(designChildMass, 1e-3f);
        float parentMassScale = parentRigidbody.mass / Mathf.Max(designParentMass, 1e-3f);

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
        float inertiaScale = childInertiaMag / Mathf.Max(designRotationInertia, 1e-3f);
        float proportionalRotationScaled = proportionalGainRotation * (rorStrength * rorStrength) * inertiaScale;
        float derivativeRotationScaled = derivativeGainRotation * rorStrength * Mathf.Sqrt(inertiaScale);


        float normalizedAngle = Mathf.Clamp01(Mathf.Abs(angleRadians) / Mathf.Max(PDVariables.maximumAngleRadians, 1e-4f));

        float rotationMult = rotationErrorCurve.Evaluate(normalizedAngle);

        //needs logic here to increase PD torque based on ragDollRotationStrength
        //Also logic to increase dampening to avoid jitter without making it too sluggish 
        //rotation strength can be default value of 1 or from 0 to 10x, try to make this wihtout jitter

        Vector3 torqueAccelerationWorld = (proportionalRotationScaled * rotationMult * angleRadians) * errorAxisWorld - (derivativeRotationScaled * (Mathf.Sqrt(rotationMult)) * angularVelocityErrorWorld);

        //torqueAccelerationWorld *= rotationMult;

        if (torqueAccelerationWorld.sqrMagnitude > PDVariables.maximumTorqueAcceleration * PDVariables.maximumTorqueAcceleration)
            torqueAccelerationWorld = torqueAccelerationWorld.normalized * PDVariables.maximumTorqueAcceleration;
        if(ragDollRotationStrength > 0.01f)
            childRigidbody.AddTorque(torqueAccelerationWorld, ForceMode.Force);
        //if (applyEqualAndOppositeTorque)
        //{
        //    parentRigidbody.AddTorque(-torqueAccelerationWorld, ForceMode.Acceleration);
        //}
        //parentRigidbody.AddTorque(-torqueAccelerationWorld, ForceMode.Acceleration);

        if (usePositionDrive)
        {
            //Vector3 parentAnchorWorld = parentRigidbody.position + parentRigidbody.rotation * parentAnchorLocal;
            //Vector3 childAnchorWorld = childRigidbody.position + childRigidbody.rotation * childAnchorLocal;

            Vector3 parentAnchorWorld = parentRigidbody.transform.TransformPoint(parentAnchorLocal);
            Vector3 childAnchorWorld = childRigidbody.transform.TransformPoint(childAnchorLocal);

            Vector3 parentAnchorVelocityWorld = parentRigidbody.GetPointVelocity(parentAnchorWorld);
            Vector3 childAnchorVelocityWorld = childRigidbody.GetPointVelocity(childAnchorWorld);

            Vector3 positionErrorWorld = parentAnchorWorld - childAnchorWorld;

            float scaledSnapDistance = snapDistance * sizeMult;
            if (positionErrorWorld.sqrMagnitude > scaledSnapDistance * scaledSnapDistance) //teleport if far away
            {
                Vector3 newPos = childRigidbody.position + positionErrorWorld;

                childRigidbody.position = newPos;

                Vector3 dv = parentAnchorVelocityWorld - childAnchorVelocityWorld;
                childRigidbody.linearVelocity += dv;

                childRigidbody.WakeUp();

                return;
            }



            Vector3 velocityErrorWorld = parentAnchorVelocityWorld - childAnchorVelocityWorld;

            float forceMult = positionErrorCurve.Evaluate(velocityErrorWorld.magnitude);

            float proportionalPositionScaled = proportionalGainPosition * childMassScale;
            float derivativePositionScaled = derivativeGainPosition * childMassScale;

            Vector3 forceAccelerationWorld = (proportionalPositionScaled * forceMult * positionErrorWorld) + (derivativePositionScaled * (Mathf.Sqrt(forceMult)) * velocityErrorWorld);

            if (forceAccelerationWorld.sqrMagnitude > PDVariables.maximumForceAcceleration * PDVariables.maximumForceAcceleration)
                forceAccelerationWorld = forceAccelerationWorld.normalized * PDVariables.maximumForceAcceleration;

            //childRigidbody.AddForceAtPosition((forceAccelerationWorld * (parentRigidbody.mass)) / (childRigidbody.mass), childAnchorWorld, ForceMode.Acceleration);
            //if (applyEqualAndOppositeForce)
            //{
            //    parentRigidbody.AddForceAtPosition(((-forceAccelerationWorld * forceTransfereRatio) * (childRigidbody.mass)) / (parentRigidbody.mass), parentAnchorWorld, ForceMode.Acceleration);
            //}
            childRigidbody.AddForceAtPosition(forceAccelerationWorld * designChildMass/** (parentRigidbody.mass))*/ /** designChildMass*/, Vector3.Lerp(childRigidbody.worldCenterOfMass, childAnchorWorld, childLeverArmRatio), ForceMode.Force);
            if (applyEqualAndOppositeForce)
            {
                parentRigidbody.AddForceAtPosition(((-forceAccelerationWorld * forceTransfereRatio) * designChildMass /** (childRigidbody.mass)*/) /** designParentMass*/, Vector3.Lerp(parentRigidbody.worldCenterOfMass, parentAnchorWorld, parentLeverArmRatio), ForceMode.Force);
            }

            
            

            

            if (Vector3.Distance(childAnchorWorld, parentAnchorWorld) > scaledSnapDistance)
            {
                childRigidbody.MovePosition(parentAnchorWorld);
            }

        }
        ApplyAngularLimits(deltaTime);

        if (ragDollRotationStrength <= 0.01f)
        {
            childRigidbody.angularVelocity *= 1 - limitSettings.generalAngularDamp;
            childRigidbody.linearVelocity *= 1 - limitSettings.generalLinearDamp;
        }
    }


    public void ApplyAngularLimits(float deltaTime)
    {
        if (parentRigidbody == null || childRigidbody == null)
            return;

        var ls = Limits;
        if (ls == null || ls.globalStrength <= 0f)
            return;

        Quaternion parentRot = parentRigidbody.rotation;
        Quaternion childRot = childRigidbody.rotation;

        Quaternion childInParent = Quaternion.Inverse(parentRot) * childRot;

        Quaternion jointLocal = Quaternion.Inverse(restChildLocalRotation) * childInParent;

        Vector3 twistLocalAxis = AxisToVector(twistAxis).normalized;
        if (twistLocalAxis.sqrMagnitude < 0.5f)
            twistLocalAxis = Vector3.right;

        DecomposeSwingTwist(jointLocal, twistLocalAxis, out Quaternion swingLocal, out Quaternion twistLocal);

        twistLocal.ToAngleAxis(out float twistAngleDeg, out Vector3 twistAxisLocal);
        if (twistAngleDeg > 180f) twistAngleDeg -= 360f;

        if (twistAxisLocal.sqrMagnitude < 1e-6f)
            twistAxisLocal = twistLocalAxis;
        else
            twistAxisLocal.Normalize();

        float twistSign = Mathf.Sign(Vector3.Dot(twistAxisLocal, twistLocalAxis));
        twistAngleDeg *= twistSign;

        swingLocal.ToAngleAxis(out float swingAngleDeg, out Vector3 swingAxisLocal);
        if (swingAngleDeg > 180f) swingAngleDeg -= 360f;

        if (swingAxisLocal.sqrMagnitude < 1e-6f || Mathf.Approximately(swingAngleDeg, 0f))
        {
            swingAxisLocal = Vector3.zero;
            swingAngleDeg = 0f;
        }
        else
        {
            swingAxisLocal.Normalize();
        }

        GetSwingAxes(twistAxis, out Vector3 swing1LocalAxis, out Vector3 swing2LocalAxis);
        swing1LocalAxis.Normalize();
        swing2LocalAxis.Normalize();

        float dot1 = Vector3.Dot(swingAxisLocal, swing1LocalAxis);
        float dot2 = Vector3.Dot(swingAxisLocal, swing2LocalAxis);

        float swing1AngleDeg = swingAngleDeg * dot1;
        float swing2AngleDeg = swingAngleDeg * dot2;

        Vector3 totalLimitTorqueWorld = Vector3.zero;

        void ApplyAxisPD(
            float currentAngleDeg,
            Vector2 limitsDeg,
            Vector3 localAxisForThisLimit,
            float axisMultiplier  
        )
        {
            float min = limitsDeg.x;
            float max = limitsDeg.y;

            if (currentAngleDeg > min && currentAngleDeg < max)
                return;

            float targetAngleDeg = Mathf.Clamp(currentAngleDeg, min, max);
            float angleErrorDeg = targetAngleDeg - currentAngleDeg; // zero if inside

            if (Mathf.Abs(angleErrorDeg) < ls.limitAngleToleranceDeg)
                return;

            Vector3 worldAxis = (parentRot * restChildLocalRotation) * localAxisForThisLimit;
            if (worldAxis.sqrMagnitude < 1e-6f)
                return;
            worldAxis.Normalize();

            float angleErrorRad = angleErrorDeg * Mathf.Deg2Rad;

            float kp = ls.baseStiffness * ls.globalStrength * axisMultiplier;
            float kd = ls.baseDamping * ls.globalStrength * axisMultiplier;

            if (kp <= 0f && kd <= 0f)
                return;

            float vAlong = Vector3.Dot(childRigidbody.angularVelocity, worldAxis);

            float torqueScalar = kp * angleErrorRad - kd * vAlong;

            Vector3 torqueWorld = torqueScalar * worldAxis;
            totalLimitTorqueWorld += torqueWorld;
        }

        ApplyAxisPD(twistAngleDeg, twistLimits, twistLocalAxis, ls.twistMultiplier);

        ApplyAxisPD(swing1AngleDeg, swing1Limits, swing1LocalAxis, ls.swing1Multiplier);

        ApplyAxisPD(swing2AngleDeg, swing2Limits, swing2LocalAxis, ls.swing2Multiplier);

        if (totalLimitTorqueWorld.sqrMagnitude > 0f)
        {
            float maxSq = PDVariables.maximumTorqueAcceleration * PDVariables.maximumTorqueAcceleration;
            if (totalLimitTorqueWorld.sqrMagnitude > maxSq)
                totalLimitTorqueWorld = totalLimitTorqueWorld.normalized * PDVariables.maximumTorqueAcceleration;

            childRigidbody.AddTorque(totalLimitTorqueWorld, ForceMode.Acceleration);
        }
    }

    private static void DecomposeSwingTwist(Quaternion q, Vector3 twistAxis, out Quaternion swing, out Quaternion twist)
    {
        Vector3 r = new Vector3(q.x, q.y, q.z);
        Vector3 proj = Vector3.Project(r, twistAxis);

        twist = new Quaternion(proj.x, proj.y, proj.z, q.w);
        twist = NormalizeSafe(twist);

        swing = q * Quaternion.Inverse(twist);
    }

    private static Quaternion NormalizeSafe(Quaternion q)
    {
        float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        if (mag > 1e-6f)
        {
            float inv = 1.0f / mag;
            q.x *= inv; q.y *= inv; q.z *= inv; q.w *= inv;
            return q;
        }
        return Quaternion.identity;
    }

    public void BakeAnchorsFromWorldPivot(Vector3 jointPivotWorld)
    {
        if (parentRigidbody == null || childRigidbody == null)
        {
            Debug.LogWarning("[PdBone] Cannot bake anchors: missing rigidbodies.");
            return;
        }

        parentAnchorLocal = parentRigidbody.transform.InverseTransformPoint(jointPivotWorld);
        childAnchorLocal = childRigidbody.transform.InverseTransformPoint(jointPivotWorld);
        worldTargetToChild = childRigidbody.rotation * Quaternion.Inverse(targetTransform.rotation);

        designChildMass = childRigidbody.mass;
        designParentMass = parentRigidbody.mass;

        designRotationInertia = childRigidbody.inertiaTensor.magnitude;

        restChildLocalRotation = Quaternion.Inverse(parentRigidbody.rotation) * childRigidbody.rotation;
    }

    public void DrawAngularLimitGizmos()
    {
        if (!useAngularLimits || !drawAngularLimitGizmos)
            return;
        if (parentRigidbody == null || childRigidbody == null)
            return;

        Vector3 pivotWorld =
            parentAnchorLocal != Vector3.zero
            ? parentRigidbody.transform.TransformPoint(parentAnchorLocal)
            : childRigidbody.worldCenterOfMass;

        Vector3 twistLocal = AxisToVector(twistAxis);
        Vector3 swing1Local, swing2Local;
        GetSwingAxes(twistAxis, out swing1Local, out swing2Local);

        Transform childT = childRigidbody.transform;
        Vector3 twistWorld = childT.TransformDirection(twistLocal).normalized;
        Vector3 swing1World = childT.TransformDirection(swing1Local).normalized;
        Vector3 swing2World = childT.TransformDirection(swing2Local).normalized;

        float size = angularLimitGizmoSize;

        Gizmos.color = Color.red;
        Gizmos.DrawLine(pivotWorld, pivotWorld + twistWorld * size * 1.3f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(pivotWorld, pivotWorld + swing1World * size);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(pivotWorld, pivotWorld + swing2World * size);

        DrawArc(pivotWorld, twistWorld, swing1World, twistLimits.x, twistLimits.y, size * 0.9f, Color.red);
        DrawArc(pivotWorld, swing2World, swing1World, swing1Limits.x, swing1Limits.y, size * 0.8f, Color.green);
        DrawArc(pivotWorld, swing1World, swing2World, swing2Limits.x, swing2Limits.y, size * 0.8f, Color.blue);
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

    private static void DrawArc(Vector3 center,Vector3 axis,Vector3 startDir,float angleMin,float angleMax, float radius,Color color)
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
        childRigidbody.angularVelocity = Vector3.zero;
        childRigidbody.linearVelocity = Vector3.zero;
        rb3d.RBIsKinematic = true;
        childRigidbody.GetComponent<Collider>().enabled = false;
        //rb3d.Teleport(new Vector3(0, 1000, 0), Quaternion.identity);
        if (rb3d.Object.HasStateAuthority)
            rb3d.ResetRigidbody();

        //DeActivateConfigurableJoint();
    }

    public void WakeBone(bool _hasStateAuth) //needs to happen beofre activating
    {
        //rb3d.transform.SetPositionAndRotation(targetTransform.position, targetTransform.rotation);
        
        

        //rb3d.Teleport(targetTransform.position, targetTransform.rotation);
        //no.RemoveInputAuthority();
        //no.ForceRemoteRenderTimeframe = true;
    }

    public void AddForcesAndApplyPhycis(bool _hasStateAuth) //needs to happen after awaken
    {

        Vector3 pos = targetTransform.position;
        Quaternion rot = targetTransform.rotation;

        childRigidbody.position = pos;
        childRigidbody.rotation = rot;

        if (rb3d.Object.HasStateAuthority)
        {
            rb3d.Teleport(pos, rot);
        }

        

        childRigidbody.GetComponent<Collider>().enabled = true;
        rb3d.RBIsKinematic = false;
        rb3d.ResetRigidbody();

        childRigidbody.linearVelocity = parentRigidbody.linearVelocity;
        childRigidbody.angularVelocity = parentRigidbody.angularVelocity;

        if (smoothingTransform != null &&
            smoothingTransform.TryGetComponent<LocalSmoothingForNetworkedRenderTarget>(
                out LocalSmoothingForNetworkedRenderTarget lsfnrt))
        {
            lsfnrt.Teleport(pos, rot);
        }
        //ActivateConfigurableJoint();

        Debug.Log($"[RagdollEnter] {childRigidbody.name} vel {childRigidbody.linearVelocity}, angVel {childRigidbody.angularVelocity}");
        Debug.Log($"[RagdollExit]  {childRigidbody.name} vel {childRigidbody.linearVelocity}, angVel {childRigidbody.angularVelocity}");

    }

    public void ActivateConfigurableJoint()
    {
        if (childRigidbody.TryGetComponent<ConfigurableJoint>(out ConfigurableJoint CJ))
        {
            CJ.autoConfigureConnectedAnchor = false;
            CJ.anchor = childAnchorLocal;
            CJ.connectedAnchor = parentAnchorLocal;
            CJ.connectedBody = parentRigidbody;

            CJ.angularXMotion = ConfigurableJointMotion.Limited;
            CJ.angularYMotion = ConfigurableJointMotion.Limited;
            CJ.angularZMotion = ConfigurableJointMotion.Limited;
            CJ.yMotion = ConfigurableJointMotion.Limited;
            CJ.xMotion = ConfigurableJointMotion.Limited;
            CJ.zMotion = ConfigurableJointMotion.Limited;
            Debug.Log($"Configurable joint activated with current foce of {CJ.currentForce} and torque {CJ.currentTorque}");
        }
    }
    public void DeActivateConfigurableJoint()
    {
        if (childRigidbody.TryGetComponent<ConfigurableJoint>(out ConfigurableJoint CJ))
        {
            CJ.connectedBody = null;

            CJ.angularXMotion = ConfigurableJointMotion.Free;
            CJ.angularYMotion = ConfigurableJointMotion.Free;
            CJ.angularZMotion = ConfigurableJointMotion.Free;
            CJ.yMotion = ConfigurableJointMotion.Free;
            CJ.xMotion = ConfigurableJointMotion.Free;
            CJ.zMotion = ConfigurableJointMotion.Free;

            CJ.connectedAnchor = Vector3.zero;
        }
    }


    public Vector3 GetPositionProjectedToCenterOfMass(Rigidbody rb, Vector3 position, float ratio)
    {
        Vector3 COM = rb.worldCenterOfMass;
        return Vector3.Lerp(COM, position, ratio);
    }

    private PDAngularLimitSettings Limits
    {
        get
        {
            if (limitSettings != null)
                return limitSettings;

            if (_fallbackLimits == null)
            {
                _fallbackLimits = ScriptableObject.CreateInstance<PDAngularLimitSettings>();
            }
            return _fallbackLimits;
        }
    }

    private static PDAngularLimitSettings _fallbackLimits;
}

public static class PDVariables
{
    public const float maximumAngleRadians = 0.7f;
    public const float maximumForceAcceleration = 2000f;
    public const float maximumTorqueAcceleration = 8000f;
}

public enum JointAxisDirection
{
    X,
    Y,
    Z,
    NegativeX,
    NegativeY,
    NegativeZ
}