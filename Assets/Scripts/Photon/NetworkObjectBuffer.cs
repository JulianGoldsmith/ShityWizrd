
using Fusion;
using Fusion.Addons.Physics;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pre-spawns network object in advance so they can be immediately used on Input Authority without a spawn delay.
/// Takes in a list of prefabs and associated prefab_counts, then distributes those across the buffer
/// to fill the capacity. 
/// Each prefab is given a section of the buffer, with its own bufferhead that loops over its section.
/// Cannot have more prefabs than max buffer capacity.
/// </summary>
[DefaultExecutionOrder(-100)]
public class NetworkObjectBuffer : NetworkBehaviour
{
    // CONSTANTS

    public const int CAPACITY = 16;

    public NetworkPrefabRef[] prefab_ids;
    public int[] prefab_counts; //to distribute the capacity between partial prefab buffers
    [SerializeField, Range(1, CAPACITY)]
    private int _bufferSize = CAPACITY;

    [Networked, Capacity(CAPACITY)]
    private NetworkArray<NetworkObject> _buffer { get; }

    [Networked, Capacity(CAPACITY)]
    private NetworkArray<int> _bufferHeads { get; } // separate buffer heads for each partial prefab buffer.

    private NetworkObject[] _localBuffer = new NetworkObject[CAPACITY];

    private Dictionary<NetworkPrefabRef, partial_buffer_info> prefab_partial_buffer_infos;
    [Networked] private byte initial_kinematic_states { get; set; } // only allows up to 8 unique prefabs. Would need to change to short/int for more.

    private readonly HashSet<NetworkObject> _locallyClaimed = new HashSet<NetworkObject>();

    public NetworkObject thisNO;
    
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
        if (!prefab_partial_buffer_infos.TryGetValue(prefabref, out partial_buffer_info buffer_info))
            return FallbackGet(prefabref, position, rotation);

        int _bufferhead = _bufferHeads[buffer_info.buffer_head_index];
        var instance = _buffer[_bufferhead];

        if (instance == null)
        {
            Debug.LogError($"Instance was null at {_bufferhead} for index {buffer_info.buffer_head_index}");
            if (HasStateAuthority)
                ReplaceBuffer(prefabref, _bufferhead);
            return FallbackGet(prefabref, position, rotation);
        }

        if (HasStateAuthority)
        {
            _buffer.Set(_bufferhead, null);
            ReplaceBuffer(prefabref, _bufferhead);

            int new_buffer_head_index = buffer_info.increment_partial_buffer_index(_bufferhead);
            _bufferHeads.Set(buffer_info.buffer_head_index, new_buffer_head_index);
        }
        if (!HasStateAuthority && Runner.IsResimulation)
            Debug.Log("ReawakenAndPlaceCalledInResim");
        // This wakes the instance on this peer
        ReawakenAndPlace(instance, prefabref, position, rotation);

        // IMPORTANT: on non-authority clients, mark this as claimed so the buffer logic
        // never tries to treat it as a hidden pooled object.
        if (!HasStateAuthority)
        {
            _locallyClaimed.Add(instance);
        }

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
        if (instance == null)
            return;

        Reawaken(instance);

        Debug.Log($"[Reawaken] instance={instance.Id} sim={instance.IsInSimulation}");

