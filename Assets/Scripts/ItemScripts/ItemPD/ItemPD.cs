using UnityEngine;

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


    public void CalculateStep(Vector3 currentPos, Quaternion currentRot, Vector3 targetPos, Quaternion targetRot, Vector3 ownerVel, Vector3 ownerAccel,float dt,ref Vector3 linVel, ref Vector3 angVel,
        out Vector3 newPos, out Quaternion newRot)
    {
        float safeDt = Mathf.Max(dt, 1e-4f);

        Vector3 posError = targetPos - currentPos;
        float dist = posError.magnitude;

        float posCurveMult = 1.0f;
        if (posMaxErrorDist > 0.001f)
            posCurveMult = posStiffnessCurve.Evaluate(Mathf.Clamp01(dist / posMaxErrorDist));

        Vector3 relativeVel = linVel - ownerVel;

        Vector3 force = (posError * (posStiffness * posCurveMult)) - (relativeVel * posDamping);

        force += ownerAccel * inertiaScale;

        linVel += force * safeDt;

        float speed = linVel.magnitude;
        if (speed > maxLinearSpeed && speed > 1e-5f)
            linVel *= (maxLinearSpeed / speed);

        newPos = currentPos + linVel * safeDt;

        Quaternion rotError = targetRot * Quaternion.Inverse(currentRot);
        rotError.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (angleDeg > 180f) angleDeg -= 360f;

        if (axis.sqrMagnitude < 1e-6f || Mathf.Abs(angleDeg) < 0.05f)
        {
            newRot = targetRot;
            angVel *= (1.0f - (rotDamping * safeDt));
            return;
        }

        axis.Normalize();

        float rotCurveMult = 1.0f;
        if (rotMaxErrorDeg > 0.001f)
            rotCurveMult = rotStiffnessCurve.Evaluate(Mathf.Clamp01(Mathf.Abs(angleDeg) / rotMaxErrorDeg));

        Vector3 angError = axis * (angleDeg * Mathf.Deg2Rad);

        Vector3 torque = (angError * (rotStiffness * rotCurveMult)) - (angVel * rotDamping);

        angVel += torque * safeDt;

        float angSpeed = angVel.magnitude;
        if (angSpeed > maxAngularSpeed)
            angVel *= (maxAngularSpeed / angSpeed);

        Quaternion deltaRot = Quaternion.identity;
        if (angVel.sqrMagnitude > 0.0001f)
        {
            float deltaAngle = angVel.magnitude * Mathf.Rad2Deg * safeDt;
            deltaRot = Quaternion.AngleAxis(deltaAngle, angVel.normalized);
        }

        newRot = deltaRot * currentRot;
    }
}