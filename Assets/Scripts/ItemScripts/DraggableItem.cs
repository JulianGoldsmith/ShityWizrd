using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.Analytics;

[RequireComponent(typeof(NetworkRigidbody3D))]
public class DraggableItem : InteractableItem
{
    [HideInInspector]
    public Rigidbody rb;

    public bool isCharacterObject = false;

    private XPBDGlobalManager _globalManager;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody>();
        networkedRB = this.GetComponent<NetworkRigidbody3D>();
        Runner.SetIsSimulated(this.Object, true);
        _globalManager = FindObjectOfType<XPBDGlobalManager>();
    }

    public override void FixedUpdateNetwork()
    {

    }

    public Vector3 GetPlayerTargetHoldPos(NetworkObject playerObj, out Vector3 dragFacingDir)
    {
        Vector3 dragTargetPos;
        if (playerObj != null &&
          playerObj.TryGetComponent(out NetworkedHandsController hands) &&
          playerObj.TryGetComponent(out HybridCharacterController controller) &&
          playerObj.TryGetComponent(out NetworkedInventoryManager inv))
        {

            Vector3 eyePos = controller.GetEyePos();
            Quaternion lookRot = controller.GetLookRot();

            float pitch = lookRot.eulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
            pitch = -pitch;
            float pitch01 = (pitch + 90f) / 180f;

            float addedHeight = hands.dragPitchToHeightModifierCurve.Evaluate(pitch01);

            Vector3 offset = hands.dragTargetOffset + new Vector3(0f, addedHeight, hands.DragDistance);

            Vector3 targetPos = eyePos + (lookRot * offset);
            dragTargetPos = targetPos;

            var itemNO = inv.currentItemInHand;
            var item = itemNO.GetComponent<DraggableItem>();
            Vector3 com = (item != null && item.rb != null) ? item.rb.worldCenterOfMass : itemNO.transform.position;

            Vector3 facing = (eyePos - com);
            dragFacingDir = facing.sqrMagnitude > 1e-6f ? facing.normalized : Vector3.forward;


        }
        else
        {
            dragFacingDir = Vector3.zero;
            dragTargetPos = Vector3.zero;
        }
        return dragTargetPos;
    }

    public override void PickUpItem(NetworkObject playerObject)
    {
       // networkedRB.RBIsKinematic = false; // (Or true, depending on if you want unity physics off)

        if (_globalManager != null && playerObject.TryGetComponent(out HybridCharacterController controller))
        {
            var hands = playerObject.GetComponent<NetworkedHandsController>();
            hands.RightHandMode = TargetingMode.DRAGG;

            Vector3 handPos = hands.rightHand.transformNet.transform.position;
            Vector3 localGrabPos = transform.InverseTransformPoint(handPos);
            Vector3 eyePos = controller.GetEyePosSim();
            float distance = Vector3.Distance(eyePos, rb.worldCenterOfMass);
            Quaternion snapshotRot = Quaternion.Inverse(controller.lookRot) * rb.rotation;

            NetworkGrabJoint newGrab = new NetworkGrabJoint()
            {
                grabberId = playerObject.Id,
                itemId = this.Object.Id,
                localGrabOffset = localGrabPos,
                grabDistance = distance,
                targetLocalRotation = snapshotRot,
                grabStrength = controller.dragStength,
                grabDamping = controller.grabDamping, // (or controller.grabDamping)
                dragResistance = controller.playerDragResistance,
            };

            _globalManager.AddGrabJoint(newGrab);
        }
    }

    public override void DropItem(NetworkObject playerObject, bool hasInputAuthority, bool hasStateAuthority)
    {
        if (_globalManager != null)
        {
            _globalManager.RemoveGrabJoint(playerObject.Id, this.Object.Id);
        }

        if (playerObject.TryGetComponent<NetworkedHandsController>(out var hands))
        {
            hands.RightHandMode = TargetingMode.ARMATURE;
            hands.SetHandTarget_ToArmature(false);
        }

    }


}
