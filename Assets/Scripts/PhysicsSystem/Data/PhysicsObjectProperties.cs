using UnityEngine;

public struct PhysicsObjectProperties
{
    /* Define the base properties of a physics object and 
     how they combine in physics calculations. */

    #region Base Properties
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


    #region Parameter Transfer
    // Assign parameter values to Rigidbody and Unity PhysicsMaterials
    public Rigidbody UpdateRigidbody(Rigidbody rigidbody)
    {
        rigidbody.mass = mass;
        rigidbody.linearDamping = 0f;
        rigidbody.angularDamping = 0.05f;
        
        rigidbody.useGravity = true;

        return rigidbody;
    }

    public PhysicsMaterial UpdatePhysicsMaterial(PhysicsMaterial material)
    {
        // Friction
        material.staticFriction = stickiness;
        material.dynamicFriction = stickiness;
        material.frictionCombine = PhysicsMaterialCombine.Average;
        
        // Bounciness
        material.bounciness = elasticity;
        material.bounceCombine = PhysicsMaterialCombine.Average;

        return material;
    }
    #endregion


    #region Physics Interactions
    // Physics interactions we need to calculate manually, since not
    // dealt with natively through unity/rigidbody.
    #endregion
}
