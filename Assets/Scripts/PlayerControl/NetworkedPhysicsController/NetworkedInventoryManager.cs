using Fusion;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-10)]
public class NetworkedInventoryManager : NetworkBehaviour
{
    //public List<GameObject> equippedItems = new List<GameObject>();

    public int activeItemIndex = 0;

    //public GameObject activeItem => equippedItems[activeItemIndex];

    public Transform itemSocketR;

    public GameObject activeItem;

    public Transform snapPoint;

    [SerializeField] private NetworkedHandsController handController;
    [SerializeField] private HybridCharacterController characterController;
    //[SerializeField] private Transform playerCamera;

    [SerializeField] private float pickupRadius = 3f;
    [SerializeField] private float pickupAngle = 45f;
    [SerializeField] public LayerMask itemLayer;

    [Networked] public NetworkObject currentItemInHand { get; set; }
    [Networked] public NetworkObject potentialItemToPickup { get; set; }

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
        if (!HasInputAuthority)
            return;
        GameController.Instance.spellGraphController.inventory = this;
    }

    public override void FixedUpdateNetwork()
    {
        if (Object.IsProxy) return;
        if (GetInput(out NetworkInputData data))
        {
            _dragTargetPos = data.dragTargetPos;
            _dragFacingDir = data.dragFacingDir;

            lookRotation = data.lookRotation;

            if (currentItemInHand == null)
            {
                LookForItems();
            }

            if (currentItemInHand == null && data.buttons.WasPressed(Prior_buttons, EInputButton.RIGHT_CLICK) && data.interactionTarget.Type == InteractionTargetType.RuneNode)
            {
                TryDetachRuneFromInput(data.interactionTarget);
            }

            if (data.buttons.WasPressed(Prior_buttons, EInputButton.PICKUP)  )
            {
                if(characterController.bonkController.BonkedState != BONKEDSTATE.BONKED)
                    PickupItem();
            }
            if (data.buttons.WasReleased(Prior_buttons, EInputButton.DROP)  )
            {
                if (characterController.bonkController.BonkedState != BONKEDSTATE.BONKED)
                    DropItem(data);
            }
            Prior_buttons = data.buttons;
        }

        if (characterController.bonkController.BonkedState == BONKEDSTATE.BONKED)
        {
            if (currentItemInHand != null)
            {
                DropItem(default);
            }
            if (potentialItemToPickup != null)
            {
                potentialItemToPickup = null;
            }
            return;
        }

        if (currentItemInHand != null && !currentItemInHand.gameObject.activeInHierarchy)
            currentItemInHand = null;

        
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
        else if (bestCandidate == null && potentialItemToPickup != null && currentItemInHand == null)
        {
            handController.SetHandTarget_ToArmature(false);
            potentialItemToPickup = null;
            currentItemInHand = null;
            handController.leftHand.draggingTransform = null;
            handController.rightHand.draggingTransform = null;
        }
    }

    private void PickupItem()
    {
        if (potentialItemToPickup == null) return;

        currentItemInHand = potentialItemToPickup;
        potentialItemToPickup = null;
        currentItemInHand.GetComponent<InteractableItem>().PickUpItem(this.GetComponent<NetworkObject>());
    }


    private void DropItem(NetworkInputData data)
    {
        if (currentItemInHand == null)
            return;

        NetworkObject droppedObject = currentItemInHand;
        InteractableItem droppedItem = droppedObject.GetComponent<InteractableItem>();

        droppedItem.DropItem(GetComponent<NetworkObject>(), HasInputAuthority, HasStateAuthority);

        if (droppedItem is RuneRigObject runeRig && data.interactionTarget.Type == InteractionTargetType.RuneBay && data.interactionTarget.ObjectId.IsValid)
        {
            runeRig.TryAttachToBay(data.interactionTarget.ObjectId, data.interactionTarget.PartIndex, data.interactionTarget.BayIndex);
        }

        handController.DragDistance = 0;
        currentItemInHand = null;
    }

   

    private void TryDetachRuneFromInput(NetworkInteractionTarget target)
    {
        if (!target.IsValid || target.Type != InteractionTargetType.RuneNode)
            return;

        if (!Runner.TryFindObject(target.ObjectId, out NetworkObject rigObject))
            return;

        if (!rigObject.TryGetComponent(out RuneRigObject runeRig))
            return;

        RuneObject selectedRune = runeRig.GetRuneObject(target.PartIndex);

        if (selectedRune == null)
            return;

        runeRig.TryDetachRune(target.PartIndex, Object.InputAuthority, selectedRune.transform.position, selectedRune.transform.rotation);
    }

    public bool TryGetLookedAtInteractionTarget(out NetworkInteractionTarget target)
    {
        target = default;

        Vector3 origin = characterController.GetEyePos();
        Vector3 direction = characterController.GetLookRot() * Vector3.forward;

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, pickupRadius * 2f, itemLayer))
            return false;

        RuneObject runeObject = hit.collider.GetComponentInParent<RuneObject>();

        if (runeObject != null && runeObject.OwningRig != null)
        {
            RuneRigObject runeRig = runeObject.OwningRig;

            if (runeRig.HasRigData && runeRig.Object != null && runeRig.Object.IsValid)
            {
                target = NetworkInteractionTarget.CreateRuneNode(runeRig.Object.Id, (byte)runeObject.NodeIndex);
                return true;
            }
        }

        GameObject hitObject = SpellSystemHelpers.GetHitGameObject(hit.collider);

        if (hitObject == null || !hitObject.TryGetComponent(out InteractableItem interactableItem))
            return false;

        if (interactableItem.Object == null || !interactableItem.Object.IsValid)
            return false;

        target = NetworkInteractionTarget.CreateItem(interactableItem.Object.Id);
        return true;
    }
}
