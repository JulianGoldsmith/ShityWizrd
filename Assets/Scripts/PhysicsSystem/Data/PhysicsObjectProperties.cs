using UnityEngine;

[System.Serializable]
public class PhysicsObjectProperties
{
    /* Define the base properties of a physics object and 
     how they combine in physics calculations. 
        Has to be a class to work with the modifier system.
    Otherwise we modify a value, creating a new struct, and then
        never return that struct to the original object.
    It doesn't really make much of a difference which is is,
        though may need to be careful if multiple objects
        end up having the same instance of POP.
     */


    #region Base Properties
    [Promotable("Material", DataTypeTag.Material)]
    public PhysicsObjectMaterial physicsobjectmaterial;

    #endregion


    #region Inherited Base Properties
    // from PhysicsObjectMaterial (POM)
    // (expose parameters from material for ease of use.
    public float density
    {
        get { return (physicsobjectmaterial != null) ? physicsobjectmaterial.density : 1.0f; }
    }
    public float hardness
    {
        get { return (physicsobjectmaterial != null) ? physicsobjectmaterial.hardness : 1.0f; }
    }
    public float elasticity
    {
        get { return (physicsobjectmaterial != null) ? physicsobjectmaterial.elasticity : 0.0f; }
    }
    public float viscosity
    {
        get { return (physicsobjectmaterial != null) ? physicsobjectmaterial.viscosity : 0.0f; }
    }
    public float stickiness
    {
        get { return (physicsobjectmaterial != null) ? physicsobjectmaterial.stickiness : 0.0f; }
    }

    #endregion


    #region Modifiers
    [Promotable("Size", DataTypeTag.Radius)]
    public float size;

    #endregion


    #region Derived Properties
    public float mass { 
        get 
        {
            // perhaps, mass should be size**3 and moment of inertia size**2,
            // but it might not matter in practice.
            return density * size;
        }
    }
    public float moment_of_inertia
    {
        get
        {
            // perhaps, mass should be size**3 and moment of inertia size**2,
            // but it might not matter in practice.
            return density * size;
        }
    }

    #endregion


    #region Physics Interactions
    // Physics interactions we need to calculate manually, since not
    // dealt with natively through unity/rigidbody.
    #endregion
}
