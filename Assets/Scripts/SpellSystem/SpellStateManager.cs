using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;


public class SpellStateManager : NetworkBehaviour
{
    // Singleton instance that keeps track of unique spell instances
    // this allows simul-casting by all connected clients, as well
    // as allowing 'breaks' in the chain (i.e. the host spawns a
    // gameobject, which replaces a client's buffered one).
    // This allows clients to 're-capture' spellstate information,
    // attach on relevant monobeaviours, etc, even when there
    // is a break in the chain due to networked instantiation.
    public int activeSpellsCount;

    public static SpellStateManager instance;

    public Dictionary<SpellGraphId, SpellGraph> active_spellblueprints = new Dictionary<SpellGraphId, SpellGraph>();

    public Dictionary<SpellGraphId, int> active_spellgraph_instances = new Dictionary<SpellGraphId, int>();

    public Dictionary<SpellGraphId, RuntimeSpell> hydratedSpells = new Dictionary<SpellGraphId, RuntimeSpell>();

    public Dictionary<ActiveCastID, ActiveSpell> activeSpells = new Dictionary<ActiveCastID, ActiveSpell>();

    [Header("Static Spells")]
    public StaticSpellDatabase staticSpellDatabase;
    [Networked, Capacity(100)] public NetworkArray<SpellGraphId> ActiveManifest { get; }
    //[Networked, Capacity(100)] public NetworkArray<NetworkCastData> ActiveCastsData { get; }

    private ChangeDetector _manifestChangeDetector;


    private void Awake()
    {
        instance = this;
        //curr_spell_state_id = new Dictionary<NetworkBehaviourId, int>();
        active_spellblueprints = new Dictionary<SpellGraphId, SpellGraph>();
        active_spellgraph_instances = new Dictionary<SpellGraphId, int>();
        timestamp = Time.time;
        
    }

    public override void Spawned()
    {
        base.Spawned();
        Runner.SetIsSimulated(this.Object, true);

        LoadStaticSpells();

        _manifestChangeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // If we joined late, the Host might already have spells in the manifest. Check now!
        if (!Object.HasStateAuthority)
        {
            SyncLocalDictionaryWithManifest();
        }

    }

    float timestamp;
    // number of seconds between spellgraph cleanup checks
    // (doesn't have to be frequent):
    const float spellgraph_cleanup_frequency = 5;
    void Update()
    {
        if((Time.time - timestamp) > spellgraph_cleanup_frequency)
        {
            CleanUpActiveSpellGraphsDict();
            timestamp = Time.time;
        }
    }

    public override void FixedUpdateNetwork()
    {
        // Token Garbage Collection (Host Only) ---
        if (Object.HasStateAuthority) {
            List<ActiveCastID> spellsToTrash = new List<ActiveCastID>();

            foreach (var kvp in activeSpells)
            {
                if (kvp.Value.IsSafeToDelete())
                {
                    spellsToTrash.Add(kvp.Key);
                }
            }

            foreach (var id in spellsToTrash)
            {
                activeSpells.Remove(id);

                if (Runner.TryFindObject(id.CasterId, out NetworkObject casterObj))
                {
                    if (casterObj.TryGetComponent<ActiveCastTracker>(out var tracker))
                    {
                        tracker.RemoveNetworkedCast(id);
                    }
                }

                //RPC_DeleteActiveSpell(id.CasterId, id.CastNumber);
            }
        } else
        {
            List<ActiveCastID> spellsToTrash = new List<ActiveCastID>();

            foreach (var kvp in activeSpells)
            {
                ActiveCastID id = kvp.Key;

                if (Runner.TryFindObject(id.CasterId, out NetworkObject casterObj))
                {
                    if (casterObj.TryGetComponent<ActiveCastTracker>(out var tracker))
                    {
                        if (!tracker.GetCastData(id).CastID.IsValid)
                        {
                            spellsToTrash.Add(id);
                        }
                    }
                }
            }

            foreach (var id in spellsToTrash)
            {
                activeSpells.Remove(id);
            }
        }

        SyncLocalDictionaryWithManifest();

        activeSpellsCount = activeSpells.Count;
    }

