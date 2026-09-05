using Fusion;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-10)]
public class NetworkedInventoryManager : NetworkBehaviour
{

    public const int InventoryCapacity = 3;
    public const byte NoActiveSlot = byte.MaxValue;
    [Networked, Capacity(InventoryCapacity)]
    public NetworkArray<NetworkId> EquippedItemIds { get; }
    [Networked] public byte ActiveSlot { get; set; }
    [Networked] public NetworkObject DraggedItem { get; set; }

    public NetworkId CurrentEquippedItemId => DraggedItem == null && ActiveSlot < InventoryCapacity ? EquippedItemIds[ActiveSlot] : default;

    public NetworkObject CurrentEquippedItem
    {
        get
        {
            NetworkId itemId = CurrentEquippedItemId;
            return itemId.IsValid && Runner.TryFindObject(itemId, out NetworkObject item) ? item : null;
        }
    }


    public Transform itemSocketR;


    public Transform snapPoint;

    [SerializeField] private NetworkedHandsController handController;
    [SerializeField] private HybridCharacterController characterController;
    //[SerializeField] private Transform playerCamera;

    [SerializeField] private float pickupRadius = 3f;
    [SerializeField] private float pickupAngle = 45f;
    [SerializeField] public LayerMask itemLayer;

    [Header("Rune Levitation")]
    [Min(0.05f)] public float RuneLevitationHoldDuration = 0.35f;

    [Header("Rune Detachment")]
    [Min(0.05f)] public float RuneDetachmentHoldDuration = 0.35f;

    [Networked] public NetworkObject potentialItemToPickup { get; set; }
    [Networked] private NetworkId ReleaseHoldItemId { get; set; }
    [Networked] private TickTimer ReleaseHoldTimer { get; set; }
    [Networked] private NetworkBool ReleaseHoldTriggered { get; set; }
    [Networked] private NetworkId InteractHoldItemId { get; set; }
    [Networked] private NetworkInteractionTarget InteractHoldTarget { get; set; }
    [Networked] private TickTimer InteractHoldTimer { get; set; }
    [Networked] private NetworkBool InteractHoldAttempted { get; set; }
    [Networked] private NetworkBool InteractHoldTriggered { get; set; }
    [Networked] private NetworkId PendingDetachedPickupItemId { get; set; }
    [Networked] private TickTimer PendingDetachedPickupTimer { get; set; }

    [Networked] public Vector3 localHandPosOnItem { get; set; }

    Quaternion lookRotation;
    [Networked] NetworkButtons Prior_buttons { get; set; }

    [Networked] public Vector3 _dragTargetPos { get; set; }
    [Networked] public Vector3 _dragFacingDir { get; set; }

    public void Start()
    {
        characterController = GetComponent<HybridCharacterController>();

    }

    public override void Spawned()
    {
        Runner.SetIsSimulated(Object, true);
        if (HasStateAuthority) ActiveSlot = 0;
        if (HasInputAuthority) GameController.Instance.spellGraphController.inventory = this;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.IsProxy)
        {
            SimulateInventory();
            ApplyEquipmentHandState();
        }

