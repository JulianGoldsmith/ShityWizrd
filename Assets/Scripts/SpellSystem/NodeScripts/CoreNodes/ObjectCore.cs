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

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        // 1. The Factory spits out the stateless C# block
        return new RuntimeObjectCore()
        {
            ArrayIndex = context.CurrentNodeIndex,
            Template = this, // We pass the template purely for legacy Physics Object initialization
            PrefabRef = this.corePrefabRef,
            CastSpawnPosition = this.CastSpawnPosition,
            CastSpawnRotation = this.CastSpawnRotation,
            TriggerSpawnPosition = this.TriggerSpawnPosition,
            TriggerSpawnRotation = this.TriggerSpawnRotation,
            OriginalTemplateGuid = this.InstanceGuid
        };
    }
}

public class RuntimeObjectCore : RuntimeCoreBase
{
    public int ArrayIndex;
    public ObjectCore Template; // Legacy reference for Promotables
    public NetworkPrefabRef PrefabRef;
    public SpellPosition CastSpawnPosition;
    public SpellRotation CastSpawnRotation;
    public SpellPosition TriggerSpawnPosition;
    public SpellRotation TriggerSpawnRotation;
    public string OriginalTemplateGuid;

    public override void ExecuteCore(SpellTriggerInfo triggerInfo)
    {
        Vector3 pos = SpellSystemHelpers.GetSpellPosition(triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);
        Quaternion rot = SpellSystemHelpers.GetSpellRotation(triggerInfo.IsCast ? CastSpawnRotation : TriggerSpawnRotation, triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);

        NetworkObjectBuffer activeBuffer = null;
        NetworkObject spellCore = null;

        // 1. Try to pull from Weapon Buffer
        if (triggerInfo.IsValid && triggerInfo.State != null && triggerInfo.State.CastItem != null)
        {
            activeBuffer = triggerInfo.State.CastItem.GetComponent<NetworkObjectBuffer>();
            if (activeBuffer != null) spellCore = activeBuffer.Get(PrefabRef, pos, rot);
        }
        // 2. Try to pull from NPC/Controller Buffer
        else if (triggerInfo.IsValid && triggerInfo.State != null && triggerInfo.State.Controller != null)
        {
            activeBuffer = triggerInfo.State.Controller.GetComponent<NetworkObjectBuffer>();
            if (activeBuffer != null) spellCore = activeBuffer.Get(PrefabRef, pos, rot);
        }
        // 3. Try to pull from the Parent Core's Buffer (For downstream spawned projectiles)
        else if (triggerInfo.IsValid && triggerInfo.Source != null && triggerInfo.Source.TryGetComponent<SpellCreatedCore>(out var parentCore))
        {
            if (parentCore.Context.BufferSourceID.IsValid)
            {
                if (parentCore.Runner.TryFindObject(parentCore.Context.BufferSourceID, out NetworkObject bufferObj))
                {
                    activeBuffer = bufferObj.GetComponent<NetworkObjectBuffer>();
                    if (activeBuffer != null) spellCore = activeBuffer.Get(PrefabRef, pos, rot);
                }
            }
        }

        // 4. Ultimate Fallback
        if (spellCore == null)
        {
            spellCore = BasicSpawner.Spawn(PrefabRef, pos, rot);
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
                    TriggerVector = triggerInfo.TriggerVector,
                    AliveTime = 0f,
                    BufferSourceID = activeBuffer != null ? activeBuffer.Object.Id : default
                };

                // TEMPORARY BRIDGE: Cast our new Hydrator lists back into the old Execution Plan
                CoreExecutionPlan dummyPlan = new CoreExecutionPlan();
                dummyPlan.Behaviours = new List<IBehaviour>(this.Behaviours);
                dummyPlan.Triggers = new List<ITrigger>(this.Triggers);

                lifecycleManager.Initialize(triggerInfo.State.ActiveCastID, triggerInfo.State.SpellGraphIdFrom, OriginalTemplateGuid, dummyPlan, context, ArrayIndex);
            }

            // Initialize the Physics (Using the legacy template reference for now)
            Template.InitialisePhysicsObjectOnSpawn(spellCore, triggerInfo);
        }
    }
}