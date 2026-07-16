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

    private Dictionary<PlayerRef, ObjectBuffer> _playerBuffers = new Dictionary<PlayerRef, ObjectBuffer>();
    public ObjectBuffer EnvironmentBuffer { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            // Spawn the Environment Buffer
            Runner.Spawn(BufferPrefab, Vector3.zero, Quaternion.identity, null, (runner, obj) =>
            {
                if (obj.TryGetComponent<ObjectBuffer>(out var envBuffer))
                {
                    envBuffer.ActiveCapacity = EnvironmentBufferCapacity;
                    envBuffer.GenericSpellCorePrefab = GenericSpellCorePrefab;
                    envBuffer.Owner = PlayerRef.None;
                    EnvironmentBuffer = envBuffer;
                    envBuffer.gameObject.name = "Environment_ObjectBuffer";
                }
            });
        }
    }

    public void PlayerJoined(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        // Spawn a dedicated buffer for the new player
        Runner.Spawn(BufferPrefab, Vector3.zero, Quaternion.identity, null, (runner, obj) =>
        {
            if (obj.TryGetComponent<ObjectBuffer>(out var buffer))
            {
                buffer.ActiveCapacity = PlayerBufferCapacity;
                buffer.GenericSpellCorePrefab = GenericSpellCorePrefab;
                buffer.Owner = player;
                buffer.gameObject.name = $"Player_{player.PlayerId}_ObjectBuffer";

                _playerBuffers.Add(player, buffer);
            }
        });
    }

    public void PlayerLeft(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        if (_playerBuffers.TryGetValue(player, out var buffer))
        {
            buffer.IsOrphaned = true;
            buffer.gameObject.name += "_[ORPHANED]";
            _playerBuffers.Remove(player);
        }
    }

    public ObjectBuffer GetBufferForCaster(NetworkObject casterSource)
    {
        if (casterSource == null) return EnvironmentBuffer;

        if (casterSource.InputAuthority != PlayerRef.None)
        {
            if (_playerBuffers.TryGetValue(casterSource.InputAuthority, out var playerBuffer))
            {
                return playerBuffer;
            }
        }
        return EnvironmentBuffer;
    }
}