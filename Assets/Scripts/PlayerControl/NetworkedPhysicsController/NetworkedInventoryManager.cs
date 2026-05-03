using Fusion;
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

            if (data.buttons.WasPressed(Prior_buttons, EInputButton.PICKUP)  )
            {
                if(characterController.bonkController.BonkedState != BONKEDSTATE.BONKED)
                    PickupItem();
            }
            if (data.buttons.WasReleased(Prior_buttons, EInputButton.DROP)  )
            {
                if (characterController.bonkController.BonkedState != BONKEDSTATE.BONKED)
                    DropItem();
            }
            Prior_buttons = data.buttons;
        }

        if (characterController.bonkController.BonkedState == BONKEDSTATE.BONKED)
        {
            if (currentItemInHand != null)
            {
                DropItem();
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

        if (Physics.Raycast(characterController.GetEyePos(), characterController.GetLookRot() * Vector3.forward, out RaycastHit hit, pickupRadius, itemLayer))
        {
            bestCandidate = hit.collider.GetComponent<InteractableItem>();
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
                    if (dot > bestDot && col.TryGetComponent<InteractableItem>(out InteractableItem newBest))
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

    private void DropItem() 
    {
        if (currentItemInHand == null) return;

        InteractableItem droppedItem = currentItemInHand.GetComponent<InteractableItem>();

        droppedItem.DropItem(this.GetComponent<NetworkObject>(), HasInputAuthority, HasStateAuthority);

        handController.DragDistance = 0;

        currentItemInHand = null;
    }
}