        AssignEquipmentContexts();
    }

    private void AssignEquipmentContexts()
    {
        for (byte slot = 0; slot < InventoryCapacity; slot++)
        {
            NetworkId itemId = EquippedItemIds[slot];
            if (!itemId.IsValid || !Runner.TryFindObject(itemId, out NetworkObject itemObject) || !itemObject.TryGetComponent(out EquipableItem item)) continue;

            item.AssignEquipmentContext(Object, slot, slot == ActiveSlot && DraggedItem == null);
        }
    }

    public void SimulateInventory()
    {
        if (Object.IsProxy) return;
        if (GetInput(out NetworkInputData data))
        {
            _dragTargetPos = data.dragTargetPos;
            _dragFacingDir = data.dragFacingDir;

            lookRotation = data.lookRotation;

            if (PendingDetachedPickupItemId.IsValid && PendingDetachedPickupTimer.Expired(Runner))
            {
                if (!characterController.IsBroken && DraggedItem == null)
                    PickupItem(PendingDetachedPickupItemId);

                PendingDetachedPickupItemId = default;
                PendingDetachedPickupTimer = default;
            }

            if (data.levitationRotationItemId.IsValid &&
                Runner.TryFindObject(data.levitationRotationItemId, out NetworkObject levitatingObject) &&
                levitatingObject.TryGetComponent(out RuneRigObject levitatingRig) &&
                levitatingRig.IsLevitating &&
                HybridCharacterController.IsFinite(data.levitationTargetRotation))
            {
                levitatingRig.LevitationTargetRotation = Quaternion.Normalize(data.levitationTargetRotation);
            }

            if (data.buttons.WasPressed(Prior_buttons, EInputButton.SLOT_1)) SelectSlot(0);
            if (data.buttons.WasPressed(Prior_buttons, EInputButton.SLOT_2)) SelectSlot(1);
            if (data.buttons.WasPressed(Prior_buttons, EInputButton.SLOT_3)) SelectSlot(2);

            if (DraggedItem == null)
            {
                LookForItems();
            }

            if (data.buttons.WasPressed(Prior_buttons, EInputButton.PICKUP))
            {
                if (!characterController.IsBroken)
                {
                    if (DraggedItem == null)
                    {
                        InteractHoldItemId = potentialItemToPickup != null ? potentialItemToPickup.Id : default;
                        InteractHoldTarget = data.interactionTarget;
                        InteractHoldTimer = TickTimer.CreateFromSeconds(Runner, RuneDetachmentHoldDuration);
                        InteractHoldAttempted = false;
                        InteractHoldTriggered = false;
                    }
                    else
                    {
                        ClearInteractHoldState();
                        TryAttachHeldRuneRig(data);
                    }
                }
            }

            if (data.buttons.IsSet(EInputButton.PICKUP) &&
                !InteractHoldAttempted &&
                InteractHoldTarget.Type == InteractionTargetType.RuneNode &&
                InteractHoldTarget.PartIndex > 0 &&
                InteractHoldTimer.Expired(Runner))
            {
                InteractHoldAttempted = true;
                InteractHoldTriggered = TryDetachRuneFromInput(InteractHoldTarget);
            }

            if (data.buttons.WasReleased(Prior_buttons, EInputButton.PICKUP))
            {
                if (!InteractHoldTriggered && InteractHoldItemId.IsValid && DraggedItem == null)
                    PickupItem(InteractHoldItemId);

                ClearInteractHoldState();
            }

            if (data.buttons.WasPressed(Prior_buttons, EInputButton.RELEASE) && DraggedItem != null)
            {
                ReleaseHoldItemId = DraggedItem.Id;
                ReleaseHoldTimer = TickTimer.CreateFromSeconds(Runner, RuneLevitationHoldDuration);
                ReleaseHoldTriggered = false;
            }

            if (data.buttons.IsSet(EInputButton.RELEASE) &&
                !ReleaseHoldTriggered &&
                ReleaseHoldItemId.IsValid &&
                ReleaseHoldTimer.Expired(Runner) &&
                DraggedItem != null &&
                DraggedItem.Id == ReleaseHoldItemId &&
                DraggedItem.TryGetComponent(out RuneRigObject heldRuneRig))
            {
                if (!characterController.IsBroken)
                {
                    ReleaseHoldTriggered = true;
                    LevitateHeldRuneRig(heldRuneRig);
                }
            }

            if (data.buttons.WasReleased(Prior_buttons, EInputButton.RELEASE))
            {
                if (!ReleaseHoldTriggered && !characterController.IsBroken)
                {
                    if (ReleaseHoldItemId.IsValid && DraggedItem != null && DraggedItem.Id == ReleaseHoldItemId)
                    {
                        DropItem();
                    }
                    else if (DraggedItem == null && CurrentEquippedItem != null)
                    {
                        DropActiveEquippedItem();
                    }
                }

                ClearReleaseHoldState();
            }

            Prior_buttons = data.buttons;
        }

        if (characterController.IsBroken)
        {
            if (DraggedItem != null) DropItem();
            if (CurrentEquippedItem != null) DropActiveEquippedItem();

            ClearReleaseHoldState();
            ClearInteractHoldState();
            potentialItemToPickup = null;
            return;
        }

        if (DraggedItem != null && !DraggedItem.gameObject.activeInHierarchy)
        {
            DraggedItem = null;
            ClearReleaseHoldState();
            ClearInteractHoldState();
        }

        
    }
    private void SelectSlot(byte slot)
    {
        if (DraggedItem != null || slot >= InventoryCapacity) return;
        ActiveSlot = slot;
    }
    public bool TrySetActiveSlot(byte slot)
    {
        if (slot >= InventoryCapacity || !EquippedItemIds[slot].IsValid) return false;

        if (DraggedItem != null) DropItem();

        ActiveSlot = slot;
        return true;
    }

    public void HolsterActiveItem()
    {
        ActiveSlot = NoActiveSlot;
    }

    private void LookForItems()
    {
        InteractableItem bestCandidate = null;
        Debug.DrawRay(characterController.GetEyePos(), (characterController.GetLookRot().normalized * Vector3.forward * pickupRadius), Color.red);

        bool overrideUpdatePos = false;

        if (Physics.Raycast(characterController.GetEyePos(), characterController.GetLookRot() * Vector3.forward, out RaycastHit hit, pickupRadius, itemLayer)
            && SpellSystemHelpers.GetHitGameObject(hit.collider).TryGetComponent<InteractableItem>(out bestCandidate))
        {
            //bestCandidate = hit.collider.GetComponent<InteractableItem>();
            overrideUpdatePos = true; //if finding by raycast it should be more accurate
        }
        else
        {
            Collider[] nearbyItems = Physics.OverlapSphere(characterController.GetEyePos(), pickupRadius, itemLayer);


            float bestDot = -1f;

            foreach (var col in nearbyItems)
            {

                Vector3 directionToItem = (col.transform.position - characterController.GetEyePos()).normalized;
                Vector3 forwardDir = lookRotation * Vector3.forward;
                float dot = Vector3.Dot(forwardDir, directionToItem);

                if (dot > Mathf.Cos(pickupAngle * Mathf.Deg2Rad))
                {
                    if (dot > bestDot && SpellSystemHelpers.GetHitGameObject(col).TryGetComponent<InteractableItem>(out var newBest))
                    {
                        bestDot = dot;
                        bestCandidate = newBest;
                        //Debug.Log($"looking for selected found this: {col}");
                    }
                }
            }
        }
        

        if ((bestCandidate != null && potentialItemToPickup != bestCandidate) || overrideUpdatePos)
        {

            //decide if item or physics grabbable 
            //Debug.Log($"Best candidate found {bestCandidate.gameObject.name}");
            if(!bestCandidate.gameObject.TryGetComponent<NetworkObject>(out var pITP)) return;

            potentialItemToPickup = pITP;


            if (potentialItemToPickup == null) return;

            if (bestCandidate is EquipableItem equipable)
            {
                handController.SetHandTarget_ToPickUpPoint(false, equipable.primaryHandle, equipable.heldHandState);

                //Debug.Log($"looking for selected set the hands to pick up : {potentialItemToPickup.name}");
            }
            else if(bestCandidate is DraggableItem draggable)
            {
                //Debug.Log($"looking for selected set the hands to DRAGG : {potentialItemToPickup.name}");
                if (Physics.Raycast(characterController.GetEyePos(), characterController.GetLookRot() * Vector3.forward, out RaycastHit hitted, pickupRadius*2, itemLayer))
                {
                    handController.SetHandTarget_ToDraggPoint(false, draggable, hitted.point);
                    localHandPosOnItem = draggable.transform.InverseTransformPoint(hitted.point);
                }
                else if (Physics.Raycast(characterController.GetEyePos(), draggable.transform.position - characterController.GetEyePos(), out RaycastHit hitted2, pickupRadius * 2, itemLayer))
                {
                    handController.SetHandTarget_ToDraggPoint(false, draggable, hitted2.point);
                    localHandPosOnItem = draggable.transform.InverseTransformPoint(hitted2.point);
                }
                else //fallback if somehownot hit by ray
                {
                    handController.SetHandTarget_ToDraggPoint(false, draggable, draggable.transform.position);
                }
            }
        }
        else if (bestCandidate == null && potentialItemToPickup != null && DraggedItem == null)
        {
            handController.SetHandTarget_ToArmature(false);
            potentialItemToPickup = null;
            DraggedItem = null;
            handController.leftHand.draggingTransform = null;
            handController.rightHand.draggingTransform = null;
        }
    }

    private void ApplyEquipmentHandState()
    {
        if (DraggedItem != null) return;

        NetworkObject itemObject = CurrentEquippedItem;

        if (itemObject != null && itemObject.TryGetComponent(out EquipableItem item))
        {
            handController.SetHandTarget_ToHold(false, item.heldHandState);

            if (item.secondaryHandle != null)
            {
                handController.SetHandTarget_ToHold(true, item.heldHandState);
            }
            else
            {
                handController.SetHandTarget_ToArmature(true);
            }

            return;
        }

        if (potentialItemToPickup != null) return;

        handController.SetHandTarget_ToArmature(false);
        handController.SetHandTarget_ToArmature(true);
    }

    private void PickupItem(NetworkId itemId)
    {
        if (!itemId.IsValid || !Runner.TryFindObject(itemId, out NetworkObject itemObject) || !itemObject.TryGetComponent(out InteractableItem item)) return;

        potentialItemToPickup = null;

        if (item is EquipableItem)
        {
            TryEquipItem(itemObject);
            return;
        }

        if (item is DraggableItem draggable)
        {
            DraggedItem = itemObject;
            draggable.PickUpItem(Object);
        }
    }

    private void DropItem()
    {
        if (DraggedItem == null)
            return;

        NetworkObject droppedObject = DraggedItem;
        InteractableItem droppedItem = droppedObject.GetComponent<InteractableItem>();

        droppedItem.DropItem(GetComponent<NetworkObject>(), HasInputAuthority, HasStateAuthority);

        handController.DragDistance = 0;
        DraggedItem = null;
    }

    private bool TryEquipItem(NetworkObject item)
    {
        if (ActiveSlot >= InventoryCapacity || EquippedItemIds[ActiveSlot].IsValid) return false;

        EquippedItemIds.Set(ActiveSlot, item.Id);
        return true;
    }

    private void DropActiveEquippedItem()
    {
        if (ActiveSlot >= InventoryCapacity || !EquippedItemIds[ActiveSlot].IsValid) return;

        EquippedItemIds.Set(ActiveSlot, default);
    }

    private bool TryAttachHeldRuneRig(NetworkInputData data)
    {
        if (DraggedItem == null ||
            !DraggedItem.TryGetComponent(out RuneRigObject runeRig) ||
            data.interactionTarget.Type != InteractionTargetType.RuneBay ||
            !data.interactionTarget.ObjectId.IsValid)
        {
            return false;
        }

        runeRig.DropItem(Object, HasInputAuthority, HasStateAuthority);
        bool attached = runeRig.TryAttachToBay(data.interactionTarget.ObjectId, data.interactionTarget.PartIndex, data.interactionTarget.BayIndex);

        handController.DragDistance = 0;
        DraggedItem = null;
        ClearReleaseHoldState();
        return attached;
    }

    private void LevitateHeldRuneRig(RuneRigObject runeRig)
    {
        if (DraggedItem == null || runeRig == null || DraggedItem != runeRig.Object)
            return;

        runeRig.DropItem(Object, HasInputAuthority, HasStateAuthority);
        runeRig.BeginLevitation();

        handController.DragDistance = 0;
        DraggedItem = null;
    }

    private void ClearReleaseHoldState()
    {
        ReleaseHoldItemId = default;
        ReleaseHoldTimer = default;
        ReleaseHoldTriggered = false;
    }

    private void ClearInteractHoldState()
    {
        InteractHoldItemId = default;
        InteractHoldTarget = default;
        InteractHoldTimer = default;
        InteractHoldAttempted = false;
        InteractHoldTriggered = false;
    }

    private bool TryDetachRuneFromInput(NetworkInteractionTarget target)
    {
        if (!target.IsValid || target.Type != InteractionTargetType.RuneNode)
            return false;

        if (!Runner.TryFindObject(target.ObjectId, out NetworkObject rigObject))
            return false;

        if (!rigObject.TryGetComponent(out RuneRigObject runeRig))
            return false;

        RuneObject selectedRune = runeRig.GetRuneObject(target.PartIndex);

        if (selectedRune == null)
            return false;

        Vector3 detachedPosition = selectedRune.transform.position;
        Quaternion detachedRotation = selectedRune.transform.rotation;
        Vector3 handTargetPosition = runeRig.transform.TransformPoint(localHandPosOnItem);

        if (!runeRig.TryDetachRune(target.PartIndex, Object.InputAuthority, detachedPosition, detachedRotation, out NetworkObject detachedObject))
            return false;

        localHandPosOnItem = Quaternion.Inverse(detachedRotation) * (handTargetPosition - detachedPosition);
        PendingDetachedPickupItemId = detachedObject.Id;
        PendingDetachedPickupTimer = TickTimer.CreateFromTicks(Runner, 1);
        return true;
    }

    public bool TryGetLookedAtInteractionTarget(out NetworkInteractionTarget target)
    {
        target = default;

        Vector3 origin = characterController.GetEyePos();
        Vector3 direction = characterController.GetLookRot() * Vector3.forward;
        int targetLayers = itemLayer.value | LayerMask.GetMask("Ragdoll", "Enviroment", "Default");

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, pickupRadius * 2f, targetLayers))
            return false;

        RuneObject runeObject = hit.collider.GetComponentInParent<RuneObject>();

        if (runeObject != null && runeObject.OwningRig != null)
        {
            RuneRigObject runeRig = runeObject.OwningRig;

            if (runeRig.HasRigData && runeRig.Object != null && runeRig.Object.IsValid)
            {
                target = NetworkInteractionTarget.CreateRuneNode(runeRig.Object.Id, (byte)runeObject.NodeIndex, hit.point, hit.normal);
                return true;
            }
        }

        GameObject hitObject = SpellSystemHelpers.GetHitGameObject(hit.collider);

        if (hitObject != null && hitObject.TryGetComponent(out InteractableItem interactableItem))
        {
            if (interactableItem.Object == null || !interactableItem.Object.IsValid)
                return false;

            target = NetworkInteractionTarget.CreateItem(interactableItem.Object.Id, hit.point, hit.normal);
            return true;
        }

        Rigidbody targetBody = hit.rigidbody;

        if (targetBody != null && targetBody.TryGetComponent(out NetworkObject bodyObject) && bodyObject.IsValid)
        {
            target = NetworkInteractionTarget.CreatePhysicsBody(bodyObject.Id, hit.point, hit.normal);
            return true;
        }

        target = NetworkInteractionTarget.CreateWorldPoint(hit.point, hit.normal);
        return true;
    }


}
