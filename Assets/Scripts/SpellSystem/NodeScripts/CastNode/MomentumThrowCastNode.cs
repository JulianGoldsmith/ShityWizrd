using UnityEngine;

[CreateAssetMenu(fileName = "MomentumThrowCastNode", menuName = "SpellNodes/CastNodes/MomentumThrowCastNode")]
public class MomentumThrowCastNode : CasterNode
{
    public HandState pointHandState;
    public float throwStrength = 10f;

    [Promotable("ThrowForce", DataTypeTag.Force)]
    public float throwForce = 1f;

    public override void OnCastStarted(SpellState state, CastActionController castController)
    {
        var handController = castController.GetComponentInChildren<PhysicsHandController>();
        if (handController == null) return;

        GameObject anchorGO = new GameObject("MomentumCastAnchor");
        anchorGO.transform.position = handController.rightHand.physicsProxy.position;
        anchorGO.transform.rotation = handController.rightHand.physicsProxy.rotation;

        handController.LockHandToTarget(handController.rightHand, anchorGO.transform);
        state.chargeCastVFX = SpellSystemHelpers.CreateVFX(ModifierType.Arcane, VFXContext.CastChargeEffect, state.CastItem.projectileSpawnPoint, 1);
        handController.SetHandState(handController.leftHand, pointHandState);
    }

    public override void OnCastUpdate(SpellState state, CastActionController castController)
    {
        base.OnCastUpdate(state, castController);

        var handController = castController.GetComponentInChildren<PhysicsHandController>();
        if (handController == null) return;
        
        Vector3 startPoint = handController.rightHand.temporaryTarget.transform.position;   
        Vector3 endPoint = handController.CalculateHandOffset(handController.rightHand, false);
        Vector3 throwVector = endPoint - startPoint;
        Vector3 camForward = state.Controller.GetForward().normalized * throwVector.magnitude;
        throwVector += camForward * 2;
        throwVector /= 3;

        handController.SetTemporaryRotation(handController.leftHand, throwVector.normalized);
    }

    public override void OnCastCanceled(SpellState state, CastActionController castController)
    {
        var handController = castController.GetComponentInChildren<PhysicsHandController>();
        if (handController == null) return;

        GameObject anchor = handController.rightHand.temporaryTarget.transform.gameObject;

        Vector3 startPoint = handController.rightHand.temporaryTarget.transform.position;
        Vector3 endPoint = handController.CalculateHandOffset(handController.rightHand, false);
        Vector3 throwVector = (endPoint - startPoint);

        Vector3 camForward = state.Controller.GetForward().normalized * throwVector.magnitude;
        throwVector += camForward * 2;
        throwVector /= 3;

        handController.ReleaseHand(handController.rightHand);
        handController.ClearTemporaryRotation(handController.leftHand);
        handController.SetHandState(handController.leftHand, handController.defaultHandState);

        Quaternion spawnRotation = Quaternion.LookRotation(throwVector.normalized);

        Debug.Log($"Throw vector is {throwVector}");

        var triggerInfo = new SpellTriggerInfo(true, state, endPoint, spawnRotation, throwVector* throwStrength * throwForce, castController.gameObject);
        triggerInfo.State.CastAimTargetPos = castController.GetAimTarget();
        state.CastPosition = state.CastItem.projectileSpawnPoint.position;
        state.CastRotation = spawnRotation;

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

        Destroy(anchor);
        if (state.chargeCastVFX != null)
            GameObject.Destroy(state.chargeCastVFX);
    }
}
