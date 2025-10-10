
using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Pre-spawns network object in advance so they can be immediately used on Input Authority without a spawn delay.
/// Takes in a list of prefabs and associated prefab_counts, then distributes those across the buffer
/// to fill the capacity. 
/// Each prefab is given a section of the buffer, with its own bufferhead that loops over its section.
/// Cannot have more prefabs than max buffer capacity.
/// </summary>
public class NetworkObjectBuffer : NetworkBehaviour
{
    // CONSTANTS

    public const int CAPACITY = 8;

    public NetworkPrefabRef[] prefab_ids;
    public int[] prefab_counts; //to distribute the capacity between partial prefab buffers
    [SerializeField, Range(1, CAPACITY)]
    private int _bufferSize = CAPACITY;

    [Networked, Capacity(CAPACITY)]
    private NetworkArray<NetworkObject> _buffer { get; }
    [Networked] private NetworkArray<int> _bufferHeads { get; } // separate buffer heads for each partial prefab buffer.

    private NetworkObject[] _localBuffer = new NetworkObject[CAPACITY];

    private Dictionary<NetworkPrefabRef, partial_buffer_info> prefab_partial_buffer_infos;
    [Networked] private byte initial_kinematic_states { get; set; } // only allows up to 8 unique prefabs. Would need to change to short/int for more.
    struct partial_buffer_info
    {
        public partial_buffer_info(int _buffer_head_index, int _partial_buffer_start_index, int _partial_buffer_length)
        {
            buffer_head_index = _buffer_head_index;
            partial_buffer_start_index = _partial_buffer_start_index;
            partial_buffer_length = _partial_buffer_length;
        }
        public int buffer_head_index;
        public int partial_buffer_start_index;
        public int partial_buffer_length;
        public int partial_buffer_end_index { get { return partial_buffer_start_index + partial_buffer_length; } }
        public int increment_partial_buffer_index(int index)
        {
            int new_buffer_head_index = index + 1;
            if (new_buffer_head_index >= partial_buffer_end_index)
                new_buffer_head_index = partial_buffer_start_index;
            return new_buffer_head_index;
        }
    }

    public T Get<T>(NetworkPrefabRef prefabref, Vector3 position, Quaternion rotation) where T : NetworkBehaviour
    {
        var instance = Get(prefabref, position, rotation);
        return instance != null ? instance.GetComponent<T>() : null;
    }

    public NetworkObject Get(NetworkPrefabRef prefabref, Vector3 position, Quaternion rotation)
    {
        // This replaces the spawn method.

        if (!prefab_partial_buffer_infos.TryGetValue(prefabref, out partial_buffer_info buffer_info))
            //throw new System.Exception("[NetworkObjectBuffer] buffer_info not found for prefab.");
            return FallbackGet(prefabref, position, rotation);

        Debug.Log($"buffer_info found {buffer_info.buffer_head_index} {buffer_info.partial_buffer_start_index} {buffer_info.partial_buffer_length}");

        int _bufferhead = _bufferHeads[buffer_info.buffer_head_index];
        var instance = _buffer[_bufferhead];

        if (instance == null)
        {
            Debug.LogError($"Instance was null at {_bufferhead} for index {buffer_info.buffer_head_index}");
            // was null so refill the buffer and return a fallback.
            ReplaceBuffer(prefabref, _bufferhead);
            return FallbackGet(prefabref, position, rotation);
        }

        Debug.Log("Buffer spawning...");

        // replace its spot in the buffer with a new copy of it.
        _buffer.Set(_bufferhead, null);
        ReplaceBuffer(prefabref, _bufferhead);

        // reawaken the networkobject and put it in the right place.
        ReawakenAndPlace(instance, prefabref, position, rotation);

        // progress the corresponding bufferhead (and loop within partial buffer).
        int new_buffer_head_index = buffer_info.increment_partial_buffer_index(_bufferhead);
        Debug.Log($"new buffer head index: {new_buffer_head_index}");
        _bufferHeads.Set(buffer_info.buffer_head_index, new_buffer_head_index);

        return instance;
    }
    public NetworkObject FallbackGet(NetworkPrefabRef prefabref, Vector3 position, Quaternion rotation)
    {
        Debug.LogError("Couldn't find a buffer, so fell back to spawn.");

        // If we couldn't find a buffered version, we fallback to just spawn as usual.
        NetworkObject instance = PrepareInstance(prefabref);

        ReawakenAndPlace(instance, prefabref, position, rotation);

        return instance;
    }

    void ReawakenAndPlace(NetworkObject instance, NetworkPrefabRef prefabref, Vector3 position, Quaternion rotation)
    {
        Runner.SetIsSimulated(instance, true);
        //instance.AssignInputAuthority(inputAuthority);

        instance.gameObject.SetActive(true);

        NetworkRigidbody3D rb = instance.GetComponent<NetworkRigidbody3D>();
        if (rb != null)
        {
            rb.Teleport(position, rotation);
            if (prefab_partial_buffer_infos.TryGetValue(prefabref, out partial_buffer_info buffer_info))
                rb.RBIsKinematic = GetIsKinematic(buffer_info.buffer_head_index);
        }
        else
        {
            instance.transform.SetPositionAndRotation(position, rotation);
        }
    }

