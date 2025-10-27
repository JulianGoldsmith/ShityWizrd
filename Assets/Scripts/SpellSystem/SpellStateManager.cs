using System.Collections.Generic;
using UnityEngine;
using Fusion;
public class SpellStateManager : NetworkBehaviour
{
    // Singleton instance that keeps track of unique spell instances
    // this allows simul-casting by all connected clients, as well
    // as allowing 'breaks' in the chain (i.e. the host spawns a
    // gameobject, which replaces a client's buffered one).
    // This allows clients to 're-capture' spellstate information,
    // attach on relevant monobeaviours, etc, even when there
    // is a break in the chain due to networked instantiation.

    public static SpellStateManager instance;
    private void Awake()
    {
        instance = this;
        //curr_spell_state_id = new Dictionary<NetworkBehaviourId, int>();
        active_spellgraphs = new Dictionary<SpellGraphId, SpellGraph>();
        active_spellgraph_instances = new Dictionary<SpellGraphId, int>();
        timestamp = Time.time;
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


    #region Spell Graphs
    // Track all spellgraphs in the scene so that clients 
    // can look up the spellnodes of a spawned spell object.
    Dictionary<SpellGraphId, SpellGraph> active_spellgraphs = new Dictionary<SpellGraphId, SpellGraph>();
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
    Dictionary<SpellGraphId, int> active_spellgraph_instances = new Dictionary<SpellGraphId, int>();
    public static int _my_next_spellgraph_id = 0;
    public SpellGraph GetSpellGraph(SpellGraphId sgid)
    {
        foreach(var kv in active_spellgraphs)
        {
            Debug.Log($"activesgs : {kv.Key.sender_ref} {kv.Key.id} {kv.Value.name} {kv.Value != null}");
        }
        if (active_spellgraphs.TryGetValue(sgid, out SpellGraph value))
            return value;
        return null;
    }
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
            active_spellgraphs.Remove(spellgraphs_to_cleanup[i]);
            active_spellgraph_instances.Remove(spellgraphs_to_cleanup[i]);
        }
    }


    #region Subscription Service
    public void OnEquipSpellGraph(SpellGraphId sgid, SpellGraph graph)
    {
        // e.g. a new spellgraph has been created and equipped
        // so store it in the global dict.
        active_spellgraphs.Add(sgid, graph);
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

public struct SpellGraphId : INetworkStruct
{
    // every spellgraph is identified
    // by who sent the compiled spell (sender_ref)
    // and an arbitrary id that is maintained
    // by that player.
    // Players retain their player_ref_id while connected.

    // So when a player saves/equips a spell, they send it 
    // along with the id of the spell, signing it with their own
    // player ref.
    public int sender_ref;
    public int id;

    public SpellGraphId(int _playerref)
    {
        // for when we created the spell.
        SpellStateManager._my_next_spellgraph_id++;
        sender_ref = _playerref;
        id = SpellStateManager._my_next_spellgraph_id;
    }
    public SpellGraphId(int _playerref, int _id)
    {
        // for when we've received it from another.
        sender_ref = _playerref;
        id = _id;
    }

    public bool IsNull() 
        => (sender_ref == 0) && (id == 0);
    public bool NotNull() 
        => !IsNull();
    public bool Equals(SpellGraphId other) 
        => sender_ref == other.sender_ref && id == other.id;

    public static bool operator ==(SpellGraphId lhs, SpellGraphId rhs) 
        => lhs.Equals(rhs);

    public static bool operator !=(SpellGraphId lhs, SpellGraphId rhs)
        => !lhs.Equals(rhs);
}