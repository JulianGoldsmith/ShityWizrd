using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.Events;
using static Fusion.NetworkBehaviour;

[DefaultExecutionOrder(+50)]
[RequireComponent(typeof(PhysicsObjectProperties))]
public class PhysicsObject : NetworkBehaviour, ISpawned, IBufferableComponent
{


    private IMovementHandler _movementHandler;
    private BufferedObject _bufferedObject;

    [Header("Dependancies")]
    public PhysicsObjectProperties physicsObjectProperties;
    public Rigidbody rb;
    public PhysicsMaterial physicsMaterial;
    [System.NonSerialized] public XPBDPosAndRotSolver ragdollController;

    [Header("Composure / Bonk System")]
    public BonkManager bonkManager;

    [SerializeField] protected List<PhysicsSubObject> physicsSubObjects = new List<PhysicsSubObject>();
    protected Tick? tick_spawned = null;

    public NetworkObject creator;
    public NetworkObject lastInteractor;
    public NetworkObject currentThreatCause => (lastInteractor ?? creator)?? null;

    [Header("Renderer Settings")]
    private List<Renderer> _renderers = new List<Renderer>();
    public List<Renderer> nonChildRenderers = new List<Renderer>();
    private MaterialPropertyBlock _mpb;
    public VisualStateData VisualState = new VisualStateData();
    private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

    [Header("Inate Physics Settings")]
    public float defaultGravityScale = 1f;

    #region Data Networking
    private void Awake()
    {
        physicsObjectProperties = GetComponent<PhysicsObjectProperties>();
        InitilizeCoreInterfaces();
    }

    public override void Spawned()
    {
        // When it spawns, ensure the properties are mapped.
        base.Spawned();

        tick_spawned = Runner.Tick;
        if (_bufferedObject == null && TryGetComponent(out BufferedObject bufferedObject)) BindBufferedObject(bufferedObject);
        //InitialisePhysicsObject();
    }
    #endregion


    public override void Render()
    {
        if (_bufferedObject != null && !_bufferedObject.CanRunSimulationCode) return;
        if (physicsObjectProperties == null || _renderers == null || physicsObjectProperties.physicsobjectmaterial == null)
            return;

        physicsObjectProperties.physicsobjectmaterial.UpdateVisuals(
            this,
            VisualState,
            _mpb,
            _renderers,
            Time.deltaTime
        );
    }

    #region Initialisation

    public void InitilizeCoreInterfaces()
    {
        _movementHandler = GetComponent<IMovementHandler>();


        _renderers = GetComponentsInChildren<Renderer>().ToList();
        _renderers.AddRange(nonChildRenderers);
        foreach (var r in _renderers)
        {
            if (r != null)
            {
                originalMaterials.Add(r, r.sharedMaterials);
            }
        }

      
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
    
    }


    public void InitialisePhysicsObject()
    {
        rb = GetComponent<Rigidbody>();

        Collider col = GetComponentInChildren<Collider>();
        if (col != null)
        {
            physicsMaterial = col.material;
        }

        _movementHandler = GetComponent<IMovementHandler>();

        UpdateVisuals();

        SimMaterialStateAndEffects();
    }






