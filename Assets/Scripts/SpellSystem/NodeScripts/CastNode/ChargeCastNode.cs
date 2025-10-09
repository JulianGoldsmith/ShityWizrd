using Unity.AppUI.Core;
using UnityEngine;

[CreateAssetMenu(fileName = "ChargeCastNode", menuName = "SpellNodes/CastNodes/ChargeCastNode")]

public class ChargeCastNode : CasterNode
{
    public float chargeValue;
    public float chargeSpeed = 2f;

    public float chargeMult = 10f;

    public override void OnCastStarted(SpellState state, CastActionController castController)
    {
 
        state.chargeCastVFX = SpellSystemHelpers.CreateVFX(ModifierType.Arcane, VFXContext.CastChargeEffect, state.CastItem.projectileSpawnPoint, 1);
        state.CastChargeLevel = 0;
    }

    public override void OnCastUpdate(SpellState state, CastActionController castController)
    {
        base.OnCastUpdate(state, castController);
        //handController.SetTemporaryRotation(handController.leftHand, flickVector.normalized);
        state.CastChargeLevel += Time.deltaTime * chargeSpeed;
    }

    public override void OnCastCanceled(SpellState state, CastActionController castController)
    {

        Vector3 spawnPosition = state.Controller.inventory.activeItem.GetComponent<EquipableItem>().projectileSpawnPoint.position;
        Quaternion spawnRotation = Quaternion.LookRotation(castController.GetForward());

        var triggerInfo = new SpellTriggerInfo(true, state, spawnPosition, spawnRotation, castController.GetForward()*state.CastChargeLevel* chargeMult, castController.gameObject);
        triggerInfo.State.CastAimTargetPos = castController.GetAimTarget();
        state.CastRotation = spawnRotation;
        state.CastPosition = spawnPosition;

        foreach (var node in outcomeCoreNodes)
        {
            if (node is CoreNode coreNode)
            {
                coreNode.CreateSpellCore(triggerInfo);
            }
            else if (node is EffectNode effectNode)
            {
                effectNode.Execute(triggerInfo);
            }
        }
        if (state.chargeCastVFX != null)
            GameObject.Destroy(state.chargeCastVFX);

        //handController.leftHand.currentHandState = handController.defaultHandState;
    }
}
