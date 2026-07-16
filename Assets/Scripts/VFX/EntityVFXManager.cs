using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class EntityVFXManager : NetworkBehaviour
{
    public const int BUFFER_CAPACITY = 12;

    [Tooltip("How far can a predicted impact drift from the server's impact and still be considered the same event? (In meters)")]
    public float SpatialMatchThreshold = 1.5f;

    // THE SCOREBOARD (Server Authority Only)
    [Networked, Capacity(BUFFER_CAPACITY)]
    public NetworkArray<VFXOneShot> NetworkHitBuffer { get; }

    [Networked]
    public int NetworkHitCount { get; set; }

    // THE NOTEBOOK (Local to each client)
    private int _renderedHits = 0;

    // PREDICTION MEMORY: The Proxy's stack of receipts
    private List<PredictionReceipt> _receipts = new List<PredictionReceipt>();

    public void ProcessHit(Vector3 position, Vector3 normal, float radius, VFXTheme theme, VFXTopology top, VFXLifecycle life)
    {
        byte packed = VFXPacker.Pack(theme, top, life);

        // 1. THE HOST: Writes the official truth
        if (HasStateAuthority)
        {
            int index = NetworkHitCount % BUFFER_CAPACITY;
            NetworkHitBuffer.Set(index, new VFXOneShot
            {
                Position = position,
                Normal = normal,
                Scale = radius,
                PackedVFXData = packed
            });
            NetworkHitCount++;
        }
        // 2. THE PROXY / INPUT AUTH: Flawless Zero-Latency Prediction
        else
        {
            // Check our local notebook BEFORE spawning.
            // This completely prevents the multi-frame resimulation echoes!
            if (!HasActiveReceipt(position, packed))
            {
                SpawnLocalVFX(position, normal, radius, theme, top, life);

                _receipts.Add(new PredictionReceipt
                {
                    Position = position,
                    PackedData = packed,
                    Timestamp = Time.time
                });
            }
        }
    }

    private bool HasActiveReceipt(Vector3 position, byte packedData)
    {
        float thresholdSq = SpatialMatchThreshold * SpatialMatchThreshold;
        foreach (var receipt in _receipts)
        {
            if (receipt.PackedData == packedData && (receipt.Position - position).sqrMagnitude <= thresholdSq)
            {
                return true;
            }
        }
        return false;
    }

    public override void Render()
    {
        // Rollback catch
        if (NetworkHitCount < _renderedHits) _renderedHits = NetworkHitCount;

        // Catch up to the Server Scoreboard
        while (_renderedHits < NetworkHitCount)
        {
            int index = _renderedHits % BUFFER_CAPACITY;
            VFXOneShot officialEvent = NetworkHitBuffer[index];

            // Does this server event match a receipt we already processed?
            if (!ConsumeMatchingReceipt(officialEvent))
            {
                // We missed it! Spawn the official visual now.
                VFXPacker.Unpack(officialEvent.PackedVFXData, out VFXTheme t, out VFXTopology p, out VFXLifecycle l);
                SpawnLocalVFX(officialEvent.Position, officialEvent.Normal, officialEvent.Scale, t, p, l);
            }

            _renderedHits++;
        }

        CleanupOldReceipts();
    }

    private bool ConsumeMatchingReceipt(VFXOneShot officialEvent)
    {
        // Using sqrMagnitude is significantly faster than Vector3.Distance
        float thresholdSq = SpatialMatchThreshold * SpatialMatchThreshold;

        for (int i = 0; i < _receipts.Count; i++)
        {
            var receipt = _receipts[i];

            // It must be the exact same type of explosion
            if (receipt.PackedData == officialEvent.PackedVFXData)
            {
                // It must be within our fuzzy spatial threshold
                if ((receipt.Position - officialEvent.Position).sqrMagnitude <= thresholdSq)
                {
                    // Match found! Tear up the receipt and report success.
                    _receipts.RemoveAt(i);
                    return true;
                }
            }
        }
        return false;
    }

    private void CleanupOldReceipts()
    {
        float currentTime = Time.time;
        // Iterate backwards when removing from a list
        for (int i = _receipts.Count - 1; i >= 0; i--)
        {
            // If the server hasn't confirmed this prediction after 2 seconds, 
            // it was a phantom hit. Throw away the receipt.
            if (currentTime - _receipts[i].Timestamp > 2.0f)
            {
                _receipts.RemoveAt(i);
            }
        }
    }

    private void SpawnLocalVFX(Vector3 pos, Vector3 norm, float scale, VFXTheme theme, VFXTopology top, VFXLifecycle life)
    {
        var vfxData = VFXRegistry.GetVFX(theme, top, life);

        if (vfxData.prefab != null)
        {
            GameObject newVfx = Instantiate(vfxData.prefab, pos, Quaternion.LookRotation(norm), null);
            if (newVfx.TryGetComponent<SpellVFX>(out var vfxController))
            {
                vfxController.Initialize(vfxData.tint);
                vfxController.UpdateSpatialData(scale, 1.0f, pos, pos);
            }
        }
    }
}


public static class VFXPacker
{
    public static byte Pack(VFXTheme theme, VFXTopology topology, VFXLifecycle lifecycle)
    {
        int t = (int)theme & 0x07;
        int p = ((int)topology & 0x03) << 3;
        int l = ((int)lifecycle & 0x01) << 5;
        return (byte)(t | p | l);
    }

    public static void Unpack(byte packed, out VFXTheme theme, out VFXTopology topology, out VFXLifecycle lifecycle)
    {
        theme = (VFXTheme)(packed & 0x07);
        topology = (VFXTopology)((packed >> 3) & 0x03);
        lifecycle = (VFXLifecycle)((packed >> 5) & 0x01);
    }
}

[System.Serializable]
public struct VFXOneShot : INetworkStruct
{
    public byte PackedVFXData;
    public float Scale;
    public Vector3 Position;
    public Vector3 Normal;
}

public struct PredictionReceipt
{
    public Vector3 Position;
    public byte PackedData;
    public float Timestamp; // Used to cull old receipts
}