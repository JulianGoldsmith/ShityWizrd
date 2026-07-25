using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class ObjectBufferAllocator : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    public static ObjectBufferAllocator Instance;

    [Header("Prefabs")]
    public NetworkPrefabRef BufferPrefab;
    public NetworkPrefabRef GenericSpellCorePrefab;
    public NetworkPrefabRef RuneRigPrefab;

    [Header("Configurations")]
    public int PlayerBufferCapacity = 32;
    public int EnvironmentBufferCapacity = 32;
    public int PlayerRuneRigBufferCapacity = 4;

    [Networked, Capacity(16)]
    public NetworkDictionary<PlayerRef, NetworkId> PlayerBufferMap { get; }

    [Networked, Capacity(16)]
    public NetworkDictionary<PlayerRef, NetworkId> PlayerRuneRigBufferMap { get; }

    [Networked] public NetworkId EnvironmentBufferId { get; set; }

    private readonly Dictionary<NetworkId, ObjectBuffer> _localBufferCache = new Dictionary<NetworkId, ObjectBuffer>();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public override void Spawned()
    {
        if (!HasStateAuthority)
            return;

        NetworkObject environmentBuffer = SpawnBuffer(GenericSpellCorePrefab, EnvironmentBufferCapacity, PlayerRef.None, "Environment_ObjectBuffer");
        EnvironmentBufferId = environmentBuffer.Id;
    }

    public void PlayerJoined(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        NetworkObject spellBuffer = SpawnBuffer(GenericSpellCorePrefab, PlayerBufferCapacity, player, $"Player_{player.PlayerId}_ObjectBuffer");
        PlayerBufferMap.Add(player, spellBuffer.Id);

        NetworkObject runeRigBuffer = SpawnBuffer(RuneRigPrefab, PlayerRuneRigBufferCapacity, player, $"Player_{player.PlayerId}_RuneRigBuffer");
        PlayerRuneRigBufferMap.Add(player, runeRigBuffer.Id);
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority)
            return;

        if (PlayerBufferMap.TryGet(player, out NetworkId spellBufferId))
        {
            MarkBufferOrphaned(spellBufferId);
            PlayerBufferMap.Remove(player);
        }

        if (PlayerRuneRigBufferMap.TryGet(player, out NetworkId runeRigBufferId))
        {
            MarkBufferOrphaned(runeRigBufferId);
            PlayerRuneRigBufferMap.Remove(player);
        }
    }

    public ObjectBuffer GetBufferForCaster(NetworkObject casterSource)
    {
        if (casterSource == null || casterSource.InputAuthority == PlayerRef.None)
            return GetCachedBuffer(EnvironmentBufferId);

        if (PlayerBufferMap.TryGet(casterSource.InputAuthority, out NetworkId bufferId))
            return GetCachedBuffer(bufferId);

        return GetCachedBuffer(EnvironmentBufferId);
    }

    public ObjectBuffer GetRuneRigBuffer(PlayerRef player)
    {
        if (player == PlayerRef.None)
            return null;

        if (PlayerRuneRigBufferMap.TryGet(player, out NetworkId bufferId))
            return GetCachedBuffer(bufferId);

        return null;
    }

    private NetworkObject SpawnBuffer(NetworkPrefabRef bufferedPrefab, int capacity, PlayerRef owner, string objectName)
    {
        return Runner.Spawn(BufferPrefab, Vector3.zero, Quaternion.identity, null, (runner, networkObject) => {
            if (!networkObject.TryGetComponent(out ObjectBuffer buffer))
                return;

            buffer.ActiveCapacity = capacity;
            buffer.BufferedPrefab = bufferedPrefab;
            buffer.Owner = owner;
            buffer.gameObject.name = objectName;
        });
    }

    private void MarkBufferOrphaned(NetworkId bufferId)
    {
        if (!Runner.TryFindObject(bufferId, out NetworkObject bufferObject))
            return;

        if (!bufferObject.TryGetComponent(out ObjectBuffer buffer))
            return;

        buffer.IsOrphaned = true;
        buffer.gameObject.name += "_[ORPHANED]";
    }

    private ObjectBuffer GetCachedBuffer(NetworkId bufferId)
    {
        if (!bufferId.IsValid)
            return null;

        if (_localBufferCache.TryGetValue(bufferId, out ObjectBuffer cachedBuffer) && cachedBuffer != null)
            return cachedBuffer;

        if (!Runner.TryFindObject(bufferId, out NetworkObject networkObject))
            return null;

        if (!networkObject.TryGetComponent(out ObjectBuffer buffer))
            return null;

        _localBufferCache[bufferId] = buffer;
        return buffer;
    }
}