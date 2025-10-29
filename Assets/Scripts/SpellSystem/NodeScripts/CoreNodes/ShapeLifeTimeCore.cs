using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// A core node that defines shapes that last a defined lifetime. Inately adds the SpellLifeTimeBehavior
/// </summary>

[CreateAssetMenu(fileName = "3DShapeLifeTimeCore", menuName = "SpellNodes/CoreNodes/ShapeLifeTimeCore")]
public class ShapeLifeTimeCore : CoreNode
{
    [Promotable("Size", DataTypeTag.Radius)]
    public float size = 1f;

    public float lifeTime = 5f;
    public bool destroyOnRelease = false; //used for channeled abilities

    public VFXContext coreVFX = VFXContext.None;

    public SpellPosition CastSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation CastSpawnRotation = SpellRotation.CasterRotation;
    public SpellPosition TriggerSpawnPosition = SpellPosition.CasterPosition;
    public SpellRotation TriggerSpawnRotation = SpellRotation.CasterRotation;

    public override void CreateSpellCore(SpellTriggerInfo triggerInfo)
    {
        if (!CanSpawn(triggerInfo.State))
            return;

        GameObject spellCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spellCore.GetComponent<Collider>().isTrigger = true;

        spellCore.transform.position = SpellSystemHelpers.GetSpellPosition(
            triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition, triggerInfo);

        spellCore.transform.rotation = SpellSystemHelpers.GetSpellRotation(
            triggerInfo.IsCast ? CastSpawnRotation : TriggerSpawnRotation, triggerInfo.IsCast ? CastSpawnPosition : TriggerSpawnPosition,  triggerInfo);

        float finalSize = size;
        float finalLifeTime = lifeTime;

        spellCore.transform.localScale = Vector3.one * finalSize;

        var coreController = spellCore.AddComponent<SpellLifeTimeBehaviour>();
        coreController.Init(finalLifeTime, destroyOnRelease, triggerInfo);

        if (coreVFX != VFXContext.None)
        {
            var vfx = SpellSystemHelpers.CreateVFX(coreVFX, ModifierType.Arcane, spellCore.transform, finalSize);
            if (vfx != null)
                spellCore.GetComponent<MeshRenderer>().enabled = false; //for now if we get the VFX we dont need the mesh renderer
        }

        Rigidbody rb = SpellSystemHelpers.AddDefaultSpellRigidBodyToGameObject(spellCore);
        //rb.AddForce(triggerInfo.TriggerVector*100, ForceMode.Impulse);
        rb.linearVelocity = triggerInfo.TriggerVector * 8;

        AttatchBehavioursAndTriggers(spellCore, triggerInfo);
    }
}
