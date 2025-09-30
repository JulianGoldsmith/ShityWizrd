using UnityEngine;

public class PhysicsObject : MonoBehaviour
{
    /* Defines interactions of physics objects in the physics system.
     * Any object that is expected to interact with physics should have:
     *  - This script
     *  - A rigidbody
     *  - A collider + physicsmaterial
     *  
     *  This script then contains the methods used for physics interactions
     *      as well as the properties (PhysicsObjectProperties) used in those
     *      calculations.
     *  - PhysicsObjectProperties struct with values assigned, including
     *      - PhysicsObjectMaterial (base material)
     *  
     *  Can be inherited to make special behavior for spells versus players, for
     *      example.
     *  
     *  Relevant for spell-objects (e.g. tangible projectiles), 
     *      players, enemies, world-objects.
     */

    public PhysicsObjectProperties physicsObjectProperties;

    #region Initialisation
    // Assign parameter values to Rigidbody and Unity PhysicsMaterials
    public void InitialisePhysicsObject()
    {
        UpdateRigidbody(GetComponent<Rigidbody>());
        Collider col = GetComponent<Collider>();
        Debug.Log(col == null);
        Debug.Log(GetComponent<MeshCollider>() == null);
        if (col != null) UpdatePhysicsMaterial(col.material);
    }
    public Rigidbody UpdateRigidbody(Rigidbody rigidbody)
    {
        if (rigidbody == null) return null;

        rigidbody.mass = physicsObjectProperties.mass;
        rigidbody.linearDamping = physicsObjectProperties.hardness;
        rigidbody.angularDamping = physicsObjectProperties.mass * 0.05f;

        rigidbody.useGravity = true;

        return rigidbody;
    }

    public PhysicsMaterial UpdatePhysicsMaterial(PhysicsMaterial material)
    {
        if (material == null) return null;

        // Friction
        material.staticFriction = physicsObjectProperties.stickiness;
        material.dynamicFriction = physicsObjectProperties.stickiness;
        material.frictionCombine = PhysicsMaterialCombine.Average;

        // Bounciness
        material.bounciness = physicsObjectProperties.elasticity;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;

        return material;
    }
    #endregion

    #region Collisions
    // additional things to happen on collision.
    // [placeholder]
    #endregion

    #region Bonking
    // [placeholder]
    // what happens when the object is 'bonked' (hit in a collision).
    // different implementations for players, enemies, objects, spells.
    #endregion

    #region Sound
    // [placeholder]
    #endregion
}
