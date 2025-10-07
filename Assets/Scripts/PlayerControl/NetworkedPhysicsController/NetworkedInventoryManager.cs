using Fusion;
using UnityEngine;

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

    public Item currentItemInHand = null;
    private Item potentialItemToPickup = null;

    Quaternion lookRotation;

    [Header("Networking Inputs")]
    [Networked] public int PickUpPressCount { get; set; }
    int lastPickUpCount;
    [Networked] public int DropPressCount { get; set; }
    int lastDropCount;
    [Networked] NetworkButtons Prior_buttons { get; set; }

    public void Start()
    {
        characterController = GetComponent<HybridCharacterController>();
    }

    public override void Spawned()
    {
        lastPickUpCount = PickUpPressCount;
        lastDropCount = DropPressCount;
    }

    public override void FixedUpdateNetwork()
    {
        if (currentItemInHand == null)
        {
            LookForItems();
        }

        if (GetInput(out NetworkInputData data))
        {
            lookRotation = data.lookRotation;

            if (data.buttons.WasPressed(Prior_buttons, EInputButton.PICKUP))
            {
                PickUpPressCount++;
            }
            if (data.buttons.WasReleased(Prior_buttons, EInputButton.DROP))
            {
                DropPressCount++;
            }
            Prior_buttons = data.buttons;
        }
    }

    public void FixedUpdate()
    {
        if (HasStateAuthority)
        {
            if (PickUpPressCount > lastPickUpCount)
            {
                lastPickUpCount++;
                PickupItem();
            }
            if (DropPressCount > lastDropCount)
            {
                lastDropCount++;
                DropItem();

            }
        }
    }

    private void OnDrawGizmos()
    {
        //Gizmos.color = Color.yellow;
        //Gizmos.DrawSphere(characterController.hipsRb.transform.position, pickupRadius);
    }

    private void LookForItems()
    {
        Collider[] nearbyItems = Physics.OverlapSphere(characterController.hipsRb.transform.position, pickupRadius, itemLayer);
        Item bestCandidate = null;
        float bestDot = -1f;

        foreach (var col in nearbyItems)
        {

            Vector3 directionToItem = (col.transform.position - characterController.hipsRb.transform.position).normalized;
            Vector3 forwardDir = lookRotation * Vector3.forward;
            float dot = Vector3.Dot(forwardDir, directionToItem);

            if (dot > Mathf.Cos(pickupAngle * Mathf.Deg2Rad))
            {
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestCandidate = col.GetComponent<Item>();
                    Debug.Log($"looking for selected found this: {col}");
                }
            }
        }

        if (bestCandidate != null && potentialItemToPickup != bestCandidate)
        {
            potentialItemToPickup = bestCandidate;

            handController.SetHandTarget_ToPickUpPoint(false, potentialItemToPickup.primaryHandle, potentialItemToPickup.heldHandState);
            Debug.Log($"looking for selected set the hands to pick up : {potentialItemToPickup.name}");
        }
        else if (bestCandidate == null && potentialItemToPickup != null && currentItemInHand == null)
        {
            handController.SetHandTarget_ToArmature(false);
            potentialItemToPickup = null;
        }
    }

    private void PickupItem() //only runs on state authority
    {
        if (potentialItemToPickup == null) return;

        currentItemInHand = potentialItemToPickup;
        potentialItemToPickup = null;

        //SetNewHoldingPlayer();

        currentItemInHand.PickUpItem(this.GetComponent<NetworkObject>());

        //handController.AttachItemToHand(false, currentItemInHand);
        //handController.SetHandTarget_ToHold(false, currentItemInHand.heldHandState);


        //if (currentItemInHand.secondaryHandle != null)
        //{
        //    handController.SetHandTarget_ToWorldPoint(true, currentItemInHand.secondaryHandle);
        //}

        activeItem = (currentItemInHand.gameObject);
        activeItemIndex = 0;
    }

    private void DropItem() //only runs on state authority
    {
        if (currentItemInHand == null) return;

        Item droppedItem = currentItemInHand;

        currentItemInHand.DropItem(this.GetComponent<NetworkObject>());

        Rigidbody itemRb = droppedItem.GetComponent<Rigidbody>();
        itemRb.linearVelocity = characterController.hipsRb.GetComponent<Rigidbody>().linearVelocity;
        Vector3 forwardDir = lookRotation * Vector3.forward;
        itemRb.AddForce(forwardDir * 5f, ForceMode.Impulse);
        activeItem = null;
    }

    //public void SetNewHoldingPlayer()
    //{
    //    currentItemInHand.HoldingPlayer = this.GetComponent<NetworkObject>();
    //    currentItemInHand.HolderChangedCount++;
    //}

    //public void ClearItemHeldByPlayer()
    //{
    //    currentItemInHand.HoldingPlayer = null;
    //    currentItemInHand.HolderChangedCount++;
    //}
}
