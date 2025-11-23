using UnityEngine;

[CreateAssetMenu(
    menuName = "Physics/XPBD Angular Limit Profile",
    fileName = "AngularLimitProfile")]
public class XpbdAngularLimitProfile : ScriptableObject
{
    [Header("Limit response")]
    [Range(0f, 1f)]
    public float limitStiffness = 0.5f;   // how hard limits push back

    [Range(0f, 1f)]
    public float limitDamping = 0.5f;     // how strongly we damp angular vel at limits

    [Tooltip("XPBD compliance. 0 = hard limit, >0 = softer.")]
    public float limitCompliance = 0f;

    [Tooltip("Max impulse magnitude per iteration for angular limits. 0 = unlimited (not recommended).")]
    public float maxLimitImpulse = 50f;

    [Range(0f, 1f)]
    [Tooltip("0 = only child reacts at limits, 1 = parent and child share correction by inertia.")]
    public float parentLimitInfluence = 0.5f;
}