using Fusion;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static Fusion.NetworkRunner;

/// <summary>
/// A core node, that instantiates a gameObject as a base. The GameObject will need have relevent components like rb, colliders and mesh renderer
/// </summary>


[CreateAssetMenu(fileName = "ObjectCore", menuName = "SpellNodes/CoreNodes/ObjectCore")]
public class ObjectCore : CoreNode, IHasPrefabRefToBuffer
{
    // Should this be a networkprefabref
    // Should there be a generic objectcore networkprefabref,
    // then we replace its components at run time? 
    // or a unique networkprefabref per thing that can be spawned?

    //This can be an rb core or a transform core
    public NetworkPrefabRef corePrefabRef;
    public NetworkPrefabRef prefabRefToBuffer { get { return corePrefabRef; } }

    [Promotable("Lifetime", DataTypeTag.Lifetime)]
    public float lifetime = 0;

    private bool base_values_from_dependencies_stored = false;

    public SpellPosition CastSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation CastSpawnRotation = SpellRotation.CasterRotation;
    public SpellPosition TriggerSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation TriggerSpawnRotation = SpellRotation.CasterRotation;


    public override void CreateSpellCore(SpellTriggerInfo triggerInfo)
    {
        if (!CanSpawn(triggerInfo.State))
            return;
        triggerInfo.State.SpawnedCoresCounter++;

        Vector3 pos = SpellSystemHelpers.GetSpellPosition(
            triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);
        Quaternion rot = SpellSystemHelpers.GetSpellRotation(
            triggerInfo.IsCast ? CastSpawnRotation : TriggerSpawnRotation, triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);

        // Prior implementation
        // Create a lambda expression to be ran onspawn, then replicated across instances.
        //NetworkRunner.OnBeforeSpawned beforespawned = (Runner, NObject) => InitialisePhysicsObjectOnSpawn(NObject, triggerInfo);

        //NetworkObject spellCore = BasicSpawner.Spawn(corePrefabRef, pos, rot, beforespawned);

        // Now we use the NetworkObjectBuffer to grab pre-spawned objects where possible.
        // falls back to just spawn as before when not possible.
        NetworkObjectBuffer activeBuffer = null;
        NetworkObject spellCore = null;


        if (triggerInfo.IsValid && triggerInfo.State != null && triggerInfo.State.CastItem != null)
        {
            Debug.Log("Trying to buffer spawn from Item");
            activeBuffer = triggerInfo.State.CastItem.GetComponent<NetworkObjectBuffer>();
            if (activeBuffer != null)
            {
                spellCore = activeBuffer.Get(corePrefabRef, pos, rot);
            }
        }
        else if (triggerInfo.IsValid && triggerInfo.State != null && triggerInfo.State.Controller != null) 
        {
            Debug.Log("Trying to buffer spawn from Controller (NPC)");
            activeBuffer = triggerInfo.State.Controller.GetComponent<NetworkObjectBuffer>();
            if (activeBuffer != null)
            {
                spellCore = activeBuffer.Get(corePrefabRef, pos, rot);
            }
        }
        else if (triggerInfo.IsValid && triggerInfo.Source != null && triggerInfo.Source.TryGetComponent<SpellCreatedCore>(out var parentCore))
        {
            Debug.Log("Trying to buffer spawn from Parent Core's Networked Context");

            // Read the perfectly networked ID we saved in the Context!
            if (parentCore.Context.BufferSourceID.IsValid)
            {
                if (parentCore.Runner.TryFindObject(parentCore.Context.BufferSourceID, out NetworkObject bufferObj))
                {
                    activeBuffer = bufferObj.GetComponent<NetworkObjectBuffer>();
                    if (activeBuffer != null)
                    {
                        spellCore = activeBuffer.Get(corePrefabRef, pos, rot);
                    }
                }
            }
        }

        if (spellCore == null)
        {
            bool isProxy = triggerInfo.IsValid && triggerInfo.Source != null &&
                           triggerInfo.Source.TryGetComponent<NetworkObject>(out var sourceNetObj) &&
                           !sourceNetObj.HasStateAuthority && !sourceNetObj.HasInputAuthority;

            if (isProxy)
            {
                Debug.LogWarning("Proxy buffer exhausted! Safely skipping visual prediction to prevent crash.");
                return;
            }

            Debug.LogError("Couldn't buffer spawn so falling back.");
            spellCore = BasicSpawner.Spawn(corePrefabRef, pos, rot);
        }


        if (spellCore != null)
        {
            var lifecycleManager = spellCore.GetComponent<SpellCreatedCore>();
            if (lifecycleManager != null)
            {
                CoreContext context = new CoreContext()
                {
                    SpawnPosition = pos,
                    CastChargeLevel = triggerInfo.State.CastChargeLevel,
                    TriggerVector = triggerInfo.TriggerVector, // AddMomentum needs this!
                    AliveTime = 0f,
                    BufferSourceID = activeBuffer != null ? activeBuffer.Object.Id : default
                };
                if (triggerInfo.Source != null && triggerInfo.Source.TryGetComponent<NetworkObject>(out var netObj))
                {
                    context.OriginalCaster = netObj.Id;
                }

                CoreExecutionPlan plan = new CoreExecutionPlan();
                lifecycleManager.Initialize(triggerInfo.State.ActiveCastID, triggerInfo.State.SpellGraphIdFrom, this.InstanceGuid, this.CompiledPlan, context);
            }

            // We leave this here for now so your current game doesn't break. 
            // We will phase this out in Phase 4 when we convert to stateless math!
            //this.AttatchBehavioursAndTriggers(spellCore.gameObject, triggerInfo);
        }

        InitialisePhysicsObjectOnSpawn(spellCore, triggerInfo);
    }