        if (instance.TryGetComponent<NetworkRigidbody3D>(out var rb))
        {
            rb.Teleport(position, rotation);
            if (prefab_partial_buffer_infos.TryGetValue(prefabref, out partial_buffer_info buffer_info))
                rb.RBIsKinematic = GetIsKinematic(buffer_info.buffer_head_index);
        }
        // 2. Handle Transform Cores (NetworkTransform)
        else if (instance.TryGetComponent<NetworkTransform>(out var nt))
        {
            // THIS IS THE FIX: Tell Fusion to snap it instantly, bypassing interpolation!
            nt.Teleport(position, rotation);
        }
        // 3. Fallback
        else
        {
            instance.transform.SetPositionAndRotation(position, rotation);
        }
    }

    void Reawaken(NetworkObject instance)
    {
        if (instance == null)
            return;
        Runner.SetIsSimulated(instance, true);
        instance.gameObject.SetActive(true);
        Debug.Log($"[Reawaken] instance={instance.Id} sim={instance.IsInSimulation}");
    }

    public override void FixedUpdateNetwork()
    {
        //if(!HasStateAuthority && !HasInputAuthority)
        //{
        //    Debug.Log("FixedUpdate is running on the proxy");
        //}
        //else
        //    Debug.Log("FixedUpdate is running on client or host");
        
    }

    public override void Render()
    {
        //if (HasInputAuthority) return;
        ReconcileLocalAndNetworkBuffers();
    }
    public void ReconcileLocalAndNetworkBuffers()
    {
        for (int i = 0; i < _bufferSize; i++)
        {
            var networkInstance = _buffer[i];

            //If our local buffer isnt the new recieved networked buffer then the host/ other has removed _localBuffer[i] from the buffer and it needs wakingup
            if (_localBuffer[i] != _buffer[i]) {
                Reawaken(_localBuffer[i]);
                //continue;
            }

            // 0) If this instance has been explicitly claimed for prediction on this client,
            //    DO NOT let the buffer system touch it. Gameplay owns it.
            if (networkInstance != null && _locallyClaimed.Contains(networkInstance))
            {
                _localBuffer[i] = networkInstance;
                continue;
            }

            // 1) For everything else, just mirror the buffer content.
            _localBuffer[i] = networkInstance;

            // 2) If there is a buffer object in this slot, we enforce "hidden + not simulated".
            if (networkInstance != null && networkInstance.IsValid)
            {
                networkInstance.gameObject.SetActive(false);
                Runner.SetIsSimulated(networkInstance, false);

                if (HasStateAuthority && thisNO != null && thisNO.InputAuthority != networkInstance.InputAuthority)
                {
                    networkInstance.AssignInputAuthority(thisNO.InputAuthority);
                }
            }

            // 3) If networkInstance is null, slot empty - nothing else to do.
        }
    }

    public override void Spawned()
    {
        Initialise();
        
    }

    public void Initialise()
    {
        thisNO = this.GetComponent<NetworkObject>();
        Runner.SetIsSimulated(thisNO, true);

        if (!VerifyInputArrayLengths())
            throw new System.Exception("[NetworkObjectBuffer] Input prefab arrays of different lengths or null.");
        if (!VerifySumTotalPartialBufferLengths())
            throw new System.Exception("[NetworkObjectBuffer] Sum total partial buffer lengths greater than capacity.");

        if (_bufferSize == 0 || prefab_ids == null || prefab_ids.Length == 0)
        {
            Debug.Log($"Buffer not filled. _bufferSize {_bufferSize} pref count {prefab_ids.Length}");
            return;
        }

        FillBuffer();
    }
    public void Initialise(SpellGraph graph)
    {
        ClearBuffer();
        if (graph != null)
            LoadFromSpellGraph(graph);
        Initialise();
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
        Debug.Log("Filling buffer");
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
                prefab_partial_buffer_infos.Add(prefab_ids[prefab_index],
                    new partial_buffer_info(
                        prefab_index,
                        i,
                        prefab_counts[prefab_index]
                    ));
            }

            if (HasStateAuthority && (_buffer[i] == null))
            {
                //Debug.Log("Adding to buffer");
                _buffer.Set(i, PrepareInstance(prefab_ids[prefab_index]));
            }
        }
    }
    void ReplaceBuffer(NetworkPrefabRef prefabref, int index)
    {
        if (HasStateAuthority == false)
            return;
        _buffer.Set(index, PrepareInstance(prefabref));
        
        // amend localbuffer to avoid flicker
        _localBuffer[index] = _buffer[index];
    }

    void ClearBuffer()
    {
        _locallyClaimed.Clear();

        if (HasStateAuthority == false)
        {
            System.Array.Clear(_localBuffer, 0, _localBuffer.Length);
            return;
        }

        for (int i = 0; i < _bufferSize; i++)
        {
            if (_buffer[i] != null && _buffer[i].IsValid)
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
            rb.isKinematic = true;
        }

        Runner.SetIsSimulated(instance, true); ////////========================
        instance.gameObject.SetActive(false);

        return instance;
    }

    #region Kinematics
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
    #endregion

    #region Loading from Spell
    const int _max_iterations_search_in_graph = 50;
    const int _first_prefab_priority = 4;
    const int _other_prefab_priority = 1;
    public void LoadFromSpellGraph(SpellGraph graph)
    {
        if (graph == null)
            return;

        // Grab all the prefabs, their weights, and
        // create a buffer based on that.

        // Here we can assign weighting depending on
        // what needs to be buffered more.
        // We prioritise the first object most (for
        // smooth casting), but might also want
        // to prioritise runes that spawn many objects
        // simultaneously (e.g. an explosion).

        // assigns:
        //prefab_ids
        //prefab_counts
        //_buffer_size

        Debug.Log("loading buffer from spellgraph.");

        Dictionary<NetworkPrefabRef, float> found_prefabrefs_with_priorities = new Dictionary<NetworkPrefabRef, float>();
        float total_priority = 0;
        float current_priority = 0;

        int search_count_remaining = 0;
        List<SpellNode> queued_search = new List<SpellNode>();

        int infinite_loop_fallback = 0;
        SpellNode next_node = graph.entryPointControllerNode;
        NetworkPrefabRef next_ref;

        while((next_node != null || search_count_remaining > 0) && infinite_loop_fallback < _max_iterations_search_in_graph)
        {
            //Debug.Log($"iteration {infinite_loop_fallback} and found {found_prefabrefs_with_priorities.Count}");
            current_priority = 0;

            if (search_count_remaining > 0 && next_node == null)
            {
                // just to catch weird cases of null nodes.
                // Continue search.
                search_count_remaining = queued_search.Count;
                next_node = queued_search[0];
                queued_search.RemoveAt(0);
                continue;
            }

            // Break out of while loop if it's been going too long.
            if (infinite_loop_fallback >= _max_iterations_search_in_graph)
                break;
            infinite_loop_fallback++;

            //Debug.Log($"next node is {next_node.name} {next_node is IHasPrefabRefToBuffer}");

            // This node has a prefabref to buffer, so add it to the dict.
            if(next_node is IHasPrefabRefToBuffer)
            {
                //Debug.Log("has prefabreftobuffer");
                // Give more priority to the first prefab found.
                // TODO:
                //  - Add more here to weight objects that might
                // be spawned many-at-a-time simultaneously
                // e.g. an explosion that creates many.
                if (total_priority <= 0)
                    current_priority = _first_prefab_priority;
                else
                    current_priority = _other_prefab_priority;

                next_ref = (next_node as IHasPrefabRefToBuffer).prefabRefToBuffer;
                if (!found_prefabrefs_with_priorities.ContainsKey(next_ref))
                    found_prefabrefs_with_priorities.Add(next_ref, 0);
                
                found_prefabrefs_with_priorities[next_ref] += current_priority;
                total_priority += current_priority;
            }

            // Add all dependent nodes to the queue.
            // Currently this will eventually get everything in the spell
            // though I don't see a case where a some node types
            // could even have a prefabref.
            // So currently generic but could be streamlined for speed.
            // All clientside though, at least.
            queued_search.AddRange(next_node.GetAllDependentNodes());
            //Debug.Log($"queued search count {queued_search.Count}");

            if (queued_search.Count > 0)
            {
                // Continue search.
                search_count_remaining = queued_search.Count;
                next_node = queued_search[0];
                queued_search.RemoveAt(0);
            }
            else
            {
                // nothing left to search, so leave the loop.
                next_node = null;
                break;
            }
        }

        DistributeDictArossPrefabArrays(found_prefabrefs_with_priorities, total_priority);
    }

    void DistributeDictArossPrefabArrays(Dictionary<NetworkPrefabRef, float> found_prefabrefs_with_priorities, float total_priority)
    {
        int count = found_prefabrefs_with_priorities.Count;
        prefab_ids = new NetworkPrefabRef[count];
        prefab_counts = new int[count];
        _bufferSize = 0;
        int index = 0;
        // if total_priority is less than count, 
        // then we don't try to fill the capacity, we
        // just leave as-is.
        // Otherwise we distribute out the CAPACITY based on
        // priority.
        float priority_to_count_factor = Mathf.Min(CAPACITY / total_priority, 1);
        foreach (KeyValuePair<NetworkPrefabRef, float> keyValuePair in found_prefabrefs_with_priorities)
        {
            prefab_ids[index] = keyValuePair.Key;
            prefab_counts[index] = Mathf.RoundToInt(keyValuePair.Value * priority_to_count_factor);
            _bufferSize += prefab_counts[index];
            index++;
        }

        if(_bufferSize > CAPACITY)
        {
            throw new System.Exception($"Tried to make buffer larger than CAPACITY {_bufferSize} / {CAPACITY}");
        }
    }
    #endregion
}