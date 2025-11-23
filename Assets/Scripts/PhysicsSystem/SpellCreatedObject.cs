using Fusion;
using UnityEngine;
using UnityEngine.Events;

public class SpellCreatedObject : NetworkBehaviour
{
    [Networked, OnChangedRender(nameof(OnCorrespondingSpellUpdated))]
    public SpellGraphId corresponding_spellgraph_id { get; set; }
    [Networked, OnChangedRender(nameof(OnCorrespondingSpellUpdated))]
    public NetworkString<_64> corresponding_node_instance_guid { get; set; }

    SpellGraph corresponding_spell_graph;
    SpellNode corresponding_spell_node;
    [Networked] public TickTimer lifetime_timer { get; set; }
    bool should_despawn_next_tick = false;

    private SpellTrigger[] spelltriggers;
    private SpellBehaviour[] spellbehaviours;

    protected Tick? tick_spawned = null;

    bool should_Parent_To_Caster = false;

    public override void Spawned()
    {
        base.Spawned();
        corresponding_node_instance_guid = "";
    }

    public void InitialiseOnSpawned(ObjectCore node, SpellTriggerInfo triggerInfo, SpellState state)
    {
        if (node == null)
            throw new System.Exception("Input node to SCO was null");

        if (corresponding_node_instance_guid != "" && corresponding_node_instance_guid != node.InstanceGuid)
        {
            Debug.LogError($"{this.Id} Tried to initialise SCO with incorrect instance. found {node.InstanceGuid} but has {corresponding_node_instance_guid}");
            return;
        }

        Runner.SetIsSimulated(Object, true);

        // Communicate the details if we're the host.
        corresponding_spell_node = node as SpellNode;
        corresponding_node_instance_guid = node.InstanceGuid;
        if (state != null)
            corresponding_spellgraph_id = state.SpellGraphIdFrom;

        SubscribeToSpellStateManager();

        // This is called by anyone that spawns the object.
        // Anyone or doesn't (e.g. proxies) will catch up by using ids.
        node.ApplyPromotableValuesGeneric<SpellCreatedObject>(this);
        AssignProperties(node);

        if (triggerInfo != null)
        {
            node.AttatchBehavioursAndTriggers(gameObject, triggerInfo);
            InitialiseAfterBehavioursAndTriggers(node, triggerInfo.State);
        }
        else
        {
            node.AttatchBehavioursAndTriggers(gameObject, state);
            InitialiseAfterBehavioursAndTriggers(node, state);
        }
    }

    public void InitialiseOnSpawnedClientside()
    {
        // We use the values networked to use to dummy spawn, e.g.
        // attach behaviours and triggers.
        // We may not have all info.
        InitialiseOnSpawned((ObjectCore)corresponding_spell_node, null, null);
    }

    public void AssignProperties(ObjectCore createdby)
    {
        // Carryover any spell-modifiers into the properties?

        // Probably also need to pass on relevant triggers
        // or the rest of the spell, so that this can
        // trigger when necessary, e.g. when it breaks or despawns.
        lifetime_timer = TickTimer.CreateFromSeconds(Runner, createdby.lifetime);
    }

    public void InitialiseAfterBehavioursAndTriggers(SpellNode node, SpellState state)
    {
        tick_spawned = Runner.Tick; // reset the spawned tick, since might be buffering objects.
        spelltriggers = GetComponents<SpellTrigger>();
        spellbehaviours = GetComponents<SpellBehaviour>();
    }

    public void OnCorrespondingSpellUpdated()
    {
        // called whenever either of the node-instance-guid or the spellgraph-id
        // is changed.
        // so both times try to load the corresponding spell and node.
        // the second call will complete, the first won't.

        //Debug.Log($"{this.Id} corresponding {corresponding_spell_node == null} {corresponding_node_instance_guid}");
        //if(corresponding_spell_node != null)
        //    Debug.Log($"{this.Id} corresponding is not null and {corresponding_spell_node.InstanceGuid} ==? {corresponding_node_instance_guid}");

        if (corresponding_spell_node != null && corresponding_node_instance_guid == corresponding_spell_node.InstanceGuid)
            return;

        if (corresponding_spellgraph_id.NotNull())
        {
            corresponding_spell_graph = SpellStateManager.instance.GetSpellGraph(corresponding_spellgraph_id);
        }

        if (corresponding_node_instance_guid.Value != "" && corresponding_spell_graph != null)
        {
            corresponding_spell_node = corresponding_spell_graph.entryPointControllerNode.GetNodeInChain(corresponding_node_instance_guid.Value);
        }

        if (corresponding_spell_node != null)
        {
            InitialiseOnSpawnedClientside();
        }
    }
    void CheckIfMissedSpellUpdate()
    {
        // if we already have it, skip.
        if (corresponding_spell_node != null)
            return;

        OnCorrespondingSpellUpdated();
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        CheckIfMissedSpellUpdate();

        OnTickTriggerComponents();

        if (should_despawn_next_tick || lifetime_timer.Expired(Runner))
        {
            if (lifetime_timer.Expired(Runner))
                OnLifetimeExpired_event.Invoke();
            DespawnObject();
        }
    }

    public UnityEvent OnLifetimeExpired_event;
    public float GetRemainingLifetime()
    {
        if (lifetime_timer.Expired(Runner))
            return 0;

        if (!lifetime_timer.IsRunning)
            return -1;

        return lifetime_timer.RemainingTime(Runner) ?? 0;
    }
    protected void OnTickTriggerComponents()
    {
        TickTriggers();
        TickBehaviours();
    }

    void TickTriggers()
    {
        if (spelltriggers == null || spelltriggers.Length == 0)
            return;

        for (int i = 0; i < spelltriggers.Length; i++)
        {
            spelltriggers[i].OnTick();
        }
    }
    void TickBehaviours()
    {
        if (spellbehaviours == null || spellbehaviours.Length == 0)
            return;

        for (int i = 0; i < spellbehaviours.Length; i++)
        {
            spellbehaviours[i].OnTick();
        }
    }

  
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);

        UnsubscribeFromSpellStateManager();
    }

    bool has_subscribed_to_spellstatemanager = false;
    bool has_unsubscribed_from_spellstatemanager = false;
    void SubscribeToSpellStateManager()
    {
        if (has_subscribed_to_spellstatemanager)
            return;

        if (corresponding_spellgraph_id.IsNull())
            return;

        SpellStateManager.instance.SubscribeNode(corresponding_spellgraph_id);
    }
    void UnsubscribeFromSpellStateManager()
    {
        if (!has_subscribed_to_spellstatemanager
            || has_unsubscribed_from_spellstatemanager)
            return;

        if (corresponding_spellgraph_id.IsNull())
            return;

        SpellStateManager.instance.UnsubscribeNode(corresponding_spellgraph_id);
    }

    protected virtual void DespawnObject()
    {
        if (HasStateAuthority)
            Runner.Despawn(Object);
    }
}
