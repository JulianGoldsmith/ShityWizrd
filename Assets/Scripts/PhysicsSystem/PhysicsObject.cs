using System.Timers;
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
    public PhysicsMaterial physicsMaterial;

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
        if (mat == null) 
            return;
        Renderer[] renderers = GetComponents<Renderer>();
        if (renderers == null || renderers.Length == 0) 
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].material = mat;
            renderers[i].shadowCastingMode = physicsObjectProperties.physicsobjectmaterial.casts_shadows? 
                UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
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
        
    }
    public void OnHaloExit(Collider other)
    {

    }
    public void OnHaloStay(Collider other)
    {

        // If sticky, apply a drag force while halo
        // collider is being triggered.
        // Note that current implementation means it gets pinged
        // away at high velocity if stickiness is too high.
        // At about 10, it actually becomes sticky.
        // Might need a maxmin.
        //rigidbody.AddForce(-rigidbody.linearVelocity * physicsObjectProperties.stickiness);
        //rigidbody.linearVelocity = rigidbody.linearVelocity * Mathf.Clamp01(1 - physicsObjectProperties.stickiness);
        OnStick(other);
    }
    #endregion

    #region Material Physics
    void OnStick(Collider other)
    {
        // If I'm not sticky, don't do anything.
        if (physicsObjectProperties.stickiness == 0)
            return;

        // If this is sticky and the halo has triggered, then:
        // - Check if the other is a physicsobject
        // - If yes, get it's rigidbody
        // - Distribute shared momentum between us based on 
        //      mass and stickiness.
        // - If other is not physicsobject, then treat it
        //      as a stationary,infinite mass.

        // We adjust velocity for both ourselves and what we
        // collided with.
        // This may be wrong and might be funky over network
        // due to authority...

        // What about cases where both are sticky? If both
        // are triggering haloes then we'd get double-stuck, which is
        // fine, but ordering of application might lead to different
        // results?

        // In this current version, nothing sticks absolutely.
        // Since the prior timestep will cause the object to fall
        // one frame, then it will hit this trigger and zero-out its
        // velocity. But it has already moved in that time, stupidly.

        PhysicsObject other_po = other.GetComponent<PhysicsObject>();

        Rigidbody other_rb = null;
        float other_mass = float.MaxValue;
        Vector3 other_velocity = Vector3.zero;
        float other_stickiness = 0;

        if(other_po != null)
        {
            if (other_po.rigidbody != null)
            {
                other_rb = other_po.rigidbody;
                other_velocity = other_rb.linearVelocity;
            }
            if (other_po.physicsObjectProperties != null)
            {
                other_mass = other_po.physicsObjectProperties.mass;
                if (other_po.physicsObjectProperties.physicsobjectmaterial != null)
                    other_stickiness = other_po.physicsObjectProperties.physicsobjectmaterial.stickiness;
            }
        }

        // QUESTION: should we be including the other's stickiness? They'll run their
        // own calc anyway, to also apply their own stickiness?
        // Another option is we track, within the currently frame, who we've already stuck
        // to and then only let one of them run the calc.

        // Here we combine stickiness.
        float shared_stickiness_factor = Mathf.Clamp01(physicsObjectProperties.stickiness + other_stickiness);
        

        float total_mass = physicsObjectProperties.mass + other_mass;

        // Here's some standard momentum sharing physics:
        Vector3 my_momentum = rigidbody.linearVelocity * physicsObjectProperties.mass;
        Vector3 other_momentum = other_velocity * other_mass;
        Vector3 total_momentum = my_momentum + other_momentum;


        // we then distribute out the momenta back to the two objects based on mass
        // and stickiness.
        Vector3 my_new_momentum = my_momentum * (1 - shared_stickiness_factor) + 
            total_momentum * shared_stickiness_factor * physicsObjectProperties.mass / total_mass;

        rigidbody.linearVelocity = my_new_momentum / physicsObjectProperties.mass;

        if (other_po != null)
        {
            // Also deal with the other.
            Vector3 other_new_momentum = other_momentum * (1 - shared_stickiness_factor) +
                total_momentum * shared_stickiness_factor * other_mass / total_mass;
            other_rb.linearVelocity = other_new_momentum / other_mass;
        }

        // Sense checking:
        // stickiness = 0 (no stick).
        // my_new_momentum = my_momentum
        // other_new_momentum = other_momentum

        // stickiness = 1 (full stick)
        // my_new_momentum = total_momentum * my_mass / total_mass
        // other_new_momentum = total_momentum * other_mass / total_mass
        // my_new_velocity = total_momentum / total_mass
        // other_new_velocity = total_momentum / total_mass
        // -> this means both objects leave the collision
        // with the same velocity, i.e. complete sticking.

        // stationary wall. other_momentum = 0, other_velocity = 0, other_mass >> my_mass
        // so therefore total_mass ~ other_mass
        // and total_momentum = my_momentum
        // my_new_momentum = my_momentum * my_mass / other_mass
        // (other_new_momentum = my_momentum * other_mass / other_mass)
        // my_new_velocity = my_velocity * my_mass / other_mass -> 0 as other_mass -> Infinity
        // so complete sticking to wall.

        // Stationary wall, stickiness = 0.5
        // other_momentum = 0, other_velocity = 0, other_mass >> my_mass
        // so therefore total_mass ~ other_mass
        // my_new_momentum = 0.5 * my_momentum * (1 + my_mass / other_mass)
        // (other_new_momentum = 0.5 * my_momentum * other_mass / other_mass)
        // my_new_velocity = 0.5 * my_velocity * (

        // All looks good.


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
