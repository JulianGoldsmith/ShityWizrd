using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.Events;
using static Fusion.NetworkBehaviour;

[DefaultExecutionOrder(+50)]
public class PhysicsObject : NetworkBehaviour, ISpawned
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

    [Header("Physics Settings")]
    public float defaultGravityScale = 1f;

    private IMovementHandler _movementHandler;

    private ChangeDetector _changeDetector;

    [Networked, OnChangedRender(nameof(OnPhysicsObjectPropertiesChanged))]
    public PhysicsObjectProperties physicsObjectProperties { get; set; }
    public CurrentPhysicsProperties currentProperties;
    public Rigidbody rb;
    public SimulatedPhysicsObject tPO;
    public PhysicsMaterial physicsMaterial;
    [SerializeField] protected List<PhysicsSubObject> physicsSubObjects = new List<PhysicsSubObject>();
    protected Tick? tick_spawned = null;
    // bonkedness is the standin for consciousness (player)
    // and health (object).
    [Networked] public float current_bonkedness { get; set; }
    protected bool zero_bonkedness { get { return current_bonkedness <= 0.0f; } }

    public NetworkObject creator;
    public NetworkObject lastInteractor;
    public NetworkObject currentThreatCause => (lastInteractor ?? creator)?? null;

    [Networked]
    public SpellEffectStates SpellEffectState { get; set; }

    #region Data Networking
    public void OnPhysicsObjectPropertiesChanged()
    {
        // Just re-init the object.
        // This includes assigning properties as well
        InitialisePhysicsObject();
    }
    public override void Spawned()
    {
        // When it spawns, ensure the properties are mapped.
        base.Spawned();

        InitilizeCoreInterfaces();

        tick_spawned = Runner.Tick;
        InitialisePhysicsObject();
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        current_bonkedness = starting_bonkedness;
    }
    #endregion


    #region Initialisation

    public void InitilizeCoreInterfaces()
    {
        _movementHandler = GetComponent<IMovementHandler>();
    }

    private void Awake()
    {
        
    }

    // Assign parameter values to Rigidbody and Unity PhysicsMaterials
    public void InitialisePhysicsObject()
    {
        UpdateRigidbody(GetComponent<Rigidbody>());
        _movementHandler = GetComponent<IMovementHandler>();
        Collider col = GetComponent<Collider>();
        if (col != null)
            UpdatePhysicsMaterial(col.material);
        UpdateVisuals();
        ModifyTransform();
        UpdateDerivedPhysics();
    }
    public Rigidbody UpdateRigidbody(Rigidbody _rb)
    {
        if (_rb == null) return null;

        rb = _rb;

        rb.mass = physicsObjectProperties.mass;
        rb.linearDamping = physicsObjectProperties.hardness;
        rb.angularDamping = physicsObjectProperties.mass * 0.05f;

        return rb;
    }

    public PhysicsMaterial UpdatePhysicsMaterial(PhysicsMaterial mat)
    {
        if (mat == null) return null;

        physicsMaterial = mat;

        // Friction
        //physicsMaterial.staticFriction = physicsObjectProperties.stickiness;
        //physicsMaterial.dynamicFriction = physicsObjectProperties.stickiness;
        //physicsMaterial.frictionCombine = PhysicsMaterialCombine.Average;

        // Bounciness
        //physicsMaterial.bounciness = physicsObjectProperties.elasticity;
        //physicsMaterial.bounceCombine = PhysicsMaterialCombine.Minimum;

        return physicsMaterial;
    }

    public void UpdateDerivedPhysics()
    {
        

        // 1. SIZE (Volume)
        float scaleModifier = 1f + (SpellEffectState.Scale / 125f);
        currentProperties.size = Mathf.Max(0.1f, physicsObjectProperties.size * scaleModifier);
        transform.localScale = Vector3.one * currentProperties.size;

        // 2. DENSITY & MASS
        float densityModifier = 1f
            + (SpellEffectState.Stone / 255f * 5f)
            - (SpellEffectState.Feather / 255f * 0.9f)
            + (SpellEffectState.Damp / 255f * 0.2f);

        currentProperties.density = physicsObjectProperties.density * densityModifier;

        // Mass = Density * Volume (Size^3 roughly)
        currentProperties.mass = Mathf.Max(0.01f, currentProperties.density * Mathf.Pow(currentProperties.size, 1.5f));
        

        // 3. ELASTICITY
        float coldPenalty = SpellEffectState.Temperature < 0 ? (Mathf.Abs(SpellEffectState.Temperature) / 125f * 0.5f) : 0f;
        currentProperties.elasticity = physicsObjectProperties.elasticity
            + (SpellEffectState.Elastic / 255f * 1.5f)
            - (SpellEffectState.Stone / 255f * 0.8f)
            - coldPenalty;
       

        // --- 4a. STICKINESS (Your Custom Momentum Engine) ---
        currentProperties.stickiness = physicsObjectProperties.stickiness
            + (SpellEffectState.Adhesion / 255f * 3f)   // Adhesion makes it glue to things
            - (SpellEffectState.Phase / 255f * 0.8f);   // Phase makes it ghost through things

        // Clamp between 0 (no stick) and 1 (perfect momentum share)
        currentProperties.stickiness = Mathf.Clamp01(currentProperties.stickiness);


        // --- 4b. FRICTION / DRAG (Unity Physics Engine) ---
        // Cold massively drops friction (Ice). Damp slightly increases it (Wet drag).
        float icePenalty = SpellEffectState.Temperature < 0 ? (Mathf.Abs(SpellEffectState.Temperature) / 125f * 0.8f) : 0f;

        currentProperties.friction = physicsObjectProperties.friction
            + (SpellEffectState.Damp / 255f * 0.2f)     // Wet surface drag
            - (SpellEffectState.Phase / 255f * 0.8f)    // Ethereal objects slide easily
            - icePenalty;                               // Ice removes sliding resistance

        // Apply strictly to the Unity Physics Material for sliding
       

        // 5. HARDNESS & BRITTLENESS
        float heatBonus = SpellEffectState.Temperature > 0 ? (SpellEffectState.Temperature / 125f * 0.5f) : 0f;

        currentProperties.hardness = physicsObjectProperties.hardness
            + (SpellEffectState.Stone / 255f * 2f)
            - heatBonus;

        currentProperties.brittleness = physicsObjectProperties.brittleness
            + coldPenalty
            + (SpellEffectState.Feather / 255f * 0.5f)
            - heatBonus
            - (SpellEffectState.Stone / 255f * 0.9f);

        // Ensure we don't hit zero or negatives
        currentProperties.hardness = Mathf.Max(0.01f, currentProperties.hardness);
        currentProperties.brittleness = Mathf.Max(0.01f, currentProperties.brittleness);

        currentProperties.gravityMultiplier = physicsObjectProperties.base_gravity_multiplier
                                          + (SpellEffectState.Gravity / 25f);

        if (rb == null || physicsMaterial == null) return;

        physicsMaterial.bounciness = Mathf.Clamp01(currentProperties.elasticity);
        physicsMaterial.staticFriction = Mathf.Max(0f, currentProperties.friction);
        physicsMaterial.dynamicFriction = Mathf.Max(0f, currentProperties.friction);
        rb.mass = currentProperties.mass;
        rb.useGravity = false;
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

    public void ModifyTransform()
    {
        // TODO:
        // This should be based on a base-size, not a multiplication.
        // So it wouldn't matter how often this is called.
        transform.localScale = Vector3.one * physicsObjectProperties.size;
    }
    #endregion


    #region Collisions
    // additional things to happen on collision.
    // [placeholder]

    public event System.Action<UniversalCollisionData> OnPhysicsImpact;

 

    public void OnCollisionEnter(Collision collision)
    {
        /* if (collision.impulse.magnitude > 0.01f)
         {
             var impactData = new UniversalCollisionData
             {
                 HitObject = collision.gameObject,
                 Point = collision.GetContact(0).point,
                 Normal = collision.GetContact(0).normal,
                 ImpulseMagnitude = collision.impulse.magnitude
             };

             // Pass it to the new universal system!
             HandleUniversalImpact(impactData);
         }*/
        if (!HasStateAuthority)
            return;

        OnBounce(collision);

        // if this hasn't properly spawned yet, don't use collisions.
        if (Object != null && Object.IsValid == false)
            return;

        PhysicsObject other = collision.gameObject.GetComponent<PhysicsObject>();

        if (other == null || other.Object == null || other.Object.IsValid == false)
            other = null;

        float bonk_amount = BonkAmount(collision.impulse.magnitude, other?.currentProperties);

        //Debug.Log($"OnCollisionEnter {collision.gameObject.name} {bonk_amount}");
        if (IfGetBonked(bonk_amount))
        {
            NetworkObject instigatorOfBonk = null;
            if (collision.gameObject.TryGetComponent<PhysicsObject>(out PhysicsObject otherPO))
            {
                instigatorOfBonk = otherPO.currentThreatCause;
            }
            OnBonk(bonk_amount, instigatorOfBonk, collision.contacts[0].point);
        }
    }
    #endregion

    //This needs
    public void BonkFromImpulse(float impulse, PhysicsObject otherPhysicsObject, NetworkObject bonk_instigator = null, Vector3? pos = null)
    {
        float bonk_amount = BonkAmount(impulse, otherPhysicsObject?.currentProperties);

        if (IfGetBonked(bonk_amount))
        {
            OnBonk(bonk_amount , bonk_instigator, pos);
        }
    }


    public Vector3 velocity_before_physics_update;
    public override void FixedUpdateNetwork()
    {
        ApplyForce(Physics.gravity * currentProperties.gravityMultiplier, ForceMode.Acceleration);
        foreach (var propertyname in _changeDetector.DetectChanges(this, out var previousBuffer, out var currentBuffer))
        {
            switch (propertyname)
            {
                case nameof(current_bonkedness):
                    {
                        OnBonkednessChanged(previousBuffer);
                        break;
                    }
                case nameof(SpellEffectState):
                    UpdateDerivedPhysics();
                    break;
            }
        }


        if (rb != null)
        {
            velocity_before_physics_update = rb.linearVelocity;
        }

        RunHaloCollisions();
    }



    #region Material Physics
    const int MAX_HALO_COLLISIONS = 8; // max number objects that can be in halo.
    Collider[] non_alloc_colliders = new Collider[MAX_HALO_COLLISIONS];
    public float halo_radius_scale_modifier = 1; // halo_size, applied to greatest localscale x/y/z.
    bool ShouldRunHaloCollisions()
    {
        // currently only run if sticky.
        // can add more.
        return currentProperties.stickiness > 0;
    }
    float halo_radius()
    {
        return 0.2f * halo_radius_scale_modifier * Mathf.Max(transform.localScale.x, transform.localScale.y, transform.localScale.z);
    }
    void RunHaloCollisions()
    {
        // This is used for stickiness, but could be
        // used for other physics processes.
        // can also layer mask if necessary.

        if (!ShouldRunHaloCollisions())
            return;
        // LayerMask mask
        int hit = Physics.OverlapSphereNonAlloc(
            transform.position, 
            halo_radius(), 
            non_alloc_colliders,
            SpellSystemHelpers.GeneralCollisionLayerMask());

        if (hit <= 0)
            return;

        for (int i = 0; i < hit; ++i)
        {
            OnStick(non_alloc_colliders[i]);
        }
    }
    bool PastInitialSpawnTick()
    {
        return (Runner.Tick - tick_spawned) > 1;
    }
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.blueViolet;
        //Gizmos.DrawWireSphere(transform.position, halo_radius());

        Gizmos.DrawLine(bounce_point, bounce_point + bounce_vector);

        if (rb != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + rb.linearVelocity);
        }
    }
    void OnStick(Collider other)
    {
        // If I'm not sticky, don't do anything.
        if (physicsObjectProperties.stickiness == 0)
            return;

        if (!PastInitialSpawnTick())
        {
            // skip the first tick of stickiness.
            // avoids sticking to player and cast-item.
            // there is defintely a better way to do this ofc.
            return;
        }

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

        // removing stickiness from non-physics objects for now.
        if (other_po == null)
        {
            PhysicsSubObject pso = other.GetComponent<PhysicsSubObject>();
            if (pso == null)
                return;
            other_rb = pso.rb;
            other_po = pso.parent_physics_object;
        }

        if(other_po != null)
        {
            if (other_po.rb != null)
            {
                other_rb = other_po.rb;
                other_velocity = other_rb.linearVelocity;
            }

            other_mass = other_po.currentProperties.mass;
            //if (other_po.physicsObjectProperties.physicsobjectmaterial != null)
            other_stickiness = other_po.currentProperties.stickiness;
        }

        // QUESTION: should we be including the other's stickiness? They'll run their
        // own calc anyway, to also apply their own stickiness?
        // Another option is we track, within the currently frame, who we've already stuck
        // to and then only let one of them run the calc.

        // Here we combine stickiness.
        float shared_stickiness_factor = Mathf.Clamp01(currentProperties.stickiness + other_stickiness);
        

        float total_mass = currentProperties.mass + other_mass;

        // Here's some standard momentum sharing physics:
        Vector3 my_momentum = rb.linearVelocity * currentProperties.mass;
        Vector3 other_momentum = other_velocity * other_mass;
        Vector3 total_momentum = my_momentum + other_momentum;


        // we then distribute out the momenta back to the two objects based on mass
        // and stickiness.
        Vector3 my_new_momentum = my_momentum * (1 - shared_stickiness_factor) + 
            total_momentum * shared_stickiness_factor * currentProperties.mass / total_mass;

        Vector3 velocity_diff = (my_new_momentum / currentProperties.mass) - rb.linearVelocity;
        ApplyForceToSelfAndSubObjects(velocity_diff, ForceMode.VelocityChange);

        if (other_po != null)
        {
            // Also deal with the other.
            Vector3 other_new_momentum = other_momentum * (1 - shared_stickiness_factor) +
                total_momentum * shared_stickiness_factor * other_mass / total_mass;
            //other_rb.linearVelocity = other_new_momentum / other_mass;
            Vector3 velocity_diff_other = (other_new_momentum / other_mass) - other_rb.linearVelocity;
            other_po.ApplyForceToSelfAndSubObjects(velocity_diff_other, ForceMode.VelocityChange);
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

    void OnBounce(Collision col)
    {
        if (!PastInitialSpawnTick())
        {
            // skip the first tick of bounce.
            // avoids bouncing on player and cast-item.
            // there is defintely a better way to do this ofc.
            return;
        }

        if (currentProperties.elasticity == 0)
            return;

        // unfortunately, can't just use the impulse

        // note that unity rigidody is already doing impulses to separate
        // the objects. Here we just apply an additional kick, to 
        // apply extra bounciness in the collision.

        // bounce on collision. Apply an impulse in the opposite direction
        // given by bounciness. Also apply to the other object.
        // If they are also elastic, they'll do their own calcs to, and 
        // therefore apply double-bounce.

        // collision.impulse isn't working so now just doing the calculations myself :(
        Vector3 normal = col.GetContact(0).normal.normalized; // the normal isn't always normalised ???
        normal = col.impulse.normalized;
        float velAlongNormal = Vector3.Dot(col.relativeVelocity, normal);
        bounce_vector = normal * velAlongNormal;
        bounce_point = col.GetContact(0).point;

        // skip if moving together.
        if (velAlongNormal < 0)
            return;

        float invMass = 1f / currentProperties.mass;
        float other_invMass;
        
        PhysicsObject other_po = col.gameObject.GetComponent<PhysicsObject>();
        if (other_po != null)
            other_invMass = 1f / other_po.currentProperties.mass;
        else
            other_invMass = 0;

        float j = currentProperties.elasticity * velAlongNormal / (invMass + other_invMass);

        //Vector3 bounce_impulse = col.impulse * physicsObjectProperties.elasticity * 5;
        Vector3 bounce_impulse = j * normal;


        // a bounce can't change the magnitude of velocity, it can only rotate it,
        //  since energy can be at-most conserved.
        ApplyForce(bounce_impulse, ForceMode.Impulse);

        if (other_po != null)
            other_po.ApplyForce(-bounce_impulse, ForceMode.Impulse);
    }
    Vector3 bounce_vector;
    Vector3 bounce_point;
    #endregion

    #region Bonking
    // [placeholder]
    // what happens when the object is 'bonked' (hit in a collision).
    // different implementations for players, enemies, objects, spells.


    
    // Everything has 100 starting.
    protected const float starting_bonkedness = 100f;
    private const float bonk_threshold = 2.5f;

    bool IfGetBonked(float bonk_amount)
    {
        // Consider what the threshold should be.
        // Since, as is, a heavier object gets a greater bonk when it hits the ground...
        // Should it just be velocity based?
        // But then 
        return bonk_amount > bonk_threshold;
    }
    float BonkAmount(float collision_impulse, CurrentPhysicsProperties? other_properties)
    {
        // Depends on size and brittleness.
        // - higher size means more effective health
        // - higher brittleness means lower effective health.
        float mass = currentProperties.mass > 0 ? currentProperties.mass : 1.0f;

        // bonk is proportional to the hardness of both objects.
        float hardness_factor = currentProperties.hardness > 0 ? currentProperties.hardness : PhysicsObjectMaterial.default_hardness;
        if(other_properties != null)
            hardness_factor *= (other_properties?.hardness > 0 ? other_properties?.hardness : PhysicsObjectMaterial.default_hardness) ?? PhysicsObjectMaterial.default_hardness;

        // quadratic.
        // this is a bad idea since falling on an object would come with a different bonk calc.
        float wall_or_floor_penalty = (other_properties == null)? 0.1f: 1.0f;

        return 150f * collision_impulse * hardness_factor * wall_or_floor_penalty *
            currentProperties.brittleness /
            mass;
    }

    public virtual void OnBonk(float bonk_amount, NetworkObject bonk_instigator = null, Vector3? pos = null)
    {
        current_bonkedness -= bonk_amount;
        //Debug.Log($"new bonkedness: {current_bonkedness} {bonk_amount}");

        //OnBonkednessChanged();
        // Do stuff when zero-bonkedness.
        // Different for object versus player.

        // instead now checking within fixedupdatenetwork 
        // if zero bonkedness.
    }

    // allow other scripts to subcribe to these zero events.
    public UnityEvent OnZeroBonk_event;
    protected virtual void OnZeroBonk()
    {
        Debug.Log("OnZeroBonk");
        OnZeroBonk_event.Invoke();
    }
    public UnityEvent OnRecoverFromBonk_event;
    protected virtual void OnRecoverFromBonk()
    {
        Debug.Log("OnRecoverFromBank");
        OnRecoverFromBonk_event.Invoke();
    }
    public virtual void OnBonkednessChanged(NetworkBehaviourBuffer previous)
    {
        var last_known_bonkedness = GetPropertyReader<float>(nameof(current_bonkedness)).Read(previous);
        //Log.Info($"counter changed: {current_bonkedness}, prev: {last_known_bonkedness}");
        if (zero_bonkedness && last_known_bonkedness > 0)
        {
            // got bonked
            OnZeroBonk();
        }
        else if(!zero_bonkedness && last_known_bonkedness < 0)
        {
            // recovered from bonk
            OnRecoverFromBonk();
        }
    }
    #endregion

    #region Sound
    // [placeholder]
    #endregion

    #region Despawning
    protected virtual void DespawnObject()
    {
        if(TryGetComponent<SpellCreatedCore>(out SpellCreatedCore CLM)){
            CLM.DeactivateCore();
        }
        if (HasStateAuthority)
            Runner.Despawn(Object);
        
    }
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);
    }

    #endregion

    #region PhysicsSubObjects
    public void SubscribeSubObject(PhysicsSubObject subObject)
    {
        if (physicsSubObjects == null)
            physicsSubObjects = new List<PhysicsSubObject>();
        physicsSubObjects.Add(subObject);
    }
    public void ApplyToSelfAndAllSubObjects(Action<GameObject> method)
    {
        method(gameObject);
        for (int i = 0; i < physicsSubObjects.Count; i++)
        {
            method(physicsSubObjects[i].gameObject);
        }
    }
    public void ApplyAcrossAllSubObjects(Action<PhysicsSubObject> method)
    {
        for (int i = 0; i < physicsSubObjects.Count; i++)
        {
            method(physicsSubObjects[i]);
        }
    }
    public void ApplyForceToSelfAndSubObjects(Vector3 force, ForceMode forceMode)
    {
        ApplyForce(force, forceMode);

        Action<PhysicsSubObject> action = obj => ApplyForceToSubObject(obj, force, forceMode);
        ApplyAcrossAllSubObjects(action);
    }
    public void ApplyForce(Vector3 force, ForceMode forceMode)
    {
        if (_movementHandler != null)
        {
            _movementHandler.ApplyForce(force, forceMode);
        }
        else if (rb != null)
        {
            rb.AddForce(force, forceMode);
        }
    }
    void ApplyForceToSubObject(PhysicsSubObject pso, Vector3 force, ForceMode forceMode)
    {
        if (pso.rb != null)
            pso.rb.AddForce(force, forceMode);
    }
    #endregion
}

public struct SpellEffectStates : INetworkStruct
{
    // twoway -125 - 125
    public sbyte Temperature;
    public sbyte Scale;
    public sbyte Gravity;

    // oneway
    public byte Stone;
    public byte Damp;
    public byte Charge;
    public byte Feather;
    public byte Elastic;
    public byte Phase;
    public byte Adhesion;
   
}

[System.Serializable]
public struct CurrentPhysicsProperties
{
    public float size;
    public float density;
    public float mass;
    public float hardness;
    public float elasticity;
    public float brittleness;
    public float stickiness;
    public float friction;
    public float gravityMultiplier;
}
