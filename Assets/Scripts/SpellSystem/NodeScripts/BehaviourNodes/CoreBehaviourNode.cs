using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "CoreBehaviourNode", menuName = "SpellNodes/CoreTypes/CoreBehaviourNode")]
public class CoreBehaviourNode : BehaviourNode
{
    [Promotable("Size", DataTypeTag.Radius)]
    public float size = 1f;

    public float lifeTime = 5f;


    public SpellPosition CastSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation CastSpawnRotation = SpellRotation.CasterRotation;
    public SpellPosition TriggerSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation TriggerSpawnRotation = SpellRotation.CasterRotation;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        throw new System.NotImplementedException();
    }

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        spellCore.transform.position = SpellSystemHelpers.GetSpellPosition(
            triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);
        spellCore.transform.rotation = SpellSystemHelpers.GetSpellRotation(
            triggerInfo.IsCast ? CastSpawnRotation : TriggerSpawnRotation, triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);

        float finalSize = size;
        float finalLifeTime = lifeTime;

        spellCore.transform.localScale = Vector3.one * finalSize;
        var coreController = spellCore.AddComponent<SpellLifeTimeBehaviour>();
        coreController.Init(finalLifeTime, false, triggerInfo);

        

       
    }
}
