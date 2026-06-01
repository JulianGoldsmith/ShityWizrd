using Fusion;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static Fusion.NetworkRunner;

/// <summary>
/// A core node, that instantiates a gameObject as a base. The GameObject will need have relevent components like rb, colliders and mesh renderer
/// </summary>


[CreateAssetMenu(fileName = "NonPhysicsCore", menuName = "SpellNodes/CoreNodes/NonPhysicsCore")]
public class NonPhysicsCore : CoreNode, IHasPrefabRefToBuffer
{
    // Makes a network prefab that isnt a physics object, usefull for making static objects with triggers / behaviours
    // or parenting to another object where aura isnt appropriate
    // we will need to tag behaviours and triggers as physics or not 

    public NetworkPrefabRef corePrefabRef;
    public NetworkPrefabRef prefabRefToBuffer { get { return corePrefabRef; } }

    [Promotable("Lifetime", DataTypeTag.Lifetime)]
    public float lifetime = 0;

    private bool base_values_from_dependencies_stored = false;

    public SpellPosition CastSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation CastSpawnRotation = SpellRotation.CasterRotation;
    public SpellPosition TriggerSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation TriggerSpawnRotation = SpellRotation.CasterRotation;


   /* public override void CreateSpellCore(SpellTriggerInfo triggerInfo)
    {
        if (!CanSpawn(triggerInfo.State))
            return;

        Vector3 pos = SpellSystemHelpers.GetSpellPosition(triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);
        Quaternion rot = SpellSystemHelpers.GetSpellRotation(triggerInfo.IsCast ? CastSpawnRotation : TriggerSpawnRotation, triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);

        NetworkObject spellCore = null;
        if (triggerInfo.IsValid && triggerInfo.State != null && triggerInfo.State.CastItem != null)
        {
            Debug.Log("Trying to buffer spawn");
            NetworkObjectBuffer buffer = triggerInfo.State.CastItem.GetComponent<NetworkObjectBuffer>();
            spellCore = buffer.Get(corePrefabRef, pos, rot);
        }
        else if (triggerInfo.IsValid && triggerInfo.State != null && triggerInfo.State.Controller != null) //added a new check for a buffer on the controller who made the object - ie an NPC
        {
            Debug.Log("Trying to buffer spawn");
            NetworkObjectBuffer buffer = triggerInfo.State.Controller.GetComponent<NetworkObjectBuffer>();
            spellCore = buffer.Get(corePrefabRef, pos, rot);
        }

        if (spellCore == null)
        {
            // Fallback if we couldn't buffer-spawn it. -> just do it manually.
            Debug.LogError("Couldn't buffer spawn so falling back.");
            spellCore = BasicSpawner.Spawn(corePrefabRef, pos, rot);
        }
        InitialiseNonPhysicsObjectOnSpawn(spellCore, triggerInfo);
    }
*/
    public void InitialiseNonPhysicsObjectOnSpawn(NetworkObject spellCore, SpellTriggerInfo triggerInfo)
    {
        // This is called by the spawner before replicating the networkobject across all instances.
        SpellCreatedObject spellCreatedObject = spellCore.GetComponent<SpellCreatedObject>();

        //// Now going with the assumption that any object you create must be a SpellCreatedPhysicsObject
        //if (physicsObject == null)
        //{
        //    throw new System.Exception("Summoned object hsa no SpellCreatedPhysicsObject script.");
        //}

        if (!base_values_from_dependencies_stored)
        {
            // Only need to do this once.
            AppendBaseValuesFromDependency(spellCreatedObject);
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

       ///////// physicsObject.InitialiseOnSpawned(this, triggerInfo, triggerInfo.State);


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
        throw new System.NotImplementedException();
    }
}
