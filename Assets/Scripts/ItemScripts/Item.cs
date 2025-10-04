using Fusion;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using System;

public class Item : NetworkBehaviour
{
    public string itemName;

    public string primarySpellID, secondarySpellID;

    public SpellGraph primaryActionSpell, secondaryActionSpell;

    public HandState heldHandState;

    public Transform primaryHandle, secondaryHandle;

    //public GameObject hitbox;
    [Header("Hitbox ponts for melee sweep")]
    public Transform weaponBase, weaponEnd;

    public Transform projectileSpawnPoint;


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
            primaryActionSpell = SpellGraphController.Instance.GetSpellFromAssestsByName(primarySpellID);
        }


        if (!string.IsNullOrEmpty(secondarySpellID))
        {
            secondaryActionSpell = SpellGraphController.Instance.GetSpellFromAssestsByName(secondarySpellID);
        }
    }

    public void Start()
    {
        LoadSpells();
    }

}
