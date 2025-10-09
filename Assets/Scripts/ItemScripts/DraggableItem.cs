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
    [Networked] public int HolderChangedCount { get; set; } //int that increments everytime someone picks up or drops an item
    [Networked, Capacity(6)] public NetworkArray<PlayerRef> CurrentHoldingPlayers { get; }
        = MakeInitializer(new PlayerRef[] { PlayerRef.None, PlayerRef.None, PlayerRef.None, PlayerRef.None, PlayerRef.None, PlayerRef.None });

    public Vector3[] localGrabPos = new Vector3[6];

    public override void Spawned()
    {
        rb = GetComponent<Rigidbody>();
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        networkedRB = this.GetComponent<NetworkRigidbody3D>();
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(HolderChangedCount):
                    if (!HasStateAuthority)
                        PickUpOrDropItem();
                    break;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        //for each player in the CurrentHoldingPlayers
        //calculate pull from player based on distance from their "target hold point" and add force with a cap. 
        Vector3 forceToAdd = Vector3.zero;
        Vector3 torqueToAdd = Vector3.zero;

        int numberOfHolders = 0;
        for(int i = 0; i < CurrentHoldingPlayers.Length; i++)
        {
            if (!Runner.TryGetPlayerObject(CurrentHoldingPlayers[i], out NetworkObject player)) continue;

            if (player == null) continue;


            numberOfHolders = numberOfHolders+1;
            var controller = player.GetComponent<HybridCharacterController>();
            var handController = player.GetComponent<NetworkedHandsController>();
            Vector3 targetHoldPos = controller.GetEyePos() + (controller.GetLookRot() * (handController.dragTargetOffset + new Vector3(0,0, handController.DragDistance)));



            //position

            Vector3 holdPos = transform.TransformPoint(localGrabPos[i]) != null ? transform.TransformPoint(localGrabPos[i]) :  handController.rightHand.transformNet.transform.position;

            Vector3 positionError = targetHoldPos - transform.position;
            float distance = positionError.magnitude;

            float strengthMultiplier = handController.dragStrengthCurve.Evaluate(Mathf.Clamp01(distance / handController.dragRange));
            Vector3 springForce = positionError * handController.dragStength * strengthMultiplier;
            
            forceToAdd += springForce;




            //rotation

            Vector3 currentDirection = (holdPos - rb.worldCenterOfMass).normalized;

            Vector3 targetDirection = (controller.GetEyePos() - rb.worldCenterOfMass).normalized;

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

        if(numberOfHolders > 0)
        {
            Vector3 damp = rb.linearVelocity * dampening * rb.mass;
            rb.AddForce(forceToAdd - damp, ForceMode.Force);

            Vector3 angularDamp = rb.angularVelocity * rotationalDampening;
            rb.AddTorque(torqueToAdd - angularDamp);
        }

    }


    public void PickUpOrDropItem() //runs for this item on eveyones game
    {
        //loop through each player
        //if the networked array holds this player Id && were not dragging then this player registers they are "dragging" this items so we call PickUpItem locally on everyones game
        //if the networked array dosnt hold this player Id && were are draggin then call drop item 

        // Debug.Log($"picked up or drag called on local player");
        
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
                    DropItem(player);
                    Debug.Log($"Drop called for {player.Id}");
                }
            }
        }


        
    }

    public override void PickUpItem(NetworkObject playerObject)
    {
        //if we have state auth then we increment HolderChangedCount and add the playerObject's localPlayerId to the networkArray

        if (HasStateAuthority) //if we the server
        {
            HolderChangedCount++;
            AddPlayerToCurrentHoldingPlayers(playerObject);
        }

        if(playerObject.TryGetComponent<NetworkedHandsController>(out NetworkedHandsController hands))
        {
            hands.RightHandMode = TargetingMode.DRAGG;
           ///////////////////////////MAKE DRAGGG///////////
        }

        playerObject.GetComponent<NetworkedInventoryManager>().currentItemInHand = this;
        Debug.Log($"picked up draggable item and currentItemInHand is {playerObject.GetComponent<NetworkedInventoryManager>().currentItemInHand.name}");
        //set the player's hand controller to dragging. 

    }

    public override void DropItem(NetworkObject playerObject)
    {
        //if we have state auth then we increment HolderChangedCount and remove the playerObject's localPlayerId from the networkArray
        if (HasStateAuthority) //if we the server
        {
            HolderChangedCount++;
            RemovePlayerFromCurrentHoldingPlayers(playerObject);
        }

        if (playerObject.TryGetComponent<NetworkedHandsController>(out NetworkedHandsController hands))
        {
            hands.RightHandMode = TargetingMode.ARMATURE;
            hands.SetHandTarget_ToArmature(false);
        }

        playerObject.GetComponent<NetworkedInventoryManager>().currentItemInHand = null;
        //set the player's hand controller to armature.
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

            localGrabPos[firstEmptySlot] = transform.InverseTransformPoint(handPos);

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

}
