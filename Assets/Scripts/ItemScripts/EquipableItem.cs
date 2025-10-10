using Fusion;
using Fusion.Addons.Physics;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using static Fusion.NetworkBehaviour;
using static UnityEngine.Rendering.VirtualTexturing.Debugging;

public class EquipableItem : InteractableItem
{
    public string itemName;

    public string primarySpellID, secondarySpellID;

    public SpellGraph primaryActionSpell, secondaryActionSpell;

    public HandState heldHandState;

    public Transform primaryHandle, secondaryHandle;

    public NetworkObjectBuffer networkObjectBuffer;

    [Header("Pickup Variables")]
    public Transform itemModelAndChildComponents;

    [HideInInspector] bool isKinematic;
    [HideInInspector] bool collideractive;
    Vector3 secretHiddenSpot = new Vector3(0, 200, 0); //lol dont look here

    //public GameObject hitbox;
    [Header("Hitbox ponts for melee sweep")]
    public Transform weaponBase, weaponEnd;

    public Transform projectileSpawnPoint;

    [Networked] public NetworkObject HoldingPlayer { get; set; }
    public NetworkObject lastHoldingPlayer;

    [Networked] public int HolderChangedCount {get; set; }

    private ChangeDetector _changeDetector;

    public bool updateNextFrame = false;
    public Vector3 throwDir = Vector3.zero;

    int my_player_id { get { return GetComponent<NetworkObject>().InputAuthority.PlayerId; } }

    #region Equipping & Communicating
    int sendingmessageid = 0;
    int receivingmessageid = 0;
    List<byte[]> received_chunks = null;
    public void EquipSpellToPrimary(SpellGraph graph)
    {
        Debug.Log("Equipping spell");
        // Set the spell as primary spell and communicate
        // the changes to all other instances via RPC call.
        SetAndInitialise(graph);
        string json = graph.ToJson();

        int playerid = my_player_id;
        // TODO:
        // Additional cleaning (pre and post) of the JSON
        // to reduce bandwidth usage.
        // Lots of variable names can be replace before,
        // then reintroduced via a mapping table (i.e. name->id).

        // chunk it
        byte[] data = Encoding.UTF8.GetBytes(json);

        // Max payload is 512.
        // The array has 
        int chunkSize = 450; 
        int totalChunks = (data.Length + chunkSize - 1) / chunkSize;

        // increment messageid
        sendingmessageid++;

        // send it
        for (int i = 0; i < totalChunks; i++)
        {
            int size = Mathf.Min(chunkSize, data.Length - (i * chunkSize));
            byte[] chunk = new byte[size];
            Buffer.BlockCopy(data, i * chunkSize, chunk, 0, size);

            // Send via RPC
            RPC_SendJsonChunk(sendingmessageid, playerid,  i, totalChunks, chunk);
        }
    }
    public void EquipSpellToPrimaryFromJSON(string json)
    {
        Debug.Log(json);
        SpellGraph graph = SpellGraph.FromJson(json);
        if(graph != null )
        {
            SetAndInitialise(graph);
        }
    }
    [Rpc(sources: RpcSources.All, targets: RpcTargets.All)]
    public void RPC_SendJsonChunk(int messageId, int player_ref, int chunkIndex, int totalChunks, byte[] chunkData)
    {
        if (my_player_id != player_ref)
            return;

        if (messageId > receivingmessageid)
        {
            receivingmessageid = messageId;
            received_chunks = new List<byte[]>(new byte[totalChunks][]);
        }
        if (messageId != receivingmessageid)
            return;

        received_chunks[chunkIndex] = chunkData;

        // Check if we have all chunks
        if (received_chunks.Count >= totalChunks && received_chunks.All(c => c != null))
        {
            // Recombine, turn into json, then equip locally.
            byte[] fullData = received_chunks.SelectMany(c => c).ToArray();
            string json = System.Text.Encoding.UTF8.GetString(fullData);

            EquipSpellToPrimaryFromJSON(json);
        }
    }
    #endregion

    public void LoadSpells()
    {

        if (!string.IsNullOrEmpty(primarySpellID))
        {
            primaryActionSpell = SpellGraphController.Instance.GetSpellFromAssestsByName(primarySpellID);
        }


        if (!string.IsNullOrEmpty(secondarySpellID))
        {
            //secondaryActionSpell = SpellGraphController.Instance.GetSpellFromAssestsByName(secondarySpellID);
        }
    }

