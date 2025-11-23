using UnityEngine;

[CreateAssetMenu(fileName = "PDAngularLimitSettings",menuName = "PD Ragdoll/Angular Limit Settings",order = 0)]
public class PDAngularLimitSettings : ScriptableObject
{
    [Header("General")]
    public float limitAngleToleranceDeg = 1f; 

    [Header("Limit PD (shared)")]
    [Tooltip("Base stiffness for ALL angular limits (twist + swings).")]
    public float baseStiffness = 200f;

    [Tooltip("Base damping for ALL angular limits (twist + swings).")]
    public float baseDamping = 20f;

    [Tooltip("Global multiplier to turn all limit PD up/down at once.")]
    public float globalStrength = 1f;

    [Header("Per-axis multipliers")]
    [Tooltip("Multiplier for twist axis limits.")]
    public float twistMultiplier = 1f;

    [Tooltip("Multiplier for first swing axis.")]
    public float swing1Multiplier = 1f;

    [Tooltip("Multiplier for second swing axis.")]
    public float swing2Multiplier = 1f;

    [Range(0,1)]
    public float generalAngularDamp = 0.3f;
    [Range(0, 1)]
    public float generalLinearDamp = 0.3f;
}
