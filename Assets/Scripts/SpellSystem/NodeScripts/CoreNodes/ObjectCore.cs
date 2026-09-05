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

    [Header("The Payload")]
    public GameObject attachedSpellComponentsPrefab;

    public NetworkPrefabRef prefabRefToBuffer { get { return corePrefabRef; } }

    [Promotable("Lifetime", DataTypeTag.Lifetime)]
    public float lifetime = 0;

    [Promotable("Size", DataTypeTag.Radius)] // (Use whatever tag you prefer)
    public float size = 1f;

    [MaterialID]
    [Promotable("Material", DataTypeTag.Material)] // (Use whatever tag you prefer)
    public ushort material;

    private bool base_values_from_dependencies_stored = false;

    public SpellPosition CastSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation CastSpawnRotation = SpellRotation.CasterRotation;
    public SpellPosition TriggerSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation TriggerSpawnRotation = SpellRotation.CasterRotation;




    /*public void InitialisePhysicsObjectOnSpawn(NetworkObject spellCore, SpellTriggerInfo triggerInfo)
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
    //}*/
    // }



    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        RuntimeObjectCore runtimeCore = CreateRuntimeCore();

        runtimeCore.ArrayIndex = context.CurrentNodeIndex;
        runtimeCore.Template = this;
        runtimeCore.PrefabRef = corePrefabRef;
        runtimeCore.AttachedSpellComponentsPrefab = attachedSpellComponentsPrefab;
        runtimeCore.CastSpawnPosition = CastSpawnPosition;
        runtimeCore.CastSpawnRotation = CastSpawnRotation;
        runtimeCore.TriggerSpawnPosition = TriggerSpawnPosition;
        runtimeCore.TriggerSpawnRotation = TriggerSpawnRotation;
        runtimeCore.OriginalTemplateGuid = InstanceGuid;
        runtimeCore.lifetime = new RuntimeFloatProperty(lifetime);
        runtimeCore.size = new RuntimeFloatProperty(size);
        runtimeCore.material = new RuntimeMaterialProperty(material);

        if (defaultBehaviourNodes != null)
        {
            foreach (BehaviourNode behaviour in defaultBehaviourNodes)
            {
                if (behaviour != null)
                    runtimeCore.AddBehaviour((IBehaviour)behaviour.CompileNode(context));
            }
        }

        if (defaultTriggerNodes != null)
        {
            foreach (TriggerNode trigger in defaultTriggerNodes)
            {
                if (trigger != null)
                    runtimeCore.AddTrigger((ITrigger)trigger.CompileNode(context));
            }
        }

        return runtimeCore;
    }

    protected virtual RuntimeObjectCore CreateRuntimeCore()
    {
        return new RuntimeObjectCore();
    }
}

public class RuntimeObjectCore : RuntimeCoreBase
{
    public int ArrayIndex;
    public ObjectCore Template;
    public NetworkPrefabRef PrefabRef;
    public GameObject AttachedSpellComponentsPrefab;
    public SpellPosition CastSpawnPosition;
    public SpellRotation CastSpawnRotation;
    public SpellPosition TriggerSpawnPosition;
    public SpellRotation TriggerSpawnRotation;
    public string OriginalTemplateGuid;
    public bool IsKinematic;
    public RuntimeFloatProperty lifetime;
    public RuntimeFloatProperty size;
    public RuntimeMaterialProperty material;

    public override void ExecuteCore(SpellTriggerInfo triggerInfo)
    {
        SpawnCore(triggerInfo);
    }

    public virtual void TickBeforePhysics(SpellCreatedCore core) { }

    public virtual void TickAfterPhysics(SpellCreatedCore core) { }

    public virtual void AfterRender(SpellCreatedCore core) { }

    protected SpellCreatedCore SpawnCore(SpellTriggerInfo triggerInfo)
    {
        Vector3 position = SpellSystemHelpers.GetSpellPosition(triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);
        Quaternion rotation = SpellSystemHelpers.GetSpellRotation(triggerInfo.IsCast ? CastSpawnRotation : TriggerSpawnRotation, triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);

        NetworkObject sourceObject = null;
        NetworkId currentTarget = default;

        if (triggerInfo.Source != null)
            triggerInfo.Source.TryGetComponent(out sourceObject);

        if (triggerInfo.HitObject != null && triggerInfo.HitObject.TryGetComponent(out NetworkObject targetObject))
            currentTarget = targetObject.Id;

        ObjectBuffer objectBuffer = null;

        if (ObjectBufferAllocator.Instance != null)
            objectBuffer = ObjectBufferAllocator.Instance.GetBufferForCaster(sourceObject);

        NetworkObject spellCore;
        int localBufferIndex = -1;

        if (objectBuffer != null)
            spellCore = objectBuffer.GetBufferedObject(out localBufferIndex);
        else
            spellCore = BasicSpawner.Spawn(PrefabRef, position, rotation);

        if (spellCore == null || !spellCore.TryGetComponent(out SpellCreatedCore lifecycleManager))
            return null;

        CoreContext context = new CoreContext
        {
            SpawnPosition = position,
            CastChargeLevel = triggerInfo.State.CastChargeLevel,
            TriggerVector = triggerInfo.TriggerVector,
            BufferSourceID = default,
            CurrentTarget = currentTarget
        };

        if (!lifecycleManager.Initialize(triggerInfo.State.ActiveCastID, triggerInfo.State.SpellGraphIdFrom, context, ArrayIndex, localBufferIndex, position, rotation, IsKinematic))
            return null;

        return lifecycleManager;
    }

}
