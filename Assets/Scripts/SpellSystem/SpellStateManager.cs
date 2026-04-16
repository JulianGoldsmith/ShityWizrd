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

    public Dictionary<ActiveCastID, ActiveSpell> activeSpells = new Dictionary<ActiveCastID, ActiveSpell>();


    [Networked, Capacity(100)] public NetworkArray<SpellGraphId> ActiveManifest { get; }
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
                RPC_DeleteActiveSpell(id.CasterId, id.CastNumber);
            }
        }
        
        SyncLocalDictionaryWithManifest();
        

            /*foreach (var change in _manifestChangeDetector.DetectChanges(this))
            {
                if (change == nameof(ActiveManifest))
                {

                }
            }*/

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
        Debug.Log($"[Host] Received request from {requester} for Blueprint {missingId.BlueprintNumber}");

        if (active_spellblueprints.TryGetValue(missingId, out SpellGraph graph))
        {
            string json = graph.ToJson();
            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
            int chunkSize = 200; // Safe MTU size
            int totalChunks = (data.Length + chunkSize - 1) / chunkSize;

            Debug.Log($"[Host] Sending Blueprint {missingId.BlueprintNumber} to {requester} in {totalChunks} chunks.");

            for (int i = 0; i < totalChunks; i++)
            {
                int size = Mathf.Min(chunkSize, data.Length - (i * chunkSize));
                byte[] chunk = new byte[size];
                Buffer.BlockCopy(data, i * chunkSize, chunk, 0, size);

                RPC_DeliverSpellChunk(requester, missingId, i, totalChunks, chunk);
            }
        }
        else
        {
            Debug.LogWarning($"[Host] Client asked for Blueprint {missingId.BlueprintNumber}, but Host doesn't have it!");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_DeliverSpellChunk(PlayerRef targetPlayer, SpellGraphId sgid, int chunkIndex, int totalChunks, byte[] chunkData)
    {
        if (Runner.LocalPlayer.PlayerId != targetPlayer.PlayerId) return;

        Debug.Log($"[Client] Received chunk {chunkIndex + 1}/{totalChunks} for Blueprint {sgid.BlueprintNumber}");

        if (!_downloadingSpells.ContainsKey(sgid))
        {
            _downloadingSpells[sgid] = new byte[totalChunks][];
        }

        _downloadingSpells[sgid][chunkIndex] = chunkData;

        bool complete = true;
        for (int i = 0; i < totalChunks; i++)
        {
            if (_downloadingSpells[sgid][i] == null)
            {
                complete = false;
                break;
            }
        }

        if (complete)
        {
            Debug.Log($"[Client] All chunks received for Blueprint {sgid.BlueprintNumber}. Rebuilding JSON...");

            try
            {
                byte[] fullData = _downloadingSpells[sgid].SelectMany(c => c).ToArray();
                string json = System.Text.Encoding.UTF8.GetString(fullData);

                SpellGraph newGraph = SpellGraph.FromJson(json);

                if (newGraph != null)
                {
                    newGraph.spellGraphId = sgid;
                    active_spellblueprints[sgid] = newGraph;
                    _downloadingSpells.Remove(sgid);

                    Debug.Log($"[Manifest] SUCCESSFULLY downloaded and built Spell {sgid.BlueprintNumber} from Host!");

                    // Wake up the weapons
                    EquipableItem[] allItems = FindObjectsOfType<EquipableItem>();
                    foreach (var item in allItems)
                    {
                        if (item.PrimarySpellID.Equals(sgid))
                        {
                            item.OnPrimarySpellChanged();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Client] Failed to build Blueprint {sgid.BlueprintNumber} from JSON! Error: {e.Message}");
                _downloadingSpells.Remove(sgid); // Clear it so we can retry safely!
            }
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
            AddToManifest(newId);

            
            if (targetWeapon.IsValid && Runner.TryFindObject(targetWeapon, out var weaponObj))
            {
                weaponObj.GetComponent<EquipableItem>().PrimarySpellID = newId;
            }
        }
        else
        {
            string json = graph.ToJson();
            byte[] data = System.Text.Encoding.UTF8.GetBytes(json);
            int chunkSize = 200;
            int totalChunks = (data.Length + chunkSize - 1) / chunkSize;

            for (int i = 0; i < totalChunks; i++)
            {
                int size = Mathf.Min(chunkSize, data.Length - (i * chunkSize));
                byte[] chunk = new byte[size];
                Buffer.BlockCopy(data, i * chunkSize, chunk, 0, size);

               
                RPC_SubmitSpellChunkToHost(Runner.LocalPlayer, i, totalChunks, chunk, targetWeapon);
            }
        }
    }

    
    [Rpc(RpcSources.Proxies | RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    public void RPC_SubmitSpellChunkToHost(PlayerRef author, int chunkIndex, int totalChunks, byte[] chunkData, NetworkId targetWeapon)
    {
        if (!_incomingSubmissions.ContainsKey(author))
        {
            _incomingSubmissions[author] = new byte[totalChunks][];
        }

        _incomingSubmissions[author][chunkIndex] = chunkData;

        bool complete = true;
        for (int i = 0; i < totalChunks; i++)
        {
            if (_incomingSubmissions[author][i] == null)
            {
                complete = false;
                break;
            }
        }

        if (complete)
        {
            try
            {
                byte[] fullData = _incomingSubmissions[author].SelectMany(c => c).ToArray();
                string json = System.Text.Encoding.UTF8.GetString(fullData);
                SpellGraph newGraph = SpellGraph.FromJson(json);

                _hostMasterBlueprintCounter++;
                SpellGraphId newId = new SpellGraphId(author, _hostMasterBlueprintCounter);
                newGraph.spellGraphId = newId;

                active_spellblueprints[newId] = newGraph;
                AddToManifest(newId);
                _incomingSubmissions.Remove(author);

                if (targetWeapon.IsValid && Runner.TryFindObject(targetWeapon, out var weaponObj))
                {
                    weaponObj.GetComponent<EquipableItem>().PrimarySpellID = newId;
                }

                Debug.Log($"[Manifest] Host officially registered new Spell {newId.BlueprintNumber} from Player {author}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Host] Failed to compile submitted spell from {author}. Error: {e.Message}");
                _incomingSubmissions.Remove(author);
            }
        }
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

