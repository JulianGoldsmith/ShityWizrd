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


