using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using Fusion;

public class InventoryManager : NetworkBehaviour
{
    //public List<GameObject> equippedItems = new List<GameObject>();

    public int activeItemIndex = 0;

    //public GameObject activeItem => equippedItems[activeItemIndex];

    public Transform itemSocketR;

    public GameObject activeItem;

    public Transform snapPoint;

    [SerializeField] private PhysicsHandController handController;
    //[SerializeField] private Transform playerCamera;

    [SerializeField] private float pickupRadius = 3f;
    [SerializeField] private float pickupAngle = 45f;
    [SerializeField] private LayerMask itemLayer;

    private Item currentItemInHand = null;
    private Item potentialItemToPickup = null;

    Quaternion lookRotation;
    int pickupkey_pressed = 0;
    int dropkey_pressed = 0;
    NetworkButtons prior_buttons; 

    private void Awake()
    {
        //playerCamera = Camera.main.transform;
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


    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData data))
        {
            lookRotation = data.lookRotation;

            if (data.buttons.WasPressed(prior_buttons, EInputButton.PICKUP))
            {
                pickupkey_pressed++;
            }
            if (data.buttons.WasReleased(prior_buttons, EInputButton.DROP))
            {
                dropkey_pressed++;
            }
            prior_buttons = data.buttons;
        }
    }

    public void FixedUpdate()
    {
        if (pickupkey_pressed > 0)
        {
            pickupkey_pressed = 0;
            PickupItem();
        }
        else if(dropkey_pressed > 0)
        {
            dropkey_pressed = 0;
            DropItem();
        }
    }

    private void LookForItems()
    {
        // before, it was camera position. For simplicity, put it as transform position,
        // though that will be incorrect since it has a different anchor to the camera.
        Collider[] nearbyItems = Physics.OverlapSphere(transform.position, pickupRadius, itemLayer);
        Item bestCandidate = null;
        float bestDot = -1f;

        foreach (var col in nearbyItems)
        {

            Vector3 directionToItem = (col.transform.position - transform.position).normalized;
            Vector3 forwardDir = lookRotation * Vector3.forward;
            float dot = Vector3.Dot(forwardDir, directionToItem);

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


        //GameObject snapPoint = new GameObject("ItemSnapPoint");
        snapPoint.position = itemHandle.position;
        snapPoint.rotation = itemHandle.rotation;

        // Can set parent, but the parent needs to also be a networkbehaviour.
        // So can't do this "create a temporary snapPoint" thing.
        currentItemInHand.transform.SetParent(snapPoint);
        //snapPoint.SetParent(handProxy);

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

        //Transform snapPoint = currentItemInHand.transform.parent;
        currentItemInHand.transform.SetParent(null);
        //Destroy(snapPoint.gameObject);
        snapPoint.localPosition = Vector3.zero;
        
        Rigidbody itemRb = currentItemInHand.GetComponent<Rigidbody>();
        itemRb.GetComponent<Collider>().enabled = true;
        itemRb.isKinematic = false;

        itemRb.linearVelocity = GetComponent<Rigidbody>().linearVelocity;
        Vector3 forwardDir = lookRotation * Vector3.forward;
        itemRb.AddForce(forwardDir * 5f, ForceMode.Impulse);

        handController.SetHandState(handController.rightHand, handController.defaultHandState);
        currentItemInHand = null;

        activeItem = null;
    }

    // --- Input System Handlers ---
    //public void OnPickup(InputAction.CallbackContext context)
    //{
    //    if (!HasInputAuthority)
    //        return;
    //    if (context.performed && currentItemInHand == null && potentialItemToPickup != null)
    //    {
    //        PickupItem();
    //    }
    //}

    //public void OnDrop(InputAction.CallbackContext context)
    //{
    //    if (!HasInputAuthority)
    //        return;
    //    if (context.performed && currentItemInHand != null)
    //    {
    //        DropItem();
    //    }
    //}


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