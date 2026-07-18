using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class ObjectBufferAllocator : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static ObjectBufferAllocator Instance;

    [Header("Prefabs")]
    public NetworkPrefabRef BufferPrefab; // The empty prefab with ObjectBuffer.cs
    public NetworkPrefabRef GenericSpellCorePrefab;

    [Header("Configurations")]
    public int PlayerBufferCapacity = 32;
    public int EnvironmentBufferCapacity = 32;

    [Networked, Capacity(16)]
    public NetworkDictionary<PlayerRef, NetworkId> PlayerBufferMap { get; }
    public NetworkId EnvironmentBufferId { get; set; }

    private Dictionary<NetworkId, ObjectBuffer> _localBufferCache = new Dictionary<NetworkId, ObjectBuffer>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            var envObj = Runner.Spawn(BufferPrefab, Vector3.zero, Quaternion.identity, null, (runner, obj) =>
            {
                if (obj.TryGetComponent<ObjectBuffer>(out var envBuffer))
                {
                    envBuffer.ActiveCapacity = EnvironmentBufferCapacity;
                    envBuffer.GenericSpellCorePrefab = GenericSpellCorePrefab;
                    envBuffer.Owner = PlayerRef.None;
                    envBuffer.gameObject.name = "Environment_ObjectBuffer";
                }
            });
            EnvironmentBufferId = envObj.Id;
        }
    }

    public void PlayerJoined(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        // Spawn a dedicated buffer for the new player
        var bufferObj = Runner.Spawn(BufferPrefab, Vector3.zero, Quaternion.identity, null, (runner, obj) =>
        {
            if (obj.TryGetComponent<ObjectBuffer>(out var buffer))
            {
                buffer.ActiveCapacity = PlayerBufferCapacity;
                buffer.GenericSpellCorePrefab = GenericSpellCorePrefab;
                buffer.Owner = player;
                buffer.gameObject.name = $"Player_{player.PlayerId}_ObjectBuffer";
            }
        });

        PlayerBufferMap.Add(player, bufferObj.Id);
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        if (PlayerBufferMap.TryGet(player, out NetworkId bufferId))
        {
            if (Runner.TryFindObject(bufferId, out var bufferObj) && bufferObj.TryGetComponent<ObjectBuffer>(out var buffer))
            {
                buffer.IsOrphaned = true;
                buffer.gameObject.name += "_[ORPHANED]";
            }
            PlayerBufferMap.Remove(player);
        }
    }

    public ObjectBuffer GetBufferForCaster(NetworkObject casterSource)
    {
        if (casterSource == null || casterSource.InputAuthority == PlayerRef.None)
        {
            return GetCachedBuffer(EnvironmentBufferId);
        }

        if (PlayerBufferMap.TryGet(casterSource.InputAuthority, out NetworkId bufferId))
        {
            return GetCachedBuffer(bufferId);
        }

        return GetCachedBuffer(EnvironmentBufferId);
    }

    private ObjectBuffer GetCachedBuffer(NetworkId bufferId)
    {
        if (!bufferId.IsValid) return null;

        if (_localBufferCache.TryGetValue(bufferId, out var buffer) && buffer != null)
        {
            return buffer;
        }

        if (Runner.TryFindObject(bufferId, out var netObj))
        {
            if (netObj.TryGetComponent<ObjectBuffer>(out var foundBuffer))
            {
                _localBufferCache[bufferId] = foundBuffer;
                return foundBuffer;
            }
        }

        return null;
    }

 
}