using Fusion;
using UnityEngine;

public struct CoreContext : INetworkStruct
{
    public Vector3 SpawnPosition;
    public float CastChargeLevel;
    public NetworkId OriginalCaster;

    public NetworkId BufferSourceID;

    public NetworkId CurrentTarget;
    public Vector3 TriggerVector { get; set; }
}