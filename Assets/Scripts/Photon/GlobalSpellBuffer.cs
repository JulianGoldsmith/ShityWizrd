using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;

public class GlobalSpellBuffer : NetworkBehaviour
{
    public static GlobalSpellBuffer Instance;

    [Header("Buffer Configuration")]
    public NetworkPrefabRef GenericSpellCorePrefab;

    public const int MAX_PLAYERS = 6;
    public const int PLAYER_SLICE_SIZE = 32;
    public const int ENV_SLICE_SIZE = 32;
    public const int TOTAL_CAPACITY = (MAX_PLAYERS * PLAYER_SLICE_SIZE) + ENV_SLICE_SIZE;

    [Networked, Capacity(TOTAL_CAPACITY)]
    private NetworkArray<NetworkObject> _buffer { get; }

    [Networked, Capacity(MAX_PLAYERS + 1)]
    private NetworkArray<int> _sliceHeads { get; }

    [Networked, Capacity(MAX_PLAYERS)]
    private NetworkArray<PlayerRef> _sliceOwners { get; }


    private NetworkObject[] _localBuffer = new NetworkObject[TOTAL_CAPACITY];
    private readonly HashSet<NetworkObject> _locallyClaimed = new HashSet<NetworkObject>();

    // --- FIX 3: Trickle Replenishment Tracker ---
    private int _replenishScanIndex = 0;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            for (int i = 0; i < TOTAL_CAPACITY; i++)
            {
                if (_buffer[i] == null)
                {
                    _buffer.Set(i, PrepareInstance());
                }
            }
        }
    }

    // --- FIX 3 & 4: Trickle Replenishment ---
    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Scan a few slots each tick to find holes. 
        // We only instantiate a MAXIMUM of 1 object per tick to completely eliminate CPU spikes.
        for (int i = 0; i < 10; i++)
        {
            if (_buffer[_replenishScanIndex] == null)
            {
                _buffer.Set(_replenishScanIndex, PrepareInstance());
                break; // Found a hole, patched it. We are done for this tick!
            }
            _replenishScanIndex = (_replenishScanIndex + 1) % TOTAL_CAPACITY;
        }
    }

    public override void Render()
    {
        for (int i = 0; i < TOTAL_CAPACITY; i++)
        {
            var networkInstance = _buffer[i];

            if (_localBuffer[i] != networkInstance)
            {
                if (_localBuffer[i] != null)
                {
                    Reawaken(_localBuffer[i]);

                    // --- FIX 2: Clear the Prediction Leak! ---
                    // If the slot changed, the Host officially woke our predicted object up.
                    _locallyClaimed.Remove(_localBuffer[i]);
                }
            }

            if (networkInstance != null && _locallyClaimed.Contains(networkInstance))
            {
                _localBuffer[i] = networkInstance;
                continue;
            }

            _localBuffer[i] = networkInstance;

            if (networkInstance != null && networkInstance.IsValid)
            {
                // --- FIX 5: Stop touching GameObjects redundantly ---
                if (networkInstance.gameObject.activeSelf)
                    networkInstance.gameObject.SetActive(false);

                if (networkInstance.IsInSimulation)
                    Runner.SetIsSimulated(networkInstance, false);
            }
        }
    }

    private NetworkObject PrepareInstance()
    {
        if (!HasStateAuthority) return null;

        var instance = Runner.Spawn(GenericSpellCorePrefab, new Vector3(0f, -1000f, 0f));

        if (instance.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
        }

        Runner.SetIsSimulated(instance, false);
        instance.gameObject.SetActive(false);

        return instance;
    }

    #region Dynamic Slice Assignment
    public void AssignSliceToPlayer(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            if (_sliceOwners[i] == PlayerRef.None)
            {
                _sliceOwners.Set(i, player);
                _sliceHeads.Set(i, 0);
                Debug.Log($"[Global Buffer] Assigned Dynamic Slice {i} to Player {player.PlayerId}");
                return;
            }
        }
    }

    public void ReleaseSliceFromPlayer(PlayerRef player)
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < MAX_PLAYERS; i++)
        {
            if (_sliceOwners[i] == player)
            {
                _sliceOwners.Set(i, PlayerRef.None);
                Debug.Log($"[Global Buffer] Released Dynamic Slice {i} from Player {player.PlayerId}");
                return;
            }
        }
    }
    #endregion

    #region Fetching Cores
    public NetworkObject GetBufferedSpellCore(PlayerRef caster, Vector3 position, Quaternion rotation, out int assignedGlobalIndex)
    {
        int sliceIndex = -1;
        int sliceSize = PLAYER_SLICE_SIZE;
        int startIndex = 0;

        if (caster != PlayerRef.None)
        {
            for (int i = 0; i < MAX_PLAYERS; i++)
            {
                if (_sliceOwners[i] == caster)
                {
                    sliceIndex = i;
                    startIndex = i * PLAYER_SLICE_SIZE;
                    break;
                }
            }
        }

        if (sliceIndex == -1)
        {
            sliceIndex = MAX_PLAYERS;
            sliceSize = ENV_SLICE_SIZE;
            startIndex = MAX_PLAYERS * PLAYER_SLICE_SIZE;
        }

        int currentHeadOffset = _sliceHeads[sliceIndex];
        int globalIndex = startIndex + currentHeadOffset;
        assignedGlobalIndex = globalIndex;

        NetworkObject instance = _buffer[globalIndex];

        // --- FIX 4: Safety Net for Starvation ---
        // If the player fired faster than our Trickle Replenisher could patch the holes, force a spawn.
        if (instance == null)
        {
            instance = PrepareInstance();
        }
        else if (HasStateAuthority)
        {
            // Evict it from the buffer instantly so the Replenisher knows to patch this hole later
            _buffer.Set(globalIndex, null);
        }

        // --- FIX 1: Client Prediction Head Advancement ---
        if (HasStateAuthority || (caster != PlayerRef.None && caster == Runner.LocalPlayer))
        {
            int nextHeadOffset = (currentHeadOffset + 1) % sliceSize;
            _sliceHeads.Set(sliceIndex, nextHeadOffset);
        }

        if (!HasStateAuthority)
        {
            _locallyClaimed.Add(instance);
        }

        ReawakenAndPlace(instance, position, rotation);

        return instance;
    }

    private void ReawakenAndPlace(NetworkObject instance, Vector3 position, Quaternion rotation)
    {
        if (instance == null) return;

        Reawaken(instance);

        if (instance.TryGetComponent<Rigidbody>(out var rbUnity))
        {
            rbUnity.linearVelocity = Vector3.zero;
            rbUnity.angularVelocity = Vector3.zero;
        }

        if (instance.TryGetComponent<NetworkRigidbody3D>(out var rb))
        {
            rb.Teleport(position, rotation);
            rb.RBIsKinematic = false;
        }
        else if (instance.TryGetComponent<NetworkTransform>(out var nt))
        {
            nt.Teleport(position, rotation);
        }
        else
        {
            instance.transform.SetPositionAndRotation(position, rotation);
        }
    }

    private void Reawaken(NetworkObject instance)
    {
        if (instance == null) return;
        Runner.SetIsSimulated(instance, true);
        instance.gameObject.SetActive(true);
    }
    #endregion
}