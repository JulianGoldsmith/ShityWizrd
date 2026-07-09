using UnityEngine;

public class ObjectMaterial : ScriptableObject
{
    [Header("Base Simulation Properties")]
    [Tooltip("The Layer 0 properties when the object is at rest with no effects.")]
    public SimProperties defaultSimProperties;

    public virtual SimProperties GetSimProperties(MaterialState currentState) {

        SimProperties finalSim = defaultSimProperties;


        return finalSim;
    }
}


