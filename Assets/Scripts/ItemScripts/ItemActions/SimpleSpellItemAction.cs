using Fusion;
using UnityEngine;

[CreateAssetMenu(fileName = "SimpleSpellItemAction",menuName = "ItemActions/Simple Spell")]
public class SimpleSpellItemAction : ItemAction
{
    [Header("Timings")]
    public float cooldown = 0.5f;
    public float comboWindow = 0.8f;

    public override void OnPress(int comboIndex,bool isAlreadyReleased)
    {

        //if (controller == null || Item == null)
        //    return;

        //if (controller.currentAttackCooldown > 0f)
        //{
        //    return;
        //}
    }

    public override void OnRelease(int comboIndex)
    {
        
        //    return;

        //if (controller.currentAttackCooldown > 0f)
        //    return;

        //SpellGraph graph = Item.primaryActionSpell;
        //if (graph == null)
        //{
        //    Debug.LogWarning($"Item '{Item.name}' has no primaryActionSpell assigned.");
        //    return;
        //}

        //graph.CompileSpell();

        //NetworkObject casterNo = controller.GetComponent<NetworkObject>();

        //SpellState state = new SpellState(
        //    controller,
        //    Item,
        //    graph,
        //    originalCasterNode: null, 
        //    caster: casterNo
        //);

        //Vector3 castPos;
        //Quaternion castRot;

        //if (Item.projectileSpawnPoint != null)
        //{
        //    castPos = Item.projectileSpawnPoint.position;
        //    castRot = Item.projectileSpawnPoint.rotation;
        //}
        //else
        //{
        //    castPos = controller.transform.position;
        //    castRot = Quaternion.LookRotation(controller.GetForward());
        //}

        //state.CastPosition = castPos;
        //state.CastRotation = castRot;
        //state.CastAimTargetPos = controller.GetAimTarget();
        //state.CastVelocity = controller.GetForward();

        //var triggerInfo = new SpellTriggerInfo(
        //    isCast: true,
        //    source: controller.gameObject,
        //    state: state,
        //    position: castPos,
        //    rotation: castRot,
        //    tiggerVector: state.CastVelocity,
        //    hitObject: null
        //);

        //graph.ExecuteComboIndex(comboIndex, triggerInfo);

        //controller.SetCoolDown(cooldown);
        //controller.StartComboTimer(comboWindow);

        //int totalActions = Item.primaryActions != null ? Item.primaryActions.Count : 0;
        //controller.AdvancePrimaryCombo(totalActions);
    }
}