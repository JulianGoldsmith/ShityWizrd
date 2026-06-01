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
        if(castController is NPCActionController)
        {
            // if this is an NPC
        }
        else
        {
            state.chargeCastVFX = SpellSystemHelpers.CreateVFX(VFXContext.CastChargeEffect, ModifierType.Arcane, state.CastItem.projectileSpawnPoint, 1);
        }
        
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
        Vector3 spawnPosition;
        if (castController is NPCActionController NPC)
        {
            spawnPosition = NPC.spellSpawnPoint.position;
        }
        else
        {
            spawnPosition = state.Controller.inventory.activeItem.GetComponent<EquipableItem>().projectileSpawnPoint.position;
        }


        //spawnPosition = state.Controller.inventory.activeItem.GetComponent<EquipableItem>().projectileSpawnPoint.position;
        Quaternion spawnRotation = Quaternion.LookRotation(castController.GetForward());

        var triggerInfo = new SpellTriggerInfo(
            true,
            castController.gameObject, 
            state, 
            spawnPosition, 
            spawnRotation, 
            castController.GetForward()*state.CastChargeLevel* chargeMult, 
            castController.gameObject
        );
        triggerInfo.State.CastAimTargetPos = castController.GetAimTarget();
        state.CastRotation = spawnRotation;
        state.CastPosition = spawnPosition;

        foreach (var node in outcomeCoreNodes)
        {
            if (node is CoreNode coreNode)
            {
               // coreNode.CreateSpellCore(triggerInfo);
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

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        throw new System.NotImplementedException();
    }
}