    public override void Render()
    {
        //ReconcileLocalAndNetworkBuffers();
    }
    public void ReconcileLocalAndNetworkBuffers()
    {
        for (int i = 0; i < _bufferSize; i++)
        {
            var networkInstance = _buffer[i];
            var localInstance = _localBuffer[i];

            if (localInstance == networkInstance)
                continue;

            if (localInstance != null && localInstance.IsValid == true)
            {
                // Network instance was released so we need to activate
                // object on all clients (including proxies) not only
                // on those where Get method was called
                localInstance.gameObject.SetActive(true);

//#if UNITY_EDITOR
//                localInstance.name = prefab.name;
//#endif
            }

            _localBuffer[i] = networkInstance;

            if (networkInstance != null)
            {
                // New instance was added to the buffer, we need to make sure
                // that the object is inactive on all clients (including proxies)
                networkInstance.gameObject.SetActive(false);

//#if UNITY_EDITOR
//                networkInstance.name = $"(Buffered) {networkInstance.name}";
//#endif
            }

            //var localName = localInstance != null ? localInstance.Id.ToString(): "null";
            //var remoteName = networkInstance != null ? networkInstance.Id.ToString() : "null";
            //Debug.Log($"{Runner.name} - {Object.InputAuthority} ({Time.frameCount}) - Changing local buffer on index {i} Local {localName} Network {remoteName}");
        }
    }

    public override void Spawned()
    {
        Initialise();
    }

    public void Initialise()
    {
        if (!VerifyInputArrayLengths())
            throw new System.Exception("[NetworkObjectBuffer] Input prefab arrays of different lengths or null.");
        if (!VerifySumTotalPartialBufferLengths())
            throw new System.Exception("[NetworkObjectBuffer] Sum total partial buffer lengths greater than capacity.");

        FillBuffer();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        ClearBuffer();
    }

    bool VerifyInputArrayLengths()
    {
        return (prefab_ids != null) &&
            (prefab_counts != null) &&
            (prefab_counts.Length == prefab_counts.Length);
    }

    bool VerifySumTotalPartialBufferLengths()
    {
        int running_total = 0;
        for (int i = 0; i < prefab_counts.Length; i++)
        {
            running_total += prefab_counts[i];
        }
        return running_total <= CAPACITY;
    }

    void FillBuffer()
    {
        if (HasStateAuthority)
            ResetKinematicsByte();

        prefab_partial_buffer_infos = new Dictionary<NetworkPrefabRef, partial_buffer_info>();

        int prefab_index = 0;
        int next_partial_buffer_start_index = prefab_counts[prefab_index];
        if (HasStateAuthority)
            _bufferHeads.Set(prefab_index, 0);
        prefab_partial_buffer_infos.Add(prefab_ids[0],
                    new partial_buffer_info(
                        prefab_index,
                        0,
                        prefab_counts[prefab_index]
                    ));

        for (int i = 0; i < _bufferSize; i++)
        {
            if (i >= next_partial_buffer_start_index)
            {
                prefab_index++;
                if (HasStateAuthority)
                    _bufferHeads.Set(prefab_index, next_partial_buffer_start_index);
                next_partial_buffer_start_index += prefab_counts[prefab_index];
                prefab_partial_buffer_infos.Add(prefab_ids[i],
                    new partial_buffer_info(
                        prefab_index,
                        i,
                        prefab_counts[prefab_index]
                    ));
            }

            if (HasStateAuthority && (_buffer[i] == null))
            {
                Debug.Log("Adding to buffer");
                _buffer.Set(i, PrepareInstance(prefab_ids[prefab_index]));
            }
        }
    }
    void ReplaceBuffer(NetworkPrefabRef prefabref, int index)
    {
        if (HasStateAuthority == false)
            return;
        _buffer.Set(index, PrepareInstance(prefabref));
    }

    void ClearBuffer()
    {
        if (HasStateAuthority == false)
        {
            System.Array.Clear(_localBuffer, 0, _localBuffer.Length);
            return;
        }

        for (int i = 0; i < _bufferSize; i++)
        {
            if (_buffer[i] != null)
            {
                Runner.Despawn(_buffer[i]);
            }
        }
        _buffer.Clear();
    }
    private NetworkObject PrepareInstance(NetworkPrefabRef prefabref)
    {
        if (!HasStateAuthority)
            return null;

        var instance = Runner.Spawn(prefabref, new Vector3(0f, -1000f, 0f));

        // cache the kinematic state so we can
        // reassign it after setting setissimulated=true.
        // this is because setissimulated seems to set
        // iskinematic to false.
        // Just manually set isKinematic to false to 
        // try stop it transmitting position.
        if (instance.TryGetComponent(out Rigidbody rb))
        {
            SetKinematicsByteBit(prefabref, rb.isKinematic);
            rb.isKinematic = false;
        }

        Runner.SetIsSimulated(instance, false);
        instance.gameObject.SetActive(false);

        return instance;
    }

    void ResetKinematicsByte()
    {
        initial_kinematic_states = 0;
    }
    void SetKinematicsByteBit(NetworkPrefabRef prefabref, bool isKinematic)
    {
        if (!prefab_partial_buffer_infos.TryGetValue(prefabref, out partial_buffer_info buffer_info))
            return;
        SetKinematicsByteBit(buffer_info.buffer_head_index, isKinematic);
    }
    void SetKinematicsByteBit(int prefab_index, bool isKinematic)
    {
        initial_kinematic_states ^= (byte)((-(isKinematic ? 1 : 0) ^ initial_kinematic_states) & (1 << prefab_index));
    }
    bool GetIsKinematic(int prefab_index)
    {
        return (initial_kinematic_states & (1 << prefab_index)) != 0;
    }
}