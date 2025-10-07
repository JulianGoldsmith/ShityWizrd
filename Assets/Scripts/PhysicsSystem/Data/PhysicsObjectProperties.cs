using Fusion;
using UnityEngine;

[System.Serializable]
public struct PhysicsObjectProperties : INetworkStruct
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

    // This remains as a struct so that it can be automatically
    // serialised and networked by Photon (user-defined Blittable Structs
    // are fine).

    // As an INetworkStruct, it cannot contain any:
    // - reference types
    // - string or char (use NetworkString instead).
    // - bools (use NetworkBool or int instead)

    #region Base Properties
    [Promotable("Material", DataTypeTag.Material)]
    public PHYSICS_OBJECT_MATERIAL material_label;
    //private PhysicsObjectMaterial _physicsobjectmaterial;
    public PhysicsObjectMaterial physicsobjectmaterial
    {
        get
        {
            //if (_physicsobjectmaterial == null
            //    || _physicsobjectmaterial.label != material_label)
            //{
            //    _physicsobjectmaterial = POMLookUp.Get(material_label);
            //}
            //return _physicsobjectmaterial;
            return POMLookUp.Get(material_label);
        }
    }

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
    public float brittleness
    {
        get { return (physicsobjectmaterial != null) ? physicsobjectmaterial.brittleness : 0.0f; }
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
            // mass should be size**3 and moment of inertia size**2,
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
