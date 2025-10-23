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

    private ChangeDetector _changeDetector;
    [Networked, OnChangedRender(nameof(PickUpOrDropItem))] public int HolderChangedCount { get; set; } //int that increments everytime someone picks up or drops an item
    [Networked, Capacity(6)] public NetworkArray<PlayerRef> CurrentHoldingPlayers { get; }
        = MakeInitializer(new PlayerRef[] { PlayerRef.None, PlayerRef.None, PlayerRef.None, PlayerRef.None, PlayerRef.None, PlayerRef.None });

    [Networked, Capacity(6)] public NetworkArray<Vector3> LocalGrabPos { get; } 
        = MakeInitializer(new Vector3[] { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero });


    public bool _localPendingDrop;
    private int _removeIATick = -1;

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody>();
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        networkedRB = this.GetComponent<NetworkRigidbody3D>();
    }

    //public override void Render()
    //{
    //    foreach (var change in _changeDetector.DetectChanges(this))
    //    {
    //        switch (change)
    //        {
    //            case nameof(HolderChangedCount):
    //                if (!HasStateAuthority)
    //                    PickUpOrDropItem();
    //                break;
    //        }
    //    }
    //}

    public override void FixedUpdateNetwork()
    {

        if (HasStateAuthority && _removeIATick > 0 && Runner.Tick >= _removeIATick) //small delay to remove inputAuth
        {
            var no = GetComponent<NetworkObject>();
            if (no.HasInputAuthority)
            {
                no.RemoveInputAuthority();
            }
            _removeIATick = -1;
        }


        Vector3 forceToAdd = Vector3.zero;
        Vector3 torqueToAdd = Vector3.zero;


        int numberOfHolders = 0;
        int lastActivePlayer = -1;
        for(int i = 0; i < CurrentHoldingPlayers.Length; i++)
        {
            if (!Runner.TryGetPlayerObject(CurrentHoldingPlayers[i], out NetworkObject player)) continue;

            if (player == null) continue;

            lastActivePlayer = i;

            /////////////////////new might need to rethinl this////////////////////////////////////////////////////////////////////////////////////
            bool isLocalHolder = (CurrentHoldingPlayers[i] == Runner.LocalPlayer);
            // input auth client: skip the local holder’s force contribution for the drop tick window
            if (_localPendingDrop && HasInputAuthority && isLocalHolder)
            {
                continue;
            }

            //Debug.Log("got to before getting player input");

            if (!Runner.TryGetInputForPlayer(CurrentHoldingPlayers[i], out NetworkInputData holderInput))
                continue; // no input => don't apply spring this tick

            //Debug.Log("got to AFTER getting player input");

            // If not dragging, skip
            if (holderInput.dragTargetPos == Vector3.zero)
                continue;

            Debug.Log("recieving non zero drag Input");

            numberOfHolders = numberOfHolders+1;


            var controller = player.GetComponent<HybridCharacterController>();
            var handController = player.GetComponent<NetworkedHandsController>();


            //Vector3 eyePos = controller.GetEyePos();

            //float pitch = controller.GetLookRot().eulerAngles.x;
            //if (pitch > 180f)
            //{
            //    pitch -= 360f;
            //}
            //pitch = -pitch;
           
            //pitch = (pitch + 90f) / 180f;


            //float addedHeightBasedOnPitch = handController.dragPitchToHeightModifierCurve.Evaluate(pitch);
            //Vector3 targetHoldPos = eyePos + (controller.GetLookRot() * (handController.dragTargetOffset + new Vector3(0, addedHeightBasedOnPitch, handController.DragDistance)));



            ////position

            //Vector3 holdPos = transform.TransformPoint(LocalGrabPos[i]) != null ? transform.TransformPoint(LocalGrabPos[i]) :  handController.rightHand.transformNet.transform.position;

            Vector3 targetHoldPos = holderInput.dragTargetPos;

            Vector3 positionError = targetHoldPos - rb.worldCenterOfMass;
            float distance = positionError.magnitude;

            float strengthMultiplier = handController.dragStrengthCurve.Evaluate(Mathf.Clamp01(distance / handController.dragRange));
            Vector3 springForce = positionError * handController.dragStength * strengthMultiplier;
            
            forceToAdd += springForce;




            //rotation

            //Vector3 currentDirection = (holdPos - rb.worldCenterOfMass).normalized;

            //Vector3 targetDirection = (controller.GetEyePos() - rb.worldCenterOfMass).normalized;

            Vector3 facing = holderInput.dragFacingDir;
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

        
        DetermineInputAuth(numberOfHolders, lastActivePlayer != -1? CurrentHoldingPlayers[lastActivePlayer]: PlayerRef.None);
        

        if(numberOfHolders > 0)
        {
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
        else if(numberOfHolders == 1)
        {
            this.GetComponent<NetworkObject>().AssignInputAuthority(primaryHolder);
        }
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

            this.GetComponent<NetworkObject>().AssignInputAuthority(playerObject.InputAuthority);
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
