using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// A core node, that instantiates a gameObject as a base. The GameObject will need have relevent components like rb, colliders and mesh renderer
/// </summary>


[CreateAssetMenu(fileName = "ObjectCore", menuName = "SpellNodes/CoreNodes/ObjectCore")]
public class ObjectCore : CoreNode
{
    public GameObject corePrefab;


    public SpellPosition CastSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation CastSpawnRotation = SpellRotation.CasterRotation;
    public SpellPosition TriggerSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation TriggerSpawnRotation = SpellRotation.CasterRotation;

    [Promotable("Size", DataTypeTag.Radius)]
    public float size = 1f;
    
    public override void CreateSpellCore(SpellTriggerInfo triggerInfo)
    {
        ApplyPromotableValues(); //apply promotable values from connected runes 


        Vector3 pos = SpellSystemHelpers.GetSpellPosition(
            triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);
        Quaternion rot = SpellSystemHelpers.GetSpellRotation(
            triggerInfo.IsCast ? CastSpawnRotation : TriggerSpawnRotation, triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);

        GameObject spellCore = Instantiate(corePrefab, pos, rot);

        SpellCreatedPhysicsObject physicsObject = spellCore.GetComponent<SpellCreatedPhysicsObject>();
        if (physicsObject != null)
        {
            physicsObject.AssignProperties(this);
            physicsObject.InitialisePhysicsObject();
        }

        spellCore.transform.localScale *= size;

        /*Debug.Log($"is cast = {triggerInfo.IsCast} [Spawn] posType={CastSpawnPosition} rotType={CastSpawnRotation} " +
          $"CastPos={triggerInfo.State.CastPosition} Override?={triggerInfo.HasOverridePosition} " +
          $"TrigPos={triggerInfo.TriggerPoint} and spell core is {spellCore.transform.position}");*/

        AttatchBehavioursAndTriggers(spellCore, triggerInfo);
    }
}
