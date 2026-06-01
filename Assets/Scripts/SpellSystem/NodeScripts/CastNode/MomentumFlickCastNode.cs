using Unity.AppUI.Core;
using UnityEngine;

[CreateAssetMenu(fileName = "MomentumFlickCastNode", menuName = "SpellNodes/CastNodes/MomentumFlickCastNode")]

public class MomentumFlickCastNode : CasterNode
{
    [Tooltip("HandState to use while holding the cast button")]
    public HandState flickCastHandState;
    [Tooltip("How long after releasing the cast the hand stays in the flick state")]
    public float returnToNormalDelay = 0.25f;
    public HandState itemHandState;
    public float momentumMult = 0.01f;

    public HandState pointHandState;

    [Promotable("ThrowForce", DataTypeTag.Force)]
    public float throwForce = 1f;

    public override void OnCastStarted(SpellState state, CastActionController castController)
    {
        var handController = castController.GetComponentInChildren<PhysicsHandController>();
        if (handController == null || flickCastHandState == null) return;
        itemHandState = state.Controller.inventory.activeItem.GetComponent<EquipableItem>().heldHandState;
        handController.SetHandState(handController.rightHand, flickCastHandState);
        state.chargeCastVFX = SpellSystemHelpers.CreateVFX(VFXContext.CastChargeEffect, ModifierType.Arcane, state.CastItem.projectileSpawnPoint, 1);
        //handController.leftHand.currentHandState = pointHandState;
    }

    public override void OnCastUpdate(SpellState state, CastActionController castController)
    {
        base.OnCastUpdate(state, castController);
        var handController = castController.GetComponentInChildren<PhysicsHandController>();
        if (handController == null) return;

        var rightHand = handController.rightHand;
        Vector3 flickVector = rightHand.fixedUpdateHandDifVector;

        //handController.SetTemporaryRotation(handController.leftHand, flickVector.normalized);
    }

    public override void OnCastCanceled(SpellState state, CastActionController castController)
    {
        var handController = castController.GetComponentInChildren<PhysicsHandController>();
        if (handController == null || flickCastHandState == null) return;

        var rightHand = handController.rightHand;

        Vector3 flickVector = rightHand.fixedUpdateHandDifVector;

        castController.ChangeHandStateAfterDelay(handController, rightHand, itemHandState, returnToNormalDelay);

        Vector3 spawnPosition = state.Controller.inventory.activeItem.GetComponent<EquipableItem>().projectileSpawnPoint.position;
        Quaternion spawnRotation = Quaternion.LookRotation(flickVector.normalized);

        var triggerInfo = new SpellTriggerInfo(
            true, 
            castController.gameObject, 
            state, 
            spawnPosition, 
            spawnRotation, 
            flickVector * throwForce, 
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
