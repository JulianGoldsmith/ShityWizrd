// This attribute allows you to create instances of this class from the Assets > Create menu.
using UnityEngine;

[CreateAssetMenu(fileName = "NewHandState", menuName = "Hand State/Hand State")]
public class HandState : ScriptableObject
{
    

    [Header("Position Spring")]
    public float positionSpringStrength = 20;
    public float positionSpringDamper = 25f;

    [Header("Rotation Spring")]
    public float rotationSpringStrength = 250f;
    public float rotationSpringDamper = 25f;

    [Header("Target Offsets")]
    public Vector3 handOffsetFromShoulder = new Vector3(0.3f, -0.15f, 0.57f);
    public Vector3 handRotationOffset = new Vector3(-32.7f, 13f, -50f);

    [Header("Procedural Hand Bob")]
    public float handBobSpeed = 1f;
    public float handBobAmountX = 0.05f;
    public float handBobAmountY = 0.06f;

    [SerializeField] public AnimationCurve forceRelativeToDistance;
    [SerializeField] public float maxDistance = 1f, maxStrengthMultiplier = 50f;

    [Header("Animation")]
    [Tooltip("The looping animation for the 'Idle' state.")]
    public AnimationClip idleClip;
    [Tooltip("windup animation")]
    public AnimationClip windupClip;
    [Tooltip("hold animation")]
    public AnimationClip holdClip;
    [Tooltip("release animation")]
    public AnimationClip releaseClip;
}