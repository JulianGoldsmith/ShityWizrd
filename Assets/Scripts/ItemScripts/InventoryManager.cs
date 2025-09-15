using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    //public List<GameObject> equippedItems = new List<GameObject>();

    public int activeItemIndex = 0;

    //public GameObject activeItem => equippedItems[activeItemIndex];

    public Transform itemSocketR;

    public GameObject activeItem;



    [SerializeField] private PhysicsHandController handController;
    [SerializeField] private Transform playerCamera;

    [SerializeField] private float pickupRadius = 3f;
    [SerializeField] private float pickupAngle = 45f;
    [SerializeField] private LayerMask itemLayer;

    private Item currentItemInHand = null;
    private Item potentialItemToPickup = null;

    private void Awake()
    {

    }

    void Start()
    {

    }

    private void Update()
    {
        if (currentItemInHand == null)
        {
            LookForItems();
        }
    }

    private void LookForItems()
    {
        Collider[] nearbyItems = Physics.OverlapSphere(playerCamera.position, pickupRadius, itemLayer);
        Item bestCandidate = null;
        float bestDot = -1f;

        foreach (var col in nearbyItems)
        {

            Vector3 directionToItem = (col.transform.position - playerCamera.position).normalized;
            float dot = Vector3.Dot(playerCamera.forward, directionToItem);

            if (dot > Mathf.Cos(pickupAngle * Mathf.Deg2Rad))
            {
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestCandidate = col.GetComponent<Item>();
                    //Debug.Log($"looking for selected found this: {col}");
                }
            }
        }

        if (bestCandidate != null && potentialItemToPickup != bestCandidate)
        {
            potentialItemToPickup = bestCandidate;

            handController.SetTemporaryTarget(handController.rightHand, potentialItemToPickup.primaryHandle);
            //Debug.Log($"looking for selected set the hands to pick up : {potentialItemToPickup.name}");
        }
        else if (bestCandidate == null && potentialItemToPickup != null)
        {
            handController.ClearTemporaryTarget(handController.rightHand);
            potentialItemToPickup = null;
        }
    }

    private void PickupItem()
    {
        if (potentialItemToPickup == null) return;

        currentItemInHand = potentialItemToPickup;
        potentialItemToPickup = null;

        // Parent the item to the hand proxy and disable its physics
        Rigidbody itemRb = currentItemInHand.GetComponent<Rigidbody>();
        itemRb.isKinematic = true;
        itemRb.interpolation = RigidbodyInterpolation.None;
        itemRb.GetComponent<Collider>().enabled = false;


        Transform handProxy = handController.rightHand.physicsProxy.transform;
        Transform itemHandle = currentItemInHand.primaryHandle;


        GameObject snapPoint = new GameObject("ItemSnapPoint");
        snapPoint.transform.position = itemHandle.position;
        snapPoint.transform.rotation = itemHandle.rotation;

        currentItemInHand.transform.SetParent(snapPoint.transform);

        snapPoint.transform.SetParent(handProxy);

        if (currentItemInHand.secondaryHandle != null)
        {
            handController.LockHandToTarget(handController.leftHand, currentItemInHand.secondaryHandle);
        }

        handController.ClearTemporaryTarget(handController.rightHand);
        handController.SetHandState(handController.rightHand, currentItemInHand.heldHandState);

        activeItem = (currentItemInHand.gameObject);
        activeItemIndex = 0;
    }

    private void DropItem()
    {
        if (currentItemInHand == null) return;

        if (currentItemInHand.secondaryHandle != null)
        {
            handController.ReleaseHand(handController.leftHand);
        }

        Transform snapPoint = currentItemInHand.transform.parent;
        currentItemInHand.transform.SetParent(null);
        Destroy(snapPoint.gameObject);
        
        Rigidbody itemRb = currentItemInHand.GetComponent<Rigidbody>();
        itemRb.GetComponent<Collider>().enabled = true;
        itemRb.isKinematic = false;

        itemRb.linearVelocity = GetComponent<Rigidbody>().linearVelocity;
        itemRb.AddForce(playerCamera.forward * 5f, ForceMode.Impulse);

        handController.SetHandState(handController.rightHand, handController.defaultHandState);
        currentItemInHand = null;

        activeItem = null;
    }

    // --- Input System Handlers ---
    public void OnPickup(InputAction.CallbackContext context)
    {
        if (context.performed && currentItemInHand == null && potentialItemToPickup != null)
        {
            PickupItem();
        }
    }

    public void OnDrop(InputAction.CallbackContext context)
    {
        if (context.performed && currentItemInHand != null)
        {
            DropItem();
        }
    }


    /*
    public void EquipItem(GameObject item, int slot)
    {
        if (slot < 0 || slot >= equippedItems.Count)
            return;

        equippedItems[slot] = item;

       
    }

    public void SwitchItem(int slot)
    {
        if (slot < 0 || slot >= equippedItems.Count)
        {
            Debug.Log("Not item in slot: " + slot);
            return;
        }

        activeItemIndex = slot;

        foreach (var item in equippedItems)
        {
            if (item != null) item.SetActive(false);
        }

        // Activate new
        var newItem = equippedItems[slot];
        if (newItem != null)
        {
            newItem.SetActive(true);
            newItem.transform.SetParent(itemSocketR);
            newItem.transform.localPosition = Vector3.zero;  // Reset position
            newItem.transform.localRotation = Quaternion.identity;  // Reset rotation

        }

        //Debug.Log($"Switched to: {newItem.name}");
    }




    public void OnSlot1(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            SwitchItem(0);
        }
    }

    public void OnSlot2(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            SwitchItem(1);
        }
    }

    public void OnSlot3(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            SwitchItem(2);
        }
    }
    */

}