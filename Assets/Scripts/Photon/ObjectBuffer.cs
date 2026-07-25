using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ObjectBuffer : NetworkBehaviour
{
    [Header("Buffer Configuration")]
    [FormerlySerializedAs("GenericSpellCorePrefab")]
    public NetworkPrefabRef BufferedPrefab;

    public const int MAX_CAPACITY = 32;

    [HideInInspector]
    [Networked] public int ActiveCapacity { get; set; }
    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public NetworkBool IsOrphaned { get; set; }
    [Networked] public int HeadIndex { get; set; }

    [Networked, Capacity(MAX_CAPACITY)]
    private NetworkArray<NetworkObject> _buffer { get; }

    private readonly NetworkObject[] _localBuffer = new NetworkObject[MAX_CAPACITY];
    private readonly HashSet<NetworkObject> _locallyClaimed = new HashSet<NetworkObject>();
    private int _replenishScanIndex;

    public override void Spawned()
    {
        if (ObjectBufferAllocator.Instance != null)
        {
            transform.SetParent(ObjectBufferAllocator.Instance.transform, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        Runner.SetIsSimulated(Object, true);

        if (!HasStateAuthority)
            return;

        for (int i = 0; i < ActiveCapacity; i++)
        {
            if (_buffer[i] == null)
                _buffer.Set(i, PrepareInstance());
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || IsOrphaned || ActiveCapacity <= 0)
            return;

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
            NetworkObject networkInstance = _buffer[i];

            if (_localBuffer[i] != networkInstance && _localBuffer[i] != null)
            {
                Reawaken(_localBuffer[i]);
                _locallyClaimed.Remove(_localBuffer[i]);
            }

            if (networkInstance != null && _locallyClaimed.Contains(networkInstance))
            {
                _localBuffer[i] = networkInstance;
                continue;
            }

            _localBuffer[i] = networkInstance;

            if (networkInstance == null || !networkInstance.IsValid)
                continue;

            if (networkInstance.gameObject.activeSelf)
                networkInstance.gameObject.SetActive(false);

            if (networkInstance.IsInSimulation)
                Runner.SetIsSimulated(networkInstance, false);

            if (networkInstance.transform.parent != transform)
                networkInstance.transform.SetParent(transform, false);
        }
    }

    public NetworkObject GetBufferedObject(Vector3 position, Quaternion rotation, out int assignedBufferIndex)
    {
        assignedBufferIndex = -1;

        if (ActiveCapacity <= 0)
            return null;

        int currentIndex = HeadIndex;
        assignedBufferIndex = currentIndex;
        NetworkObject instance = _buffer[currentIndex];

        if (instance == null)
        {
            instance = PrepareInstance();
        }
        else if (HasStateAuthority)
        {
            _buffer.Set(currentIndex, null);
        }

        HeadIndex = (currentIndex + 1) % ActiveCapacity;

        if (!HasStateAuthority && instance != null)
            _locallyClaimed.Add(instance);

        ReawakenAndPlace(instance, position, rotation);
        return instance;
    }

    private NetworkObject PrepareInstance()
    {
        if (!HasStateAuthority)
            return null;

        NetworkObject instance = Runner.Spawn(BufferedPrefab, new Vector3(0f, -1000f, 0f));

        if (instance.TryGetComponent(out Rigidbody rb))
            rb.isKinematic = true;

        Runner.SetIsSimulated(instance, false);
        instance.gameObject.SetActive(false);
        instance.transform.SetParent(transform, false);

        return instance;
    }

    private void ReawakenAndPlace(NetworkObject instance, Vector3 position, Quaternion rotation)
    {
        if (instance == null)
            return;

        Reawaken(instance);

        if (instance.TryGetComponent(out Rigidbody unityRigidbody))
        {
            unityRigidbody.linearVelocity = Vector3.zero;
            unityRigidbody.angularVelocity = Vector3.zero;
        }

        if (instance.TryGetComponent(out NetworkRigidbody3D networkRigidbody))
        {
            networkRigidbody.Teleport(position, rotation);
            networkRigidbody.RBIsKinematic = false;
        }
        else if (instance.TryGetComponent(out NetworkTransform networkTransform))
        {
            networkTransform.Teleport(position, rotation);
        }
        else
        {
            instance.transform.SetPositionAndRotation(position, rotation);
        }
    }

    private void Reawaken(NetworkObject instance)
    {
        if (instance == null)
            return;

        instance.transform.SetParent(null);
        Runner.SetIsSimulated(instance, true);
        instance.gameObject.SetActive(true);
    }
}