    public void UpdateVisuals()
    {
        // Update VFX based on the physicsobjectmaterial, if appropriate.
        // This is a simplistic approach that will likely need to be 
        // updated for complex objects.
        if (physicsObjectProperties.physicsobjectmaterial == null)
            return;

        Material mat = physicsObjectProperties.physicsobjectmaterial.visual_material;
        if (mat == null) 
            return;

        if (_renderers == null || _renderers.Count == 0)
            return;

        for (int i = 0; i < _renderers.Count; i++)
        {
            Renderer r = _renderers[i];
            if (r == null) continue;

            if (physicsObjectProperties.Material_label == physicsObjectProperties.innateMaterial ||
                physicsObjectProperties.Material_label == 0)
            {
                if (originalMaterials.TryGetValue(r, out var tmpMats))
                {
                    r.sharedMaterials = tmpMats;
                }
            }
            else if (mat != null)
            {
                Material[] overrideMats = new Material[r.sharedMaterials.Length];
                for (int j = 0; j < overrideMats.Length; j++)
                {
                    overrideMats[j] = mat;
                }
                r.sharedMaterials = overrideMats;
            }

            r.shadowCastingMode = physicsObjectProperties.physicsobjectmaterial.casts_shadows ?
                UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    public void RegisterAttachedVisuals(AttatchedSpellComponent attachedData, PhysicsObjectMaterial activeMaterial)
    {
        Renderer[] autoRenderers = attachedData.GetAllRenderers();

        if (autoRenderers == null || autoRenderers.Length == 0) return;

        List<Renderer> updatedRenderers = new List<Renderer>(_renderers != null ? _renderers : new Renderer[0]);

        foreach (var r in autoRenderers)
        {
            if (r == null) continue;

            if (!originalMaterials.ContainsKey(r))
            {
                originalMaterials.Add(r, r.sharedMaterials);
            }

            if (attachedData.allowBaseMaterialOverride && activeMaterial != null && activeMaterial.visual_material != null)
            {
                Material[] overrideMats = new Material[r.sharedMaterials.Length];
                for (int j = 0; j < overrideMats.Length; j++)
                {
                    overrideMats[j] = activeMaterial.visual_material;
                }
                r.sharedMaterials = overrideMats;

                r.shadowCastingMode = activeMaterial.casts_shadows ?
                    UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            if (!updatedRenderers.Contains(r))
            {
                updatedRenderers.Add(r);
            }
        }

        _renderers = updatedRenderers.ToList();
    }

    public void UnregisterAttachedVisuals(AttatchedSpellComponent attachedData)
    {
        if (attachedData == null) return;
        Renderer[] attachedRenderers = attachedData.GetAllRenderers();
        if (attachedRenderers == null) return;

        foreach (Renderer renderer in attachedRenderers)
        {
            if (renderer == null) continue;
            _renderers.Remove(renderer);
            originalMaterials.Remove(renderer);
        }
    }

    #endregion


    #region Collisions


    public event System.Action<UniversalCollisionData> OnPhysicsImpact;



    public void OnCollisionEnter(Collision collision)
    {
        if (_bufferedObject != null && !_bufferedObject.CanRunSimulationCode) return;
        if (Runner == null || Object == null || !Object.IsValid)
            return;
        //OnBounce(collision);

        if (Object != null && Object.IsValid == false) return;

        PhysicsObject otherPO = collision.gameObject.GetComponent<PhysicsObject>();

        if (bonkManager != null)
        {
            NetworkObject instigator = otherPO != null ? otherPO.currentThreatCause : null;
            bonkManager.ReportCollision(this, collision, instigator);
        }
    }

    // For spell explosions or forces that don't trigger a physical Unity Collision
    public void ReportRawImpulse(float impulse, PhysicsObject otherPhysicsObject = null, NetworkObject instigator = null, Vector3? pos = null)
    {
        if (_bufferedObject != null && !_bufferedObject.CanRunSimulationCode) return;
        if (bonkManager != null)
        {
            bonkManager.ReportImpulse(
                hitBone: this,
                impulseMagnitude: impulse,
                otherProperties: otherPhysicsObject != null ? otherPhysicsObject.physicsObjectProperties : null,
                instigator: instigator,
                contactPoint: pos ?? transform.position
            );
        }
    }

    public void OnCollisionStay(Collision collision)
    {
        if (_bufferedObject != null && !_bufferedObject.CanRunSimulationCode) return;
        if (Runner == null || Object == null || !Object.IsValid)
            return;

        PhysicsObject otherPO =
            collision.gameObject.GetComponent<PhysicsObject>();

        if (bonkManager != null)
        {
            NetworkObject instigator =
                otherPO != null
                    ? otherPO.currentThreatCause
                    : null;

            bonkManager.ReportCollisionStay(
                this,
                collision,
                instigator
            );
        }
    }
    #endregion

    //This needs


    public Vector3 velocity_before_physics_update;
    public override void FixedUpdateNetwork()
    {
        if (_bufferedObject != null && !_bufferedObject.CanRunSimulationCode) return;
        /*if (TryGetComponent<StatusEffectManager>(out var effectManager))
        {
            effectManager.BeginTick();
        }*/

        SimMaterialStateAndEffects();

        ApplyForce(Physics.gravity * physicsObjectProperties.CurrentSimData.GravityMultiplier, ForceMode.Acceleration);
        

        if (rb != null)
        {
            velocity_before_physics_update = rb.linearVelocity;
        }

        //RunHaloCollisions();
    }

    public void SimMaterialStateAndEffects()
    {
        StatusEffectManager effectManager = null;
        NetworkedMemoryAllocator memory = null;

        if (TryGetComponent<StatusEffectManager>(out effectManager))
        {
            effectManager.CleanUpExpiredEffects(Runner.Tick);
            memory = GetComponent<NetworkedMemoryAllocator>();
        }

        // 1. RUN THE SIMULATION ENGINE
        physicsObjectProperties.CalculateSimState(Runner, this, memory, effectManager);



        if (TryGetComponent<StatusEffectManager>(out effectManager))
        {
            effectManager.ClearPersistanceCache();
        }
        

        // 2. APPLY TO UNITY PHYSICS
        SimProperties finalSim = physicsObjectProperties.CurrentSimData;
        Vector3 targetScale = physicsObjectProperties.InitialEditorScale * finalSim.Scale;
        bool scaleChanged = (transform.localScale - targetScale).sqrMagnitude > 0.000001f;

        if (scaleChanged) transform.localScale = targetScale;

        if (rb != null)
        {
            bool massChanged = !Mathf.Approximately(rb.mass, finalSim.Mass);

            rb.mass = finalSim.Mass;
            rb.linearDamping = finalSim.LinearDamping;
            rb.angularDamping = finalSim.AngularDamping;
            rb.useGravity = false; // We apply custom gravity in FUN

            if (scaleChanged) rb.ResetCenterOfMass();
            if (scaleChanged || massChanged) rb.ResetInertiaTensor();
        }

        if (physicsMaterial != null)
        {
            physicsMaterial.dynamicFriction = finalSim.Friction;
            physicsMaterial.staticFriction = finalSim.Friction;
            physicsMaterial.bounciness = finalSim.Bounce;
        }
    }


    #region Material Physics
    const int MAX_HALO_COLLISIONS = 8; // max number objects that can be in halo.
    Collider[] non_alloc_colliders = new Collider[MAX_HALO_COLLISIONS];
    public float halo_radius_scale_modifier = 1; // halo_size, applied to greatest localscale x/y/z.
    bool ShouldRunHaloCollisions()
    {
        // currently only run if sticky.
        // can add more.
        return physicsObjectProperties.stickiness > 0;
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
        if (Runner == null || tick_spawned == null)
            return false;

        return (Runner.Tick - tick_spawned.Value) > 1;
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

            other_mass = other_po.physicsObjectProperties.mass;
            //if (other_po.physicsObjectProperties.physicsobjectmaterial != null)
            other_stickiness = other_po.physicsObjectProperties.stickiness;
        }

        // QUESTION: should we be including the other's stickiness? They'll run their
        // own calc anyway, to also apply their own stickiness?
        // Another option is we track, within the currently frame, who we've already stuck
        // to and then only let one of them run the calc.

        // Here we combine stickiness.
        float shared_stickiness_factor = Mathf.Clamp01(physicsObjectProperties.stickiness + other_stickiness);
        

        float total_mass = physicsObjectProperties.mass + other_mass;

        // Here's some standard momentum sharing physics:
        Vector3 my_momentum = rb.linearVelocity * physicsObjectProperties.mass;
        Vector3 other_momentum = other_velocity * other_mass;
        Vector3 total_momentum = my_momentum + other_momentum;


        // we then distribute out the momenta back to the two objects based on mass
        // and stickiness.
        Vector3 my_new_momentum = my_momentum * (1 - shared_stickiness_factor) + 
            total_momentum * shared_stickiness_factor * physicsObjectProperties.mass / total_mass;

        Vector3 velocity_diff = (my_new_momentum / physicsObjectProperties.mass) - rb.linearVelocity;
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

        if (physicsObjectProperties.elasticity == 0)
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

        float invMass = 1f / physicsObjectProperties.mass;
        float other_invMass;
        
        PhysicsObject other_po = col.gameObject.GetComponent<PhysicsObject>();
        if (other_po != null)
            other_invMass = 1f / other_po.physicsObjectProperties.mass;
        else
            other_invMass = 0;

        float j = physicsObjectProperties.elasticity * velAlongNormal / (invMass + other_invMass);

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
    /////////////////////////////////////////Bonking is now managed in the Bonk Manager script, any object that can be bonked needs one of these (probably will make it an Interface eventually ///////////////////

    #endregion

    #region Sound
    // [placeholder]
    #endregion

    #region Despawning
    protected virtual void DespawnObject()
    {
        if (TryGetComponent<SpellCreatedCore>(out SpellCreatedCore CLM))
        {
           
            CLM.DeactivateCore();
        }
        else
        {
            if (HasStateAuthority)
                Runner.Despawn(Object);
        }

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
        if (_bufferedObject != null && !_bufferedObject.CanRunSimulationCode) return;
        if (_movementHandler != null)
        {
            _movementHandler.ApplyForce(force, forceMode);
        }
        else if (rb != null)
        {
            rb.AddForce(force, forceMode);
        }
    }

    public void BindBufferedObject(BufferedObject bufferedObject)
    {
        _bufferedObject = bufferedObject;
    }

    public void OnBufferedWake(int wakeTick, bool isActivationTick)
    {
    }

    public void OnBufferedSleep(int sleepTick)
    {
        velocity_before_physics_update = Vector3.zero;
    }
    void ApplyForceToSubObject(PhysicsSubObject pso, Vector3 force, ForceMode forceMode)
    {
        if (pso.rb != null)
            pso.rb.AddForce(force, forceMode);
    }
    #endregion
}