    public void InitialisePhysicsObjectOnSpawn(NetworkObject spellCore, SpellTriggerInfo triggerInfo)
    {
        // This is called by the spawner before replicating the networkobject
        // across all instances.

        SpellCreatedPhysicsObject physicsObject = spellCore.GetComponent<SpellCreatedPhysicsObject>();

        // Now going with the assumption that any object you create must be a SpellCreatedPhysicsObject
        if(physicsObject == null)
        {
            throw new System.Exception("Summoned object hsa no SpellCreatedPhysicsObject script.");
        }

        if (!base_values_from_dependencies_stored)
        {
            // Only need to do this once.
            AppendBaseValuesFromDependency(physicsObject);
            AppendBaseValuesFromDependency(physicsObject.physicsObjectProperties);
            base_values_from_dependencies_stored = true;
        }

        // We now initialise from within the objectcore spawn method, rather than here.
        // This allows clients to catchup and do all this themselves too, so long
        // as they are provided this core-node (and equivalent spellgraph)
        
        // for multi-stage casts, wait until host tells us. Otherwise there
        // can be divergences.
        // For the first stage, we're usually fine to do it ourselves
        // since we have an actual triggerinfo/spellstate.
        
        //Debug.Log($"my instance id is {InstanceGuid}");

        physicsObject.InitialiseOnSpawned(this, triggerInfo, triggerInfo.State);


        //if (physicsObject != null)
        //{
        //    // We allow modification of values also within the created physicsobject
        //    // as well as its properties.
        //    // We capture the base values here, since this is the first time
        //    // we're seeing it. Then we can apply promotable values.

        //    physicsObject = ApplyPromotableValuesGeneric<SpellCreatedPhysicsObject>(physicsObject);
        //    physicsObject.physicsObjectProperties = ApplyPromotableValuesGeneric<PhysicsObjectProperties>(physicsObject.physicsObjectProperties);
        //    physicsObject.AssignProperties(this);
        //    physicsObject.InitialisePhysicsObject();
        //}


        ///*Debug.Log($"is cast = {triggerInfo.IsCast} [Spawn] posType={CastSpawnPosition} rotType={CastSpawnRotation} " +
        //  $"CastPos={triggerInfo.State.CastPosition} Override?={triggerInfo.HasOverridePosition} " +
        //  $"TrigPos={triggerInfo.TriggerPoint} and spell core is {spellCore.transform.position}");*/

        //AttatchBehavioursAndTriggers(spellCore.gameObject, triggerInfo);

        //if(physicsObject != null)
        //{
        //    // To catch initial momenta, etc.
        //    physicsObject.InitialiseAfterBehavioursAndTriggers(this, triggerInfo.State);
        //}
    }

    public override List<SocketDefinition> GetSockets()
    {
        List<SocketDefinition> sockets = base.GetSockets();

        // Append on sockets for additional promotable values from extra scripts.
        var modifiableFields = typeof(SpellCreatedPhysicsObject).GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in modifiableFields)
        {
            var promotableAttr = field.GetCustomAttribute<PromotableAttribute>(); if (promotableAttr != null)
            {
                sockets.Add(new SocketDefinition(
                    name: promotableAttr.DisplayName,
                    type: SocketType.Data,
                    direction: SocketDirection.Input,
                    tag: promotableAttr.Tag,
                    dataType: field.FieldType,
                    owningNodeGUID: this.InstanceGuid,
                    targetFieldName: field.Name
                ));
            }
        }
        modifiableFields = typeof(PhysicsObjectProperties).GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in modifiableFields)
        {
            var promotableAttr = field.GetCustomAttribute<PromotableAttribute>(); if (promotableAttr != null)
            {
                sockets.Add(new SocketDefinition(
                    name: promotableAttr.DisplayName,
                    type: SocketType.Data,
                    direction: SocketDirection.Input,
                    tag: promotableAttr.Tag,
                    dataType: field.FieldType,
                    owningNodeGUID: this.InstanceGuid,
                    targetFieldName: field.Name
                ));
            }
        }

        return sockets;
    }
}
