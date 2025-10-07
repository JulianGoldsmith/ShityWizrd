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

public class Item : NetworkBehaviour
{
    public string itemName;

    public string primarySpellID, secondarySpellID;

    public SpellGraph primaryActionSpell, secondaryActionSpell;

    public HandState heldHandState;

    public Transform primaryHandle, secondaryHandle;

    [Header("Pickup Variables")]
    public NetworkRigidbody3D networkedRB;

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

    #region Equipping & Communicating
    int sendingmessageid = 0;
    int receivingmessageid = 0;
    List<byte[]> received_chunks = null;
    public void EquipSpellToPrimary(SpellGraph graph)
    {
        Debug.Log("Equipping spell");
        // Set the spell as primary spell and communicate
        // the changes to all other instances via RPC call.
        primaryActionSpell = graph;
        string json = graph.ToJson();

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
            RPC_SendJsonChunk(sendingmessageid, i, totalChunks, chunk);
        }
    }
    public void EquipSpellToPrimaryFromJSON(string json)
    {
        Debug.Log(json);
        SpellGraph graph = SpellGraph.FromJson(json);
        if(graph != null )
        {
            primaryActionSpell = graph;
        }
    }
    [Rpc(sources: RpcSources.All, targets: RpcTargets.All)]
    public void RPC_SendJsonChunk(int messageId, int chunkIndex, int totalChunks, byte[] chunkData)
    {
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
            //primaryActionSpell = SpellGraphController.Instance.GetSpellFromAssestsByName(primarySpellID);
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

    public void PickUpItem(NetworkObject playerObject)
    {
        
        CasheNetworkedRBSettings();
        //If we have state authority then we should disable the rb (isKinematic) and teleport it to 000 and unparent it so it stops transmitting move
        this.transform.parent = null;

        if (playerObject.TryGetComponent(out NetworkedInventoryManager inventory))
        {
            inventory.activeItem = gameObject;
        }

        if (HasStateAuthority)
        {
            HoldingPlayer = playerObject;
            HolderChangedCount++;
            networkedRB.RBIsKinematic = true;
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
            Quaternion modelRot = (handPalm.rotation * Quaternion.Euler(hands.pickUpRotOffset) *  Quaternion.Inverse(handleRelRot));
            Vector3 modelPos = handPalm.position - (modelRot * handleRelPos);

            itemModelAndChildComponents.transform.SetPositionAndRotation(modelPos, modelRot);

            itemModelAndChildComponents.transform.SetParent(handPalm);

            //Quaternion handleRotationOffset = Quaternion.Inverse(itemHandle.rotation) * transform.rotation;
            //itemModelAndChildComponents.transform.rotation = handPalm.rotation * handleRotationOffset;

            //itemModelAndChildComponents.transform.localPosition = (transform.position - itemHandle.position);


            playerObject.GetComponent<NetworkedInventoryManager>().currentItemInHand = this;
            hands.SetHandTarget_ToHold(false, heldHandState);
        }

        DisableNetworkedRB();
    }
    
    public void DropItem(NetworkObject playerObject)
    {
        if (playerObject.TryGetComponent(out NetworkedInventoryManager inventory))
        {
            inventory.activeItem = null;
        }

        if (HasStateAuthority)
        {
            HoldingPlayer = null;
            HolderChangedCount++;
            networkedRB.RBIsKinematic = false;
            networkedRB.Teleport(playerObject.GetComponent<NetworkedHandsController>().rightHand.transform.position, itemModelAndChildComponents.rotation);
            Debug.Log($"Item {this.name} at position {transform.position}, item pos on drop {itemModelAndChildComponents.position}");
            //transform.position = itemModelAndChildComponents.position; //had some strange thing where it wasnt moving so added this. 
        }

        Debug.Log($"dropped item {this.name}");

        itemModelAndChildComponents.SetParent(this.transform);

        transform.SetParent(null);

        itemModelAndChildComponents.localPosition = Vector3.zero;
        itemModelAndChildComponents.localRotation = Quaternion.identity;

        if (playerObject != null)
        {
            playerObject.GetComponent<NetworkedInventoryManager>().currentItemInHand = null;
            playerObject.GetComponent<NetworkedHandsController>().SetHandTarget_ToArmature(false);
        }

        RestoreNetworkedPhysicsSettings();

    }

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
        networkedRB.GetComponent<Collider>().enabled = collideractive;
    }
}