    public void Start()
    {
        LoadSpells();
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        networkedRB = this.GetComponent<NetworkRigidbody3D>();
        networkObjectBuffer = this.GetComponent<NetworkObjectBuffer>();
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(HolderChangedCount):
                    if(!HasStateAuthority)
                        PickUpOrDropItem();
                    break;
            }
        }
        lastHoldingPlayer = HoldingPlayer;
    }

    public void PickUpOrDropItem()
    {
        if(HoldingPlayer != null) //pickup 
        {
            PickUpItem(HoldingPlayer);
        }
        else //drop
        {
            DropItem(lastHoldingPlayer);
        }
    }

    //public override void FixedUpdateNetwork()
    //{
    //    if (updateNextFrame && !HasStateAuthority)
    //        UpdateRBNextFrame();
    //}

    public override void PickUpItem(NetworkObject playerObject)
    {
        
        CasheNetworkedRBSettings();        

        if (playerObject.TryGetComponent(out NetworkedInventoryManager inventory))
        {
            inventory.activeItem = gameObject;
        }

        if (HasStateAuthority)
        {
            HoldingPlayer = playerObject;
            HolderChangedCount++;

            networkedRB.RBIsKinematic = true;
            networkedRB.GetComponent<Collider>().enabled = false;

            networkedRB.Rigidbody.angularVelocity = Vector3.zero; 
            networkedRB.Rigidbody.linearVelocity = Vector3.zero;

            networkedRB.Teleport(secretHiddenSpot, Quaternion.identity);

           
        }

        //Everyone takes the child transform (ie without the networked RB) and puts it in their hand.
        if (playerObject.TryGetComponent(out NetworkedHandsController hands))
        {
            Debug.Log($"Holding player {playerObject} picked up item {this.name}");

            Transform handPalm = hands.rightHand.palmTransform;
            Transform itemHandle = this.primaryHandle;

            

            Quaternion handleRelRot = Quaternion.Inverse(itemModelAndChildComponents.transform.rotation) * itemHandle.rotation;
            Vector3 handleRelPos = Quaternion.Inverse(itemModelAndChildComponents.transform.rotation) * (itemHandle.position - itemModelAndChildComponents.transform.position);
            Quaternion modelRot = (handPalm.rotation * Quaternion.Euler(hands.pickUpItemRotOffset) *  Quaternion.Inverse(handleRelRot));
            Vector3 modelPos = handPalm.position - (modelRot * handleRelPos);

            itemModelAndChildComponents.transform.SetPositionAndRotation(modelPos, modelRot);

            itemModelAndChildComponents.transform.SetParent(handPalm);


            playerObject.GetComponent<NetworkedInventoryManager>().currentItemInHand = this;
            hands.SetHandTarget_ToHold(false, heldHandState);
        }

        if(!HasStateAuthority)
            DisableNetworkedRB();
    }
    
    public override void DropItem(NetworkObject playerObject)
    {

        var characterController = playerObject.GetComponent<HybridCharacterController>();
        var handController = playerObject.GetComponent<NetworkedHandsController>();

        if (playerObject.TryGetComponent(out NetworkedInventoryManager inventory))
        {
            inventory.currentItemInHand = null;
        }


        if (HasStateAuthority)
        {
            HoldingPlayer = null;
            HolderChangedCount++;

            Vector3 dropPosition = itemModelAndChildComponents.transform.position;
            Quaternion dropRotation = itemModelAndChildComponents.rotation;
            
            networkedRB.Teleport(dropPosition, dropRotation);

            networkedRB.RBIsKinematic = false;
            networkedRB.GetComponent<Collider>().enabled = true;

            throwDir = characterController.GetLookRot() * Vector3.forward;
            networkedRB.Rigidbody.AddForce((throwDir * 5f), ForceMode.Impulse);
        }

        itemModelAndChildComponents.SetParent(this.transform);
        itemModelAndChildComponents.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        Debug.Log($"dropped item {this.name}");


        if (!HasStateAuthority)
        {
            updateNextFrame = true;
        }
        RestoreNetworkedPhysicsSettings();
        handController.SetHandTarget_ToArmature(false);
        
    }


    //public void UpdateRBNextFrame()
    //{
    //    RestoreNetworkedPhysicsSettings();
    //    updateNextFrame = false;
    //}


    public void CasheNetworkedRBSettings()
    {
        isKinematic = networkedRB.Rigidbody.isKinematic;
        collideractive = networkedRB.GetComponent<Collider>().enabled;
    }

    public void DisableNetworkedRB()
    {
        networkedRB.Rigidbody.isKinematic = true;
        networkedRB.GetComponent<Collider>().enabled = false;
    }

    public void RestoreNetworkedPhysicsSettings()
    {
        networkedRB.Rigidbody.isKinematic = isKinematic;
        networkedRB.GetComponent<Collider>().enabled = true;
    }


    void SetAndInitialise(SpellGraph graph)
    {
        primaryActionSpell = graph;

        if (networkObjectBuffer != null)
            networkObjectBuffer.Initialise();
    }
}
