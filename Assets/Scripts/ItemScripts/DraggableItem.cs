using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;
using UnityEngine.Analytics;

[RequireComponent(typeof(NetworkRigidbody3D))]
public class DraggableItem : InteractableItem
{
    [HideInInspector]
    public Rigidbody rb;
    public float dampening = 10f;
    public float rotationalDampening = 0.1f;

    [Networked, OnChangedRender(nameof(PickUpOrDropItem))] public int HolderChangedCount { get; set; } //int that increments everytime someone picks up or drops an item
    [Networked, Capacity(6)] public NetworkArray<PlayerRef> CurrentHoldingPlayers { get; }
        = MakeInitializer(new PlayerRef[] { PlayerRef.None, PlayerRef.None, PlayerRef.None, PlayerRef.None, PlayerRef.None, PlayerRef.None });

    [Networked, Capacity(6)] public NetworkArray<Vector3> LocalGrabPos { get; } 
        = MakeInitializer(new Vector3[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero });


    public bool _localPendingDrop;
    private int _removeIATick = -1;

    public bool isCharacterObject = false;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody>();
        networkedRB = this.GetComponent<NetworkRigidbody3D>();
        Runner.SetIsSimulated(this.Object, true);
    }

    public override void FixedUpdateNetwork()
    {

        Vector3 forceToAdd = Vector3.zero;
        Vector3 torqueToAdd = Vector3.zero;

        int numberOfHolders = 0;
        int lastActivePlayer = -1;
        for(int i = 0; i < CurrentHoldingPlayers.Length; i++)
        {
            PlayerRef playerRef = CurrentHoldingPlayers[i];

            if (playerRef == PlayerRef.None)
            {
                continue;
            }

            if (!Runner.TryGetPlayerObject(playerRef, out NetworkObject player) || player == null)
            {
                continue;
            }

            lastActivePlayer = i;

            bool isLocalHolder = (CurrentHoldingPlayers[i] == Runner.LocalPlayer);

            if (_localPendingDrop && HasInputAuthority && isLocalHolder)
            {
                continue;
            }

            if(!player.TryGetComponent(out NetworkedInventoryManager inv)){
                continue;
            }

            if (inv._dragTargetPos == Vector3.zero)
            {
                continue;
            }

            numberOfHolders = numberOfHolders+1;

            var controller = player.GetComponent<HybridCharacterController>();
            var handController = player.GetComponent<NetworkedHandsController>();

            Vector3 targetHoldPos = inv._dragTargetPos;

            Vector3 positionError = targetHoldPos - rb.worldCenterOfMass;
            float distance = positionError.magnitude;

            float strengthMultiplier = handController.dragStrengthCurve.Evaluate(Mathf.Clamp01(distance / handController.dragRange));
            Vector3 springForce = positionError * handController.dragStength * strengthMultiplier;

            Vector3 actualForceOnObject = Vector3.ClampMagnitude(springForce, handController.maxDragStrength);

            forceToAdd += actualForceOnObject;

            //float targetDistanceToPlayer = -(targetHoldPos - handController.rightHand.shoulderTransform.position).magnitude;
            //float actualDistanceToPlayer = -(rb.worldCenterOfMass - handController.rightHand.shoulderTransform.position).magnitude;
            //Vector3 tensionForce = (handController.rightHand.shoulderTransform.position - rb.worldCenterOfMass) * (actualDistanceToPlayer - targetDistanceToPlayer);

            //controller.pdBones[0].childRigidbody.AddForceAtPosition(tensionForce * actualForceOnObject.magnitude / 5f, handController.rightHand.shoulderTransform.position, ForceMode.Acceleration);

            //if (actualForceOnObject.magnitude > handController.maxDragStrength * handController.maxStableStrengthRatio)
            //{
            //    Vector3 forceOnPlayer = -(actualForceOnObject.magnitude - (handController.maxDragStrength * handController.maxStableStrengthRatio)) * actualForceOnObject.normalized;
            //    controller.pdBones[0].childRigidbody.AddForceAtPosition((forceOnPlayer / controller.pdBones[0].childRigidbody.mass) / 2f, transform.position, ForceMode.Acceleration);
            //}

            Vector3 forceOnPlayer = -actualForceOnObject / (controller.totalMass);
            controller.hipsRb.AddForce(forceOnPlayer, ForceMode.Acceleration);

            //rotation

            Vector3 facing = inv._dragFacingDir;
            if (facing.sqrMagnitude > 0.001f)
            {
                Vector3 currentDirection = (transform.TransformPoint(LocalGrabPos[i]) - rb.worldCenterOfMass).normalized;
                Vector3 targetDirection = facing.normalized;

                if (currentDirection.sqrMagnitude > 0.01f && targetDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion rotationError = Quaternion.FromToRotation(currentDirection, targetDirection);

                    rotationError.ToAngleAxis(out float angleInDeg, out Vector3 axis);
                    if (angleInDeg > 180) angleInDeg -= 360;
                    float normalizedAngleError = Mathf.Clamp01(Mathf.Abs(angleInDeg) / 180);

                    float rotationMultiplier = handController.dragRotationStrengthCurve.Evaluate(normalizedAngleError);

                    Vector3 rotationalSpringForce = axis * (angleInDeg * Mathf.Deg2Rad) * handController.dragRotationalStrength * rotationMultiplier;
                    torqueToAdd += rotationalSpringForce;
                }
            }
        }
        

        if(numberOfHolders > 0)
        {
            Debug.Log($"{numberOfHolders} Holders of {this.name}");
            Vector3 damp = rb.linearVelocity * dampening * rb.mass;
            rb.AddForce(forceToAdd - damp, ForceMode.Force);

            Vector3 angularDamp = rb.angularVelocity * rotationalDampening;
            rb.AddTorque(torqueToAdd - angularDamp);
        }

    }


    void DetermineInputAuth(int numberOfHolders, PlayerRef primaryHolder)
    {
        if(numberOfHolders > 1)
        {
            this.GetComponent<NetworkObject>().RemoveInputAuthority();
        }
        else if(numberOfHolders == 1 && !isCharacterObject)
        {
            this.GetComponent<NetworkObject>().AssignInputAuthority(primaryHolder);
        }
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

    public void PickUpOrDropItem() //runs for this item on eveyones game
    {
        //loop through each player
        //if the networked array holds this player Id && were not dragging then this player registers they are "dragging" this items so we call PickUpItem locally on everyones game
        //if the networked array dosnt hold this player Id && were are draggin then call drop item 
        if (_localPendingDrop) _localPendingDrop = false;
        // Debug.Log($"picked up or drag called on local player");
        if (HasStateAuthority) return;
        if (HasInputAuthority) return;
        if (IsProxy) return; 
        
        //after looking at this again i think this dosnt need to run at all as the state is now networked i think ive over complicated things

        foreach (PlayerRef p in Runner.ActivePlayers)
        {
            //Debug.Log($"looping through active players");
            

            bool holding = false;
            foreach (PlayerRef playerCurrentlyHolding in CurrentHoldingPlayers)
            {
                //  Debug.Log($"Id for player and holding player {p.PlayerId} and  {playerCurrentlyHolding.PlayerId}");
                //Debug.Log($"looping playerCurrentlyHolding found {playerCurrentlyHolding.PlayerId}");
                if (p == playerCurrentlyHolding)
                {
                    //Debug.Log("Id match");
                    holding = true; 
                }
            }

            if (!Runner.TryGetPlayerObject(p, out var player)) continue;

            Debug.Log($"pickup called for {player.Id}");

            if (holding)
            {
                if(player.GetComponent<NetworkedInventoryManager>().currentItemInHand == null)
                {
                    PickUpItem(player);
                    Debug.Log($"pickup called for {player.Id}");
                }
            }
            else
            {
                if (player.GetComponent<NetworkedInventoryManager>().currentItemInHand != null)
                {
                    DropItem(player, player.HasInputAuthority, player.HasStateAuthority);
                    Debug.Log($"Drop called for {player.Id}");
                }
            }
        }
    }

    public override void PickUpItem(NetworkObject playerObject)
    {
        //if we have state auth then we increment HolderChangedCount and add the playerObject's localPlayerId to the networkArray

        if (true) //if we the server
        {
            HolderChangedCount++;
            AddPlayerToCurrentHoldingPlayers(playerObject);
            if (playerObject.TryGetComponent<NetworkedHandsController>(out NetworkedHandsController hands))
            {
                hands.RightHandMode = TargetingMode.DRAGG;
                ///////////////////////////MAKE DRAGGG///////////
            }
            playerObject.GetComponent<NetworkedInventoryManager>().currentItemInHand = this.GetComponent<NetworkObject>();
            Debug.Log($"picked up draggable item and currentItemInHand is {playerObject.GetComponent<NetworkedInventoryManager>().currentItemInHand.name}");
            //set the player's hand controller to dragging. 

            //if(!isCharacterObject)
            //    this.GetComponent<NetworkObject>().AssignInputAuthority(playerObject.InputAuthority);
        }
    }

    public override void DropItem(NetworkObject playerObject, bool playerHasInputAuthority, bool playerHasStateAuthority) //note playerHasInputAut is different to this object
    {
        if (playerHasInputAuthority)
        {
            LocalImmediateDrop(playerObject);
        }

        if (HasStateAuthority) //if we the server
        {
            HolderChangedCount++;
            RemovePlayerFromCurrentHoldingPlayers(playerObject);

            if (playerObject.TryGetComponent<NetworkedHandsController>(out NetworkedHandsController hands))
            {
                hands.RightHandMode = TargetingMode.ARMATURE;
                hands.SetHandTarget_ToArmature(false);
            }

            playerObject.GetComponent<NetworkedInventoryManager>().currentItemInHand = null;

            _removeIATick = Runner.Tick + 1;
        }
    }

    public void AddPlayerToCurrentHoldingPlayers(NetworkObject _playerObjectToAdd)
    {
        PlayerRef playerObjectToAdd = _playerObjectToAdd.InputAuthority;

        int firstEmptySlot = -1;

        for (int i = 0; i < CurrentHoldingPlayers.Length; i++ )
        {
            if (CurrentHoldingPlayers[i] == playerObjectToAdd)
            {
                Debug.LogWarning($"Player {playerObjectToAdd.PlayerId} is already in the CurrentHoldingPlayers array. No action taken.");
                return;
            }
            if (firstEmptySlot == -1 && CurrentHoldingPlayers[i] == PlayerRef.None)
            {
                firstEmptySlot = i;
            }
        }

        if (firstEmptySlot != -1)
        {
            // Use the index of the first empty slot we found.
            CurrentHoldingPlayers.Set(firstEmptySlot, playerObjectToAdd);

            var handPos = _playerObjectToAdd.GetComponent<NetworkedHandsController>().rightHand.transformNet.transform.position;

            transform.InverseTransformPoint(handPos);

            LocalGrabPos.Set(firstEmptySlot, transform.InverseTransformPoint(handPos));

            Debug.Log($"Added player {playerObjectToAdd.PlayerId} to slot {firstEmptySlot} of draggable item '{this.name}'.");
        }
        else
        {
            // If firstEmptySlot is still -1, the array is full.
            Debug.LogWarning($"Could not add player {playerObjectToAdd.PlayerId}, the CurrentHoldingPlayers array for '{this.name}' is full.");
        }
    }

    public void RemovePlayerFromCurrentHoldingPlayers(NetworkObject _playerObjectToRemove)
    {
        PlayerRef playerObjectToRemove = _playerObjectToRemove.InputAuthority;

        for (int i = 0; i < CurrentHoldingPlayers.Length; i++)
        {
            if (CurrentHoldingPlayers[i] == playerObjectToRemove)
            {
                CurrentHoldingPlayers.Set(i, PlayerRef.None);

                Debug.Log($"Removed player {playerObjectToRemove.PlayerId} from slot {i} of draggin items for {this.name}.");
                return;
            }
        }
    }




    public void LocalImmediateDrop(NetworkObject playerObject)
    {
        _localPendingDrop = true;

        if (playerObject.TryGetComponent<NetworkedHandsController>(out var hands))
        {
            hands.RightHandMode = TargetingMode.ARMATURE;
            hands.SetHandTarget_ToArmature(false);
        }
    }
}
