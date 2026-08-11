using UnityEngine;

public struct ItemPDStepResult
{
    public Vector3 Position;
    public Quaternion Rotation;
    public Vector3 LinearVelocity;
    public Vector3 AngularVelocity;
}


[CreateAssetMenu(fileName = "NewItemPD", menuName = "Items/Item PD Settings")]
public class ItemPD : ScriptableObject
{
    [Header("Position Settings")]
    public float posStiffness = 80f;   
    public float posDamping = 14f;     
    public float maxLinearSpeed = 25f;

    [Header("Position Feel")]
    [Tooltip("X Axis: Normalized Error (0 to 1), Y Axis: Stiffness Multiplier")]
    public AnimationCurve posStiffnessCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));
    [Tooltip("At what distance (meters) does the curve hit 1.0?")]
    public float posMaxErrorDist = 1.0f;

    [Header("Rotation Settings")]
    public float rotStiffness = 40f;
    public float rotDamping = 10f;
    public float maxAngularSpeed = 30f;

    [Header("Rotation Feel")]
    [Tooltip("X Axis: Normalized Error (0 to 1), Y Axis: Stiffness Multiplier")]
    public AnimationCurve rotStiffnessCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));
    [Tooltip("At what angle (degrees) does the curve hit 1.0?")]
    public float rotMaxErrorDeg = 90f;

    [Header("Inertia")]
    [Tooltip("How much of the player's acceleration is added to the item? 0 = detached, 1 = fully weighted.")]
    public float inertiaScale = 0.5f;


    public ItemPDStepResult CalculateStep(Vector3 currentPos, Quaternion currentRot, Vector3 currentLinVel, Vector3 currentAngVel, Vector3 targetPos, Quaternion targetRot, Vector3 targetLinVel, Vector3 targetAngVel, Vector3 targetAccel, float dt)
    {
        float safeDt = Mathf.Max(dt, 0.0001f);

        Vector3 posError = targetPos - currentPos;
        float normalizedPosError = posMaxErrorDist > 0.001f ? Mathf.Clamp01(posError.magnitude / posMaxErrorDist) : 0f;
        float posCurveMult = Mathf.Max(0f, posStiffnessCurve.Evaluate(normalizedPosError));

        float positionKp = Mathf.Max(0f, posStiffness * posCurveMult);
        float positionKd = Mathf.Max(0f, posDamping * Mathf.Sqrt(posCurveMult));

        float positionGain = 1f / (1f + positionKd * safeDt + positionKp * safeDt * safeDt);
        float stablePositionKp = positionKp * positionGain;
        float stablePositionKd = (positionKd + positionKp * safeDt) * positionGain;

        Vector3 linearAcceleration = posError * stablePositionKp + (targetLinVel - currentLinVel) * stablePositionKd;
        linearAcceleration += targetAccel * inertiaScale;

        Vector3 newLinVel = currentLinVel + linearAcceleration * safeDt;
        Vector3 relativeLinVel = newLinVel - targetLinVel;

        if (maxLinearSpeed > 0f && relativeLinVel.sqrMagnitude > maxLinearSpeed * maxLinearSpeed)
        {
            relativeLinVel = relativeLinVel.normalized * maxLinearSpeed;
            newLinVel = targetLinVel + relativeLinVel;
        }

        Vector3 newPos = currentPos + newLinVel * safeDt;

        Quaternion rotationError = Quaternion.Normalize(targetRot * Quaternion.Inverse(currentRot));

        if (rotationError.w < 0f)
        {
            rotationError.x = -rotationError.x;
            rotationError.y = -rotationError.y;
            rotationError.z = -rotationError.z;
            rotationError.w = -rotationError.w;
        }

        rotationError.ToAngleAxis(out float angleDeg, out Vector3 axis);

        if (angleDeg > 180f) angleDeg -= 360f;

        Vector3 angularError = Vector3.zero;

        if (axis.sqrMagnitude > 0.000001f && Mathf.Abs(angleDeg) > 0.0001f)
        {
            angularError = axis.normalized * angleDeg * Mathf.Deg2Rad;
        }

        float normalizedRotError = rotMaxErrorDeg > 0.001f ? Mathf.Clamp01(Mathf.Abs(angleDeg) / rotMaxErrorDeg) : 0f;
        float rotCurveMult = Mathf.Max(0f, rotStiffnessCurve.Evaluate(normalizedRotError));

        float rotationKp = Mathf.Max(0f, rotStiffness * rotCurveMult);
        float rotationKd = Mathf.Max(0f, rotDamping * Mathf.Sqrt(rotCurveMult));

        float rotationGain = 1f / (1f + rotationKd * safeDt + rotationKp * safeDt * safeDt);
        float stableRotationKp = rotationKp * rotationGain;
        float stableRotationKd = (rotationKd + rotationKp * safeDt) * rotationGain;

        Vector3 angularAcceleration = angularError * stableRotationKp + (targetAngVel - currentAngVel) * stableRotationKd;
        Vector3 newAngVel = currentAngVel + angularAcceleration * safeDt;
        Vector3 relativeAngVel = newAngVel - targetAngVel;

        if (maxAngularSpeed > 0f && relativeAngVel.sqrMagnitude > maxAngularSpeed * maxAngularSpeed)
        {
            relativeAngVel = relativeAngVel.normalized * maxAngularSpeed;
            newAngVel = targetAngVel + relativeAngVel;
        }

        Quaternion newRot = currentRot;
        float angularStep = newAngVel.magnitude * safeDt;

        if (angularStep > 0.000001f)
        {
            Quaternion deltaRotation = Quaternion.AngleAxis(angularStep * Mathf.Rad2Deg, newAngVel.normalized);
            newRot = Quaternion.Normalize(deltaRotation * currentRot);
        }

        return new ItemPDStepResult
        {
            Position = newPos,
            Rotation = newRot,
            LinearVelocity = newLinVel,
            AngularVelocity = newAngVel
        };
    }
}