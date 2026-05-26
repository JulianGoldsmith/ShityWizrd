using UnityEngine;

public class ObjectMaterial : ScriptableObject
{
    [Header("Base Simulation Properties")]
    [Tooltip("The Layer 0 properties when the object is at rest with no effects.")]
    public SimProperties defaultSimProperties;

    public virtual SimProperties GetSimProperties(MaterialState currentState) {

        SimProperties finalSim = defaultSimProperties;

        float volumeScale = currentState.ScaleMultiplier * currentState.ScaleMultiplier;
        finalSim.Mass = finalSim.Mass * currentState.DensityMultiplier * volumeScale;

        finalSim.Friction -= currentState.Lubrication;
        finalSim.Friction = Mathf.Max(0, finalSim.Friction);

        finalSim.Restitution += currentState.Rubberization;

        return finalSim;
    }
}

[System.Serializable]
public struct SimProperties
{
    public float Mass;
    public float Friction;
    public float Restitution; // Bounce
    public float LinearDrag;
    public float AngularDrag;

    public float ThermalConductance;
    public float ElectricalConductance;

    public float Brittleness; // Impact fracture threshold
    public float Adhesion;    // Force required to pull away
}

public struct MaterialState
{
    public float Temperature;
    public float Wetness;
    public float Charge;

    public float Rubberization;
    public float Lubrication;

    public float ScaleMultiplier;
    public float DensityMultiplier;

    public void Reset()
    {
        Temperature = 0f;
        Wetness = 0f;
        Charge = 0f;
        Rubberization = 0f;
        Lubrication = 0f;
        ScaleMultiplier = 1f;
        DensityMultiplier = 1f;
    }
}