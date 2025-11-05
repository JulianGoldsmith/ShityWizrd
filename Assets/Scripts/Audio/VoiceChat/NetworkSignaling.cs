using UnityEngine;
using Fusion;
using System;
using Unity.WebRTC;
using System.Text;
using System.Collections.Generic;
using System.Linq;

public class NetworkSignaling : NetworkBehaviour
{
    // We use Photon Fusion to set up the SDP offer
    // for WebRTC (the handshake). Once the p2p connection
    // is established, we can then send audio "directly".
    // This is separated out from the WebRTCConnector
    // code for cleanliness.

    // Note that this implementation only uses STUN servers.
    // If players might be behind NATs or firewalls, then
    // we might need a TURN server, which usually requires
    // hosting or third-parties.
    // STUN servers are free, however. (stun.l.google.com:19302)
    // According to chatgpt:
    //  Around 85–90% of P2P connections succeed with STUN only.
    //  The remaining 10–15% require a TURN relay.
    //  So if you want reliable voice chat, you should plan to have a TURN fallback.
    // So we could try relay on STUN, but have a backup TURN server,
    // which should be cheap anyway since few people should
    // have to use it (?).

    // Other option is use steamworks. They seem to have voice chat.

    public static NetworkSignaling instance;

    public WebRTCConnector connector;

    private void Awake()
    {
        instance = this;
    }


    int sendingofferid = 0;
    int receivingofferid = 0;
    List<byte[]> received_offer_chunks = null;
    public void SendOffer(PlayerRef target, RTCSessionDescription offer)
    {

        // need to send sdp in chunks, unfortunately.
        // I tried and the RPC was 1920 bytes when an RPC can only send 512.
        
        // chunk it
        string sdp = offer.sdp;
        byte[] data = Encoding.UTF8.GetBytes(sdp);

        // Max payload is 512.
        // The array has 
        int chunkSize = 450;
        int totalChunks = (data.Length + chunkSize - 1) / chunkSize;

        // increment messageid
        sendingofferid++;

        // send it
        for (int i = 0; i < totalChunks; i++)
        {
            int size = Mathf.Min(chunkSize, data.Length - (i * chunkSize));
            byte[] chunk = new byte[size];
            Buffer.BlockCopy(data, i * chunkSize, chunk, 0, size);

            // Send via RPC
            RPC_SendOffer_Chunk(sendingofferid, Runner.LocalPlayer, target, i, totalChunks, chunk);
        }
    }


    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendOffer_Chunk(int offerid, PlayerRef source, PlayerRef target, int chunkIndex, int totalChunks, byte[] chunkData)
    {
        if (Runner.LocalPlayer != target)
            return;

        if (offerid > receivingofferid)
        {
            receivingofferid = offerid;
            received_offer_chunks= new List<byte[]>(new byte[totalChunks][]);
        }
        if (offerid != receivingofferid)
            return;
        
        received_offer_chunks[chunkIndex] = chunkData;

        // Check if we have all chunks
        if (received_offer_chunks.Count >= totalChunks && received_offer_chunks.All(c => c != null))
        {
            // Recombine, turn into json, then equip locally.
            byte[] fullData = received_offer_chunks.SelectMany(c => c).ToArray();
            string sdp = Encoding.UTF8.GetString(fullData);

            // Received the offer
            StartCoroutine(connector.OnReceivedOffer(source, sdp));
        }
    }



    int sendinganswerid = 0;
    int receivinganswerid = 0;
    List<byte[]> received_answer_chunks = null;
    public void SendAnswer(PlayerRef target, RTCSessionDescription answer)
    {
        // chunk it
        string sdp = answer.sdp;
        byte[] data = Encoding.UTF8.GetBytes(sdp);

        // Max payload is 512.
        // The array has 
        int chunkSize = 450;
        int totalChunks = (data.Length + chunkSize - 1) / chunkSize;

        // increment messageid
        sendinganswerid++;

        // send it
        for (int i = 0; i < totalChunks; i++)
        {
            int size = Mathf.Min(chunkSize, data.Length - (i * chunkSize));
            byte[] chunk = new byte[size];
            Buffer.BlockCopy(data, i * chunkSize, chunk, 0, size);

            // Send via RPC
            RPC_SendAnswer_Chunk(sendinganswerid, Runner.LocalPlayer, target, i, totalChunks, chunk);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendAnswer_Chunk(int answerid, PlayerRef source, PlayerRef target, int chunkIndex, int totalChunks, byte[] chunkData)
    {
        if (Runner.LocalPlayer != target)
            return;

        if (answerid > receivinganswerid)
        {
            receivinganswerid = answerid;
            received_answer_chunks = new List<byte[]>(new byte[totalChunks][]);
        }
        if (answerid != receivinganswerid)
            return;

        received_answer_chunks[chunkIndex] = chunkData;

        // Check if we have all chunks
        if (received_answer_chunks.Count >= totalChunks && received_answer_chunks.All(c => c != null))
        {
            byte[] fullData = received_answer_chunks.SelectMany(c => c).ToArray();
            string sdp = Encoding.UTF8.GetString(fullData);

            // Received the answer
            StartCoroutine(connector.OnReceivedAnswer(source, sdp));
        }
    }

    public void SendIceCandidate(PlayerRef target, RTCIceCandidate candidate)
    {
        RPC_SendIceCandidate(target, candidate.Candidate, candidate.SdpMid, candidate.SdpMLineIndex??0);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_SendIceCandidate(PlayerRef target, string candidate, string sdpmid, int sdpmlineindex, RpcInfo info = default)
    {
        if (target == Runner.LocalPlayer)
        {
            connector.AddIceCandidate(info.Source, candidate, sdpmid, sdpmlineindex);
        }
    }
}
