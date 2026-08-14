using UnityEngine;

[DisallowMultipleComponent]
public class CustomPhysicsFormulas : MonoBehaviour
{
    public static CustomPhysicsFormulas Instance { get; private set; }

    private const float DefaultMassScaleExponent = 2f;
    private const float MinimumScale = 0.01f;
    private const float MinimumDensity = 0.01f;
    private const float MinimumMass = 0.01f;

    [Header("Scale To Mass")]
    [SerializeField, Min(0f)] private float massScaleExponent = 1.5f;

    public static float MassScaleExponent => Instance != null ? Instance.massScaleExponent : DefaultMassScaleExponent;
    public static float DistanceConstraintStrengthExponent => MassScaleExponent - 1f;
    public static float AngularConstraintStrengthExponent => MassScaleExponent + 1f;
    public static float DistanceDampingExponent => (DistanceConstraintStrengthExponent + MassScaleExponent) * 0.5f;
    public static float AngularDampingExponent => (AngularConstraintStrengthExponent + MassScaleExponent + 2f) * 0.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("More than one CustomPhysicsFormulas component exists.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public static float CalculateDensity(float density, float densityMultiplier)
    {
        return Mathf.Max(MinimumDensity, density * densityMultiplier);
    }

    public static float CalculateMassScale(float scale)
    {
        return Mathf.Pow(Mathf.Max(MinimumScale, scale), MassScaleExponent);
    }

    public static float CalculateScalePower(float scale, float exponent)
    {
        return Mathf.Pow(Mathf.Max(MinimumScale, scale), exponent);
    }

    public static float ClampMass(float mass)
    {
        return Mathf.Max(MinimumMass, mass);
    }
}