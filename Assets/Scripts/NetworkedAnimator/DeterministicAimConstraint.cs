using UnityEngine;
using static DeterministicAimConstraint;

[System.Serializable]
public class DeterministicAimConstraint
{
    [Tooltip("Turn the constraint on/off entirely.")]
    public bool IsActive = true;

    [Range(0f, 1f)]
    public float Weight = 1f;

    [Header("Bones & Targets")]
    public Transform ConstrainedBone;
    public Transform Target;

    [Header("Axes Configuration")]
    [Tooltip("The local axis of the bone that should point at the target.")]
    public ConstraintAxis AimAxis = ConstraintAxis.Z;

    [Tooltip("The local axis of the bone that defines 'Up'.")]
    public ConstraintAxis UpAxis = ConstraintAxis.Y;

    [Tooltip("The global 'Up' direction to align against.")]
    public ConstraintAxis WorldUpAxis = ConstraintAxis.Y;

    [Header("Limits")]
    [Tooltip("Maximum angle (in degrees) the bone can bend away from its default animated pose.")]
    [Range(0f, 180f)]
    public float MaxAngleLimit = 90f;

    public void Solve()
    {
        if (!IsActive || Weight <= 0f || ConstrainedBone == null || Target == null) return;

        // 1. Where do we want to look?
        Vector3 directionToTarget = (Target.position - ConstrainedBone.position).normalized;
        if (directionToTarget.sqrMagnitude < 0.001f) return;

        // 2. Convert our nice Enums into Math Vectors
        Vector3 aimVec = AimAxis.ToVector3();
        Vector3 upVec = UpAxis.ToVector3();
        Vector3 worldUpVec = WorldUpAxis.ToVector3();

        // 3. What is the math telling the bone to do?
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget, worldUpVec);

        // Offset the rotation so the specific AimAxis points at the target, not just Z
        Quaternion axisOffset = Quaternion.Inverse(Quaternion.LookRotation(aimVec, upVec));
        Quaternion finalDesiredRotation = targetRotation * axisOffset;

        // 4. Enforce Limits (The Cone of Vision)
        float angleDifference = Quaternion.Angle(ConstrainedBone.rotation, finalDesiredRotation);

        if (angleDifference > MaxAngleLimit)
        {
            float clampWeight = MaxAngleLimit / angleDifference;
            finalDesiredRotation = Quaternion.Slerp(ConstrainedBone.rotation, finalDesiredRotation, clampWeight);
        }

        // 5. Apply the Final Blend
        ConstrainedBone.rotation = Quaternion.Slerp(ConstrainedBone.rotation, finalDesiredRotation, Weight);
    }

    public enum ConstraintAxis
    {
        X,
        Y,
        Z,
        NegativeX,
        NegativeY,
        NegativeZ
    }
}

public static class ConstraintAxisExtensions
{
    public static Vector3 ToVector3(this ConstraintAxis axis)
    {
        switch (axis)
        {
            case ConstraintAxis.X: return Vector3.right;
            case ConstraintAxis.Y: return Vector3.up;
            case ConstraintAxis.Z: return Vector3.forward;
            case ConstraintAxis.NegativeX: return Vector3.left;
            case ConstraintAxis.NegativeY: return Vector3.down;
            case ConstraintAxis.NegativeZ: return Vector3.back;
            default: return Vector3.forward;
        }
    }
}