    #region SpellBluePrintsManifest

    public SpellGraph GetSpellGraph(SpellGraphId id)
    {
        if (id.IsNull()) return null;
        if (active_spellblueprints.TryGetValue(id, out SpellGraph graph))
        {
            return graph;
        }
        return null;
    }

    private Dictionary<SpellGraphId, float> _requestTimestamps = new Dictionary<SpellGraphId, float>();

    private void SyncLocalDictionaryWithManifest()
    {
        if (Object.HasStateAuthority) return;

        // =======================================
        // PART 1: Check for ADDITIONS (Missing Data)
        // =======================================
        for (int i = 0; i < ActiveManifest.Length; i++)
        {
            SpellGraphId id = ActiveManifest[i];

            if (id.IsNull()) continue;

            if (!active_spellblueprints.ContainsKey(id))
            {
                bool shouldRequest = true;

                if (_requestTimestamps.TryGetValue(id, out float lastRequestTime))
                {
                    if (Time.time - lastRequestTime < 3f)
                    {
                        shouldRequest = false; // Still waiting, don't spam
                    }
                }

                if (shouldRequest)
                {
                    Debug.Log($"[Manifest] Missing Blueprint {id.BlueprintNumber}. Requesting from Host...");
                    _requestTimestamps[id] = Time.time;

                    if (_downloadingSpells.ContainsKey(id))
                    {
                        _downloadingSpells.Remove(id);
                    }

                    RPC_RequestSpellBlueprint(id);
                }
            }
        }

        // =======================================
        // PART 2: Check for REMOVALS (Garbage Collection)
        // =======================================
        List<SpellGraphId> keysToDelete = new List<SpellGraphId>();

        foreach (var localKey in active_spellblueprints.Keys)
        {
            if (localKey.AuthorRef == PlayerRef.None) continue;
            bool foundInManifest = false;
            for (int i = 0; i < ActiveManifest.Length; i++)
            {
                if (ActiveManifest[i].Equals(localKey))
                {
                    foundInManifest = true;
                    break;
                }
            }

            // If we have it in RAM, but the Host erased it from the Manifest, we must delete it!
            if (!foundInManifest)
            {
                keysToDelete.Add(localKey);
            }
        }

        foreach (var key in keysToDelete)
        {
            active_spellblueprints.Remove(key);
            Debug.Log($"[Manifest] Host deleted Blueprint {key.BlueprintNumber}. Removed from local RAM.");
        }
    }

    public void AddToManifest(SpellGraphId newId)
    {
        if (!Object.HasStateAuthority) return;

        for (int i = 0; i < ActiveManifest.Length; i++)
        {
            if (ActiveManifest[i].IsNull())
            {
                ActiveManifest.Set(i, newId);
                return;
            }
        }
        Debug.LogWarning("Manifest is full! Max 100 spells reached.");
    }

    public void RemoveFromManifest(SpellGraphId idToRemove)
    {
        if (!Object.HasStateAuthority) return;

        // 1. Remove it from the Host's Master Library
        if (active_spellblueprints.ContainsKey(idToRemove))
        {
            active_spellblueprints.Remove(idToRemove);
        }

        // 2. Find it in the Networked Manifest and wipe the slot clean
        for (int i = 0; i < ActiveManifest.Length; i++)
        {
            if (ActiveManifest[i].Equals(idToRemove))
            {
                // Setting it to default creates a struct where AuthorRef is invalid and Number is 0.
                // This makes IsNull() return true for this slot!
                ActiveManifest.Set(i, default);
                Debug.Log($"[Manifest] Cleared slot {i}. Blueprint {idToRemove.BlueprintNumber} deleted.");
                break;
            }
        }
    }


    #endregion

    #region SPELL RPC DATA TRANSFER 

    private Dictionary<SpellGraphId, byte[][]> _downloadingSpells = new Dictionary<SpellGraphId, byte[][]>();
    private Dictionary<PlayerRef, byte[][]> _incomingSubmissions = new Dictionary<PlayerRef, byte[][]>();
    private int _hostMasterBlueprintCounter = 0;

