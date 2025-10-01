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
    private Rigidbody rigidbody;
    private PhysicsMaterial physicsMaterial;

    #region Initialisation
    // Assign parameter values to Rigidbody and Unity PhysicsMaterials
    public void InitialisePhysicsObject()
    {
        UpdateRigidbody(GetComponent<Rigidbody>());
        Collider col = GetComponent<Collider>();
        if (col != null) UpdatePhysicsMaterial(col.material);
        UpdateVisuals();
    }
    public Rigidbody UpdateRigidbody(Rigidbody rb)
    {
        if (rb == null) return null;

        rigidbody = rb;

        rigidbody.mass = physicsObjectProperties.mass;
        rigidbody.linearDamping = physicsObjectProperties.hardness;
        rigidbody.angularDamping = physicsObjectProperties.mass * 0.05f;

        rigidbody.useGravity = true;

        return rigidbody;
    }

    public PhysicsMaterial UpdatePhysicsMaterial(PhysicsMaterial mat)
    {
        if (mat == null) return null;

        physicsMaterial = mat;

        // Friction
        physicsMaterial.staticFriction = physicsObjectProperties.stickiness;
        physicsMaterial.dynamicFriction = physicsObjectProperties.stickiness;
        physicsMaterial.frictionCombine = PhysicsMaterialCombine.Average;

        // Bounciness
        physicsMaterial.bounciness = physicsObjectProperties.elasticity;
        physicsMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;

        return physicsMaterial;
    }
    public void UpdateVisuals()
    {
        // Update VFX based on the physicsobjectmaterial, if appropriate.
        // This is a simplistic approach that will likely need to be 
        // updated for complex objects.
        if (physicsObjectProperties.physicsobjectmaterial == null)
            return;

        Material mat = physicsObjectProperties.physicsobjectmaterial.vfx_material;
        Renderer[] renderers = GetComponents<Renderer>();
        if (renderers == null || renderers.Length == 0) 
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material = mat;
        }
    }
    #endregion

    #region Collisions
    // additional things to happen on collision.
    // [placeholder]

    #endregion

    #region Halo Collisions
    // Physics objects are given halo colliders which are just triggers
    // to allow sticking to objects.
    public void OnHaloEnter(Collider other)
    {
        Debug.Log("Halo entered");
    }
    public void OnHaloExit(Collider other)
    {
        Debug.Log("Halo exited");
    }
    public void OnHaloStay(Collider other)
    {
        Debug.Log("Halo stayed");

        // If sticky, apply a drag force while halo
        // collider is being triggered.
        // Note that current implementation means it gets pinged
        // away at high velocity if stickiness is too high.
        // At about 10, it actually becomes sticky.
        // Might need a maxmin.
        //rigidbody.AddForce(-rigidbody.linearVelocity * physicsObjectProperties.stickiness);
        rigidbody.linearVelocity = rigidbody.linearVelocity * Mathf.Clamp01(1 - physicsObjectProperties.stickiness);
        
    }
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
