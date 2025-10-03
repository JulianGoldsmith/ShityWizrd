using Fusion;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// A core node, that instantiates a gameObject as a base. The GameObject will need have relevent components like rb, colliders and mesh renderer
/// </summary>


[CreateAssetMenu(fileName = "ObjectCore", menuName = "SpellNodes/CoreNodes/ObjectCore")]
public class ObjectCore : CoreNode
{
    // Should this be a networkprefabref
    // Should there be a generic objectcore networkprefabref,
    // then we replace its components at run time? 
    // or a unique networkprefabref per thing that can be spawned?
    public GameObject corePrefab;
    public NetworkPrefabRef corePrefabRef;

    private bool base_values_from_dependencies_stored = false;

    public SpellPosition CastSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation CastSpawnRotation = SpellRotation.CasterRotation;
    public SpellPosition TriggerSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation TriggerSpawnRotation = SpellRotation.CasterRotation;
    
    public override void CreateSpellCore(SpellTriggerInfo triggerInfo)
    {
        ApplyPromotableValues(); //apply promotable values from connected runes 

        Vector3 pos = SpellSystemHelpers.GetSpellPosition(
            triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);
        Quaternion rot = SpellSystemHelpers.GetSpellRotation(
            triggerInfo.IsCast ? CastSpawnRotation : TriggerSpawnRotation, triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);

        // Create a lambda expression to be ran onspawn, then replicated across instances.
        NetworkRunner.OnBeforeSpawned beforespawned = (Runner, NObject) => InitialisePhysicsObjectOnSpawn(NObject, triggerInfo);

        NetworkObject spellCore = BasicSpawner.Spawn(corePrefabRef, pos, rot, beforespawned);
    }

    public void InitialisePhysicsObjectOnSpawn(NetworkObject spellCore, SpellTriggerInfo triggerInfo)
    {
        // This is called by the spawner before replicating the networkobject
        // across all instances.
        SpellCreatedPhysicsObject physicsObject = spellCore.GetComponent<SpellCreatedPhysicsObject>();
        if (physicsObject != null)
        {
            // We allow modification of values also within the created physicsobject
            // as well as its properties.
            // We capture the base values here, since this is the first time
            // we're seeing it. Then we can apply promotable values.
            if (!base_values_from_dependencies_stored)
            {
                // Only need to do this once.
                AppendBaseValuesFromDependency(physicsObject);
                AppendBaseValuesFromDependency(physicsObject.physicsObjectProperties);
                base_values_from_dependencies_stored = true;
            }
            physicsObject = ApplyPromotableValuesGeneric<SpellCreatedPhysicsObject>(physicsObject);
            physicsObject.physicsObjectProperties = ApplyPromotableValuesGeneric<PhysicsObjectProperties>(physicsObject.physicsObjectProperties);
            physicsObject.AssignProperties(this);
            physicsObject.InitialisePhysicsObject();
        }


        /*Debug.Log($"is cast = {triggerInfo.IsCast} [Spawn] posType={CastSpawnPosition} rotType={CastSpawnRotation} " +
          $"CastPos={triggerInfo.State.CastPosition} Override?={triggerInfo.HasOverridePosition} " +
          $"TrigPos={triggerInfo.TriggerPoint} and spell core is {spellCore.transform.position}");*/

        AttatchBehavioursAndTriggers(spellCore.gameObject, triggerInfo);
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