    [Rpc(RpcSources.Proxies | RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_RequestSpellBlueprint(SpellGraphId missingId, RpcInfo info = default)
    {
        PlayerRef requester = info.Source;
        if (active_spellblueprints.TryGetValue(missingId, out SpellGraph graph))
        {
            byte[] compressedData = SerializeSpellData(graph.Data);
            RPC_DeliverSpell(requester, missingId, compressedData); // 1 single packet!
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_DeliverSpell(PlayerRef targetPlayer, SpellGraphId sgid, byte[] compressedData)
    {
        if (Runner.LocalPlayer.PlayerId != targetPlayer.PlayerId) return;

        try
        {
            SpellGraph newGraph = ScriptableObject.CreateInstance<SpellGraph>();
            newGraph.Data = DeserializeSpellData(compressedData);
            newGraph.spellGraphId = sgid;

            active_spellblueprints[sgid] = newGraph;
            HydrateAndStoreSpell(sgid, newGraph);

            Debug.Log($"[Manifest] SUCCESSFULLY downloaded Spell {sgid.BlueprintNumber} from Host!");

            EquipableItem[] allItems = FindObjectsOfType<EquipableItem>();
            foreach (var item in allItems)
            {
                if (item.PrimarySpellID.Equals(sgid)) item.OnPrimarySpellChanged();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Client] Failed to build requested Blueprint {sgid.BlueprintNumber}!");
        }
    }
    public void SubmitNewSpellToHost(SpellGraph graph, NetworkId targetWeapon)
    {
        if (Object.HasStateAuthority)
        {
            _hostMasterBlueprintCounter++;
            SpellGraphId newId = new SpellGraphId(Runner.LocalPlayer, _hostMasterBlueprintCounter);
            graph.spellGraphId = newId;
            active_spellblueprints[newId] = graph;
            HydrateAndStoreSpell(newId, graph);
            AddToManifest(newId);
            BroadcastSpellToAll(newId, graph);

            if (targetWeapon.IsValid && Runner.TryFindObject(targetWeapon, out var weaponObj))
            {
                weaponObj.GetComponent<EquipableItem>().PrimarySpellID = newId;
            }
        }
        else
        {
            // Instantly compress and send in 1 packet!
            byte[] compressedData = SerializeSpellData(graph.Data);
            RPC_SubmitSpellToHost(Runner.LocalPlayer, compressedData, targetWeapon);
        }
    }

    [Rpc(RpcSources.Proxies | RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SubmitSpellToHost(PlayerRef author, byte[] compressedData, NetworkId targetWeapon)
    {
        try
        {
            SpellGraph newGraph = ScriptableObject.CreateInstance<SpellGraph>();
            newGraph.Data = DeserializeSpellData(compressedData); // Instantly rebuild!

            _hostMasterBlueprintCounter++;
            SpellGraphId newId = new SpellGraphId(author, _hostMasterBlueprintCounter);
            newGraph.spellGraphId = newId;

            active_spellblueprints[newId] = newGraph;
            HydrateAndStoreSpell(newId, newGraph);
            AddToManifest(newId);
            BroadcastSpellToAll(newId, newGraph);

            if (targetWeapon.IsValid && Runner.TryFindObject(targetWeapon, out var weaponObj))
            {
                weaponObj.GetComponent<EquipableItem>().PrimarySpellID = newId;
            }

            Debug.Log($"[Manifest] Host registered new Spell {newId.BlueprintNumber} from {author}.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Host] Failed to compile submitted spell. Error: {e.Message}");
        }
    }

    private void BroadcastSpellToAll(SpellGraphId id, SpellGraph graph)
    {
        byte[] compressedData = SerializeSpellData(graph.Data);
        RPC_BroadcastSpell(id, compressedData); // 1 single packet!
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_BroadcastSpell(SpellGraphId sgid, byte[] compressedData)
    {
        if (Object.HasStateAuthority) return;

        try
        {
            SpellGraph newGraph = ScriptableObject.CreateInstance<SpellGraph>();
            newGraph.Data = DeserializeSpellData(compressedData);
            newGraph.spellGraphId = sgid;

            active_spellblueprints[sgid] = newGraph;
            HydrateAndStoreSpell(sgid, newGraph);

            Debug.Log($"[Manifest] Broadcast Received! Instantly Hydrated Spell {sgid.BlueprintNumber}");

            EquipableItem[] allItems = FindObjectsOfType<EquipableItem>();
            foreach (var item in allItems)
            {
                if (item.PrimarySpellID.Equals(sgid)) item.OnPrimarySpellChanged();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Client] Failed to build Broadcasted Spell: {e.Message}");
        }
    }
    #endregion

    #region Compression

    private byte[] SerializeSpellData(SpellNetworkData data)
    {
        using (System.IO.MemoryStream ms = new System.IO.MemoryStream())
        using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(ms))
        {
            writer.Write(data.MaxNodeIndex);
            writer.Write(data.MaxWireIndex);

            // Only network the active nodes!
            for (int i = 0; i <= data.MaxNodeIndex; i++)
            {
                writer.Write(data.Nodes[i].TemplateID);
                writer.Write(data.Nodes[i].Position.x);
                writer.Write(data.Nodes[i].Position.y);
                writer.Write(data.Nodes[i].Position.z);
            }

            // Only network the active wires!
            for (int i = 0; i <= data.MaxWireIndex; i++)
            {
                writer.Write(data.Wires[i].FromNodeIndex);
                writer.Write(data.Wires[i].FromSocketIndex);
                writer.Write(data.Wires[i].ToNodeIndex);
                writer.Write(data.Wires[i].ToSocketIndex);
            }

            return ms.ToArray();
        }
    }

    private SpellNetworkData DeserializeSpellData(byte[] bytes)
    {
        SpellNetworkData data = new SpellNetworkData();
        data.Nodes = new NetworkNodeData[64];
        data.Wires = new WireData[128];
        for (int i = 0; i < 128; i++) data.Wires[i].FromSocketIndex = 255; // Apply tombstones

        using (System.IO.MemoryStream ms = new System.IO.MemoryStream(bytes))
        using (System.IO.BinaryReader reader = new System.IO.BinaryReader(ms))
        {
            data.MaxNodeIndex = reader.ReadByte();
            data.MaxWireIndex = reader.ReadByte();

            for (int i = 0; i <= data.MaxNodeIndex; i++)
            {
                data.Nodes[i].TemplateID = reader.ReadUInt16();
                data.Nodes[i].Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            }

            for (int i = 0; i <= data.MaxWireIndex; i++)
            {
                data.Wires[i].FromNodeIndex = reader.ReadByte();
                data.Wires[i].FromSocketIndex = reader.ReadByte();
                data.Wires[i].ToNodeIndex = reader.ReadByte();
                data.Wires[i].ToSocketIndex = reader.ReadByte();
            }
        }
        return data;
    }
    #endregion

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_DeleteActiveSpell(NetworkId casterId, int castNum)
    {
        ActiveCastID id = new ActiveCastID(casterId, castNum);
        if (activeSpells.ContainsKey(id))
        {
            activeSpells.Remove(id);
        }
    }

 

    public void RegisterNewCast(ActiveCastID castId, ActiveSpell newCast)
    {
        activeSpells[castId] = newCast;
    }

    public ActiveSpell GetActiveSpell(ActiveCastID castId)
    {
        if (activeSpells.TryGetValue(castId, out ActiveSpell activeSpell))
        {
            return activeSpell;
        }
        return null;
    }


    private void LoadStaticSpells()
    {
        if (staticSpellDatabase == null) return;

        for (int i = 0; i < staticSpellDatabase.staticSpells.Count; i++)
        {
            TextAsset spellJson = staticSpellDatabase.staticSpells[i];
            if (spellJson == null || string.IsNullOrEmpty(spellJson.text)) continue;

            try
            {
                SpellGraph graph = ScriptableObject.CreateInstance<SpellGraph>();
                JsonUtility.FromJsonOverwrite(spellJson.text, graph);

                SpellGraphId staticId = new SpellGraphId(PlayerRef.None, i + 1);
                graph.spellGraphId = staticId;

                active_spellblueprints[staticId] = graph;
                HydrateAndStoreSpell(staticId, graph);
                Debug.Log($"[SpellStateManager] Loaded Static Spell [{i + 1}]: {spellJson.name}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SpellStateManager] Failed to load static spell at index {i + 1}: {e.Message}");
            }
        }
        
    }

    #region Spell Graphs
    // Track all spellgraphs in the scene so that clients 
    // can look up the spellnodes of a spawned spell object.

    // A dictionary to track how many active instances of a spellgraph there are.
    // When it hits zero, we clean it up from active_spellgraphs so that we
    // don't create an arbitrarily large dict as people change their spells.
    // This works by a subscription methodology:
    //  - We increment when a spell is equipped.
    //  - We decrement when a spell is replaced (overwritten).
    //  - Summoned objects increment their corresponding spellgraph when they spawn (awaken).
    //  - Summoned objects decrement when they despawn.
    // This ensures that active_spellgraphs contains all spellgraphs that are
    //  either currently-equipped or have an active object/node somewhere in the scene.
    public static int _my_next_spellgraph_id = 0;
 
    List<SpellGraphId> spellgraphs_to_cleanup = new List<SpellGraphId>();
    public void CleanUpActiveSpellGraphsDict()
    {
        // scrub through the active_spellgraph_instances
        // and check if any are zero, and therefore
        // should be removed from active_spellgraphs.

        // This doesn't really need to be done that often,
        // we just need to make sure we don't let it
        // become arbitrarily big.
        // So we can just do this every few seconds, and that's
        // sufficient.

        //use a local list to reduce GC.
        spellgraphs_to_cleanup.Clear();
        
        foreach(KeyValuePair<SpellGraphId, int> kv in active_spellgraph_instances)
        {
            if(kv.Value <= 0)
            {
                // no instances left, and unequipped,
                // so remove.
                // Here we track what we want to remove, then
                // loop again and remove, rather than
                // deleting here, which could mess with the
                // foreach loop.
                // So just being safe.
                spellgraphs_to_cleanup.Add(kv.Key);
            }
        }

        for(int i = 0; i < spellgraphs_to_cleanup.Count; i++)
        {
            active_spellblueprints.Remove(spellgraphs_to_cleanup[i]);
            active_spellgraph_instances.Remove(spellgraphs_to_cleanup[i]);
        }
    }


    #region Subscription Service
    public void OnEquipSpellGraph(SpellGraphId sgid, SpellGraph graph)
    {
        // e.g. a new spellgraph has been created and equipped
        // so store it in the global dict.
        active_spellblueprints.Add(sgid, graph);
        active_spellgraph_instances.Add(sgid, 1);
        HydrateAndStoreSpell(sgid, graph);
    }
    public void OnUnequipSpellGraph(SpellGraphId sgid)
    {
        // e.g. a new spellgraph has been created and replaced
        // the prior.
        // We don't remove, we just decrement its spellgraph instances.
        if (active_spellgraph_instances.ContainsKey(sgid))
            active_spellgraph_instances[sgid] += 1;
    }
    public void SubscribeNode(SpellGraphId sgid)
    {
        if (active_spellgraph_instances.ContainsKey(sgid))
            active_spellgraph_instances[sgid] += 1;
    }
    public void UnsubscribeNode(SpellGraphId sgid)
    {
        if (active_spellgraph_instances.ContainsKey(sgid))
            active_spellgraph_instances[sgid] -= 1;
    }
    #endregion
    #endregion


    //#region SpellStates

    //Dictionary<NetworkBehaviourId, int> curr_spell_state_id = new Dictionary<NetworkBehaviourId, int>();
    //// spell state ids (i.e. cast ids) will not be the same order
    //// across clients/host/proxies, due to when packets arrive and
    //// ticks are calculated.
    //// but, the cast ids per network object will be ordered the same way
    //// ...assuming no missing packets... :( 
    //// So instead of just an incrementing int id, we build up an id from
    //// a networkbehaviourid and an int id. The int essentially shows
    //// the count of casts that object has performed.

    //// the only way this gets out-of-sync is if a packet is missed
    //// where something (a player/enemy) cast and then they cast again
    //// before that packet is received (or its never received).
    //// the other option is that each castactioncontroller tracks this
    //// state id, to allow us to correct up in case of missed packets.

    //public int GetNextSpellStateId(NetworkBehaviour nb)
    //{
    //    if (curr_spell_state_id.ContainsKey(nb))
    //    {
    //        curr_spell_state_id[nb]++;
    //        return curr_spell_state_id[nb];
    //    }
    //    else
    //    {
    //        curr_spell_state_id.Add(nb, 0);
    //        return 0;
    //    }
    //}
    //// This dictionary contains all spell_states currently in effect.
    //// They are indexed by a unique id.
    //// These are all local spell states, generated by sharing
    //// information.
    //// Each casting of a spell receives a unique identifier.
    //// This allows us to find it's spell state globally, not just
    //// from passing into scripts.
    //// The unique identifier of a spellstate is networked and attached
    //// to any summoned object.
    //Dictionary<SpellStateID, SpellState> local_spell_states = new Dictionary<SpellStateID, SpellState>();



    //public void AddSpellState(SpellState state)
    //{
    //    Debug.Log($"adding spell state with guid {state.SpellStateID.cast_id}");
    //    local_spell_states.Add(state.SpellStateID, state);
    //}

    //public SpellState GetSpellState(SpellStateID guid)
    //{
    //    local_spell_states.TryGetValue(guid, out SpellState state);
    //    return state;
    //}
    //#endregion

    public void HydrateAndStoreSpell(SpellGraphId id, SpellGraph graph)
    {
        if (graph == null || graph.Data.Nodes == null) return;

        SpellCompilationContext context = new SpellCompilationContext();

        // 1. Run the Assembly Line to get the FULL array
        // (We modify SpellHydrator to return the full array, not just the root)
        IRuntimeNode[] hydratedGraph = SpellHydrator.HydrateFullGraph(graph.Data, SpellGraphController.Instance.availableNodeTemplates, context);

        // 2. Store the whole package in RAM
        hydratedSpells[id] = new RuntimeSpell()
        {
            Blueprint = graph,
            HydratedNodes = hydratedGraph,
            RootNode = hydratedGraph[0] // Entry Point is always index 0!
        };
    }

    public IRuntimeNode GetHydratedSpell(SpellGraphId id)
    {
        // 1. Output the RuntimeSpell container
        if (hydratedSpells.TryGetValue(id, out RuntimeSpell runtimeSpell))
        {
            // 2. Return the RootNode from inside the container!
            return runtimeSpell.RootNode;
        }
        return null;
    }

}

public struct SpellGraphId : INetworkStruct, IEquatable<SpellGraphId>
{
    // We keep the variables simple. 
    // The Host will assign these when it confirms the spell.
    public PlayerRef AuthorRef;
    public int BlueprintNumber;

    // The single, explicit constructor. No more hidden static counters!
    public SpellGraphId(PlayerRef authorRef, int blueprintNumber)
    {
        AuthorRef = authorRef;
        BlueprintNumber = blueprintNumber;
    }

    public bool IsNull() => BlueprintNumber == 0;

    public bool NotNull() => !IsNull();

    // Required for Fusion dictionaries to compare IDs perfectly
    public bool Equals(SpellGraphId other)
    {
        return AuthorRef == other.AuthorRef && BlueprintNumber == other.BlueprintNumber;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(AuthorRef, BlueprintNumber);
    }


}

public class RuntimeSpell
{
    public SpellGraph Blueprint;
    public IRuntimeNode[] HydratedNodes; // The whole graph in RAM!
    public IRuntimeNode RootNode;        // Entry Point (Node 0)
}
