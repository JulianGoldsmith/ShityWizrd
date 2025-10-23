using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "CoreBehaviourNode", menuName = "SpellNodes/CoreTypes/CoreBehaviourNode")]
public class CoreBehaviourNode : BehaviourNode
{
    [Promotable("Size", DataTypeTag.Radius)]
    public float size = 1f;

    public float lifeTime = 5f;

    public VFXContext coreVFX = VFXContext.None;

    public SpellPosition CastSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation CastSpawnRotation = SpellRotation.CasterRotation;
    public SpellPosition TriggerSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation TriggerSpawnRotation = SpellRotation.CasterRotation;

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        ApplyPromotableValues();
        spellCore.transform.position = SpellSystemHelpers.GetSpellPosition(
            triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);
        spellCore.transform.rotation = SpellSystemHelpers.GetSpellRotation(
            triggerInfo.IsCast ? CastSpawnRotation : TriggerSpawnRotation, triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);

        float finalSize = size;
        float finalLifeTime = lifeTime;

        spellCore.transform.localScale = Vector3.one * finalSize;
        var coreController = spellCore.AddComponent<SpellLifeTimeBehaviour>();
        coreController.Init(finalLifeTime, false, triggerInfo);

        if (coreVFX != VFXContext.None)
        {
            var vfx = SpellSystemHelpers.CreateVFX(coreVFX, ModifierType.Arcane, spellCore.transform, finalSize);
            if (vfx != null)
                spellCore.GetComponent<MeshRenderer>().enabled = false; //for now if we get the VFX we dont need the mesh renderer
        }

       
    }
}
