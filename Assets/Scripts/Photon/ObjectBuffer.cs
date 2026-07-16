using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;

public class ObjectBuffer : NetworkBehaviour
{
    [Header("Buffer Configuration")]
    public NetworkPrefabRef GenericSpellCorePrefab;

    // Hard limit for memory allocation, but we use ActiveCapacity for loops
    public const int MAX_CAPACITY = 128;

    [HideInInspector]
    [Networked] public int ActiveCapacity { get; set; }
    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public NetworkBool IsOrphaned { get; set; }
    [Networked] public int HeadIndex { get; set; }

    [Networked, Capacity(MAX_CAPACITY)]
    private NetworkArray<NetworkObject> _buffer { get; }

    private NetworkObject[] _localBuffer = new NetworkObject[MAX_CAPACITY];
    private readonly HashSet<NetworkObject> _locallyClaimed = new HashSet<NetworkObject>();

    private int _replenishScanIndex = 0;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            for (int i = 0; i < ActiveCapacity; i++)
            {
                if (_buffer[i] == null)
                {
                    _buffer.Set(i, PrepareInstance());
                }
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Drain logic for disconnected players
        if (IsOrphaned)
        {
            // Optional: Add logic here to check if all spells are dead before despawning the buffer
            return;
        }

        // Exact Trickle Replenishment from your working script
        for (int i = 0; i < 10; i++)
        {
            if (_buffer[_replenishScanIndex] == null)
            {
                _buffer.Set(_replenishScanIndex, PrepareInstance());
                break;
            }
            _replenishScanIndex = (_replenishScanIndex + 1) % ActiveCapacity;
        }
    }

    public override void Render()
    {
        for (int i = 0; i < ActiveCapacity; i++)
        {
            var networkInstance = _buffer[i];

            if (_localBuffer[i] != networkInstance)
            {
                if (_localBuffer[i] != null)
                {
                    Reawaken(_localBuffer[i]);
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
                if (networkInstance.gameObject.activeSelf)
                    networkInstance.gameObject.SetActive(false);

                if (networkInstance.IsInSimulation)
                    Runner.SetIsSimulated(networkInstance, false);

                if (networkInstance.transform.parent != this.transform)
                    networkInstance.transform.SetParent(this.transform, false);
                
            }
        }
    }

    private NetworkObject PrepareInstance()
    {
        if (!HasStateAuthority) return null;

        var instance = Runner.Spawn(GenericSpellCorePrefab, new Vector3(0f, -1000f, 0f));

        if (instance.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;

        Runner.SetIsSimulated(instance, false);
        instance.gameObject.SetActive(false);

        // HIERARCHY CLEANUP: Keep the hierarchy clean!
        instance.transform.SetParent(this.transform, false);

        return instance;
    }

    public NetworkObject GetBufferedSpellCore(Vector3 position, Quaternion rotation, out int assignedGlobalIndex)
    {
        int currentIndex = HeadIndex;
        assignedGlobalIndex = currentIndex;
        NetworkObject instance = _buffer[currentIndex];

        // Exact eviction logic from your working script
        if (instance == null)
        {
            instance = PrepareInstance();
        }
        else if (HasStateAuthority)
        {
            _buffer.Set(currentIndex, null);
        }

        // Exact Prediction Head Advancement
        if (HasStateAuthority || (Owner != PlayerRef.None && Owner == Runner.LocalPlayer))
        {
            HeadIndex = (currentIndex + 1) % ActiveCapacity;
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
        else instance.transform.SetPositionAndRotation(position, rotation);
    }

    private void Reawaken(NetworkObject instance)
    {
        if (instance == null) return;
        instance.transform.SetParent(null);
        Runner.SetIsSimulated(instance, true);
        instance.gameObject.SetActive(true);
    }
}