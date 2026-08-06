using Fusion;
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

    private int _replenishScanIndex;
    private int _orphanCleanupIndex;

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
        ReconcileBufferedObjectParents();

        if (!HasStateAuthority)
            return;

        if (IsOrphaned)
        {
            CleanUpOrphanedBuffer();
            return;
        }

        if (ActiveCapacity <= 0)
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

    private void ReconcileBufferedObjectParents()
    {
        int capacity = Mathf.Clamp(ActiveCapacity, 0, MAX_CAPACITY);

        for (int i = 0; i < capacity; i++)
        {
            NetworkObject instance = _buffer[i];

            if (instance == null || !instance.IsValid)
                continue;

            if (instance.TryGetComponent(out BufferedObject bufferedObject) && bufferedObject.IsAwake)
                continue;

            if (instance.transform.parent != transform)
                instance.transform.SetParent(transform, true);
        }
    }

    public NetworkObject GetBufferedObject(out int assignedBufferIndex)
    {
        assignedBufferIndex = -1;

        if (ActiveCapacity <= 0)
            return null;

        int currentIndex = HeadIndex;
        assignedBufferIndex = currentIndex;
        NetworkObject instance = _buffer[currentIndex];

        if (instance != null && instance.TryGetComponent(out BufferedObject existingBufferedObject) && existingBufferedObject.IsAwake)
        {
            Debug.LogError($"[ObjectBuffer] Slot {currentIndex} contains an already-awake object.", instance);
            assignedBufferIndex = -1;
            return null;
        }

        if (instance == null)
        {
            instance = PrepareInstance();
        }
        else if (HasStateAuthority)
        {
            _buffer.Set(currentIndex, null);
        }

        if (instance == null)
        {
            assignedBufferIndex = -1;
            return null;
        }

        instance.transform.SetParent(null, true);
        HeadIndex = (currentIndex + 1) % ActiveCapacity;

        return instance;
    }

    private NetworkObject PrepareInstance()
    {
        if (!HasStateAuthority)
            return null;

        NetworkObject instance = Runner.Spawn(BufferedPrefab, new Vector3(0f, -1000f, 0f));

        if (!instance.TryGetComponent(out BufferedObject bufferedObject))
        {
            Debug.LogError("[ObjectBuffer] Buffered prefab needs a BufferedObject component.", instance);
            Runner.Despawn(instance);
            return null;
        }

        instance.transform.SetParent(transform, true);
        Runner.SetIsSimulated(instance, true);
        bufferedObject.ApplyStateImmediately();
        return instance;
    }

    private void CleanUpOrphanedBuffer()
    {
        while (_orphanCleanupIndex < ActiveCapacity)
        {
            int index = _orphanCleanupIndex++;
            NetworkObject instance = _buffer[index];

            if (instance == null)
                continue;

            _buffer.Set(index, null);
            Runner.Despawn(instance);
            return;
        }

        Runner.Despawn(Object);
    }
}
