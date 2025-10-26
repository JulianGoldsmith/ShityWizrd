
using Fusion;
using UnityEngine;
using UnityEngine.VFX;


public class SpellCreatedPhysicsObject : PhysicsObject
{
    //[Networked] SpellStateID spell_state_id { get; set; }
    //SpellState original_spell_state;
    [Networked, OnChangedRender(nameof(OnCorrespondingSpellUpdated))]
    public SpellGraphId corresponding_spellgraph_id { get; set; }
    [Networked, OnChangedRender(nameof(OnCorrespondingSpellUpdated))] 
    public NetworkString<_64> corresponding_node_instance_guid { get; set; }
    // note that the guids are 36 characters long but have four dashes ('-')
    // at regular index, so could be converted to a 32-length string and _32 used.
    // the dashes would need to then be added back in.
    SpellGraph corresponding_spell_graph;
    SpellNode corresponding_spell_node;

    [SerializeField] GameObject shatterVFX;
    [Networked] private TickTimer lifetime_timer { get; set; }
    bool should_despawn_next_tick = false;

    private SpellTrigger[] spelltriggers;

    bool initialised_on_spawn = false;

    public void InitialiseOnSpawned(ObjectCore node, SpellTriggerInfo triggerInfo, SpellState state)
    {
        if (initialised_on_spawn)
            return;

        initialised_on_spawn = true;

        if (HasStateAuthority)
        {
            // Communicate the details if we're the host.
            corresponding_spell_node = node as SpellNode;
            corresponding_node_instance_guid = corresponding_spell_node.InstanceGuid;
            if (state != null)
                corresponding_spellgraph_id = state.SpellGraphIdFrom;
        }

        SubscribeToSpellStateManager();

        // This is called by anyone that spawns the object.
        // Anyone or doesn't (e.g. proxies) will catch up by using ids.
        node.ApplyPromotableValuesGeneric<SpellCreatedPhysicsObject>(this);
        physicsObjectProperties = node.ApplyPromotableValuesGeneric<PhysicsObjectProperties>(physicsObjectProperties);
        AssignProperties(node);
        InitialisePhysicsObject();

        Debug.Log("initialising object on spawn");

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
        Debug.Log("clientside spawning");
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
    }

    public void OnCorrespondingSpellUpdated()
    {
        // called whenever either of the node-instance-guid or the spellgraph-id
        // is changed.
        // so both times try to load the corresponding spell and node.
        // the second call will complete, the first won't.
        Debug.Log($"corresponding spell updated");
        if (corresponding_spellgraph_id.NotNull())
        {
            corresponding_spell_graph = SpellStateManager.instance.GetSpellGraph(corresponding_spellgraph_id);
            Debug.Log($"found corresponding spell graph {corresponding_spell_graph.name} {corresponding_spell_graph != null} {corresponding_spellgraph_id.sender_ref} {corresponding_spellgraph_id.id}");
        }

        if (corresponding_node_instance_guid.Value != "" && corresponding_spell_graph != null)
        {
            corresponding_spell_node = corresponding_spell_graph.entryPointControllerNode.GetNodeInChain(corresponding_node_instance_guid.Value);
            Debug.Log($"found corresponding spell node 1 {corresponding_node_instance_guid.Value} {corresponding_spell_node != null}");
            Debug.Log($"found corresponding spell node 2 {corresponding_spell_node.InstanceGuid}");
        }

        if (corresponding_spell_graph != null && corresponding_spell_node != null)
        {
            Debug.Log($"spell updated, clientside init");
            InitialiseOnSpawnedClientside();
        }
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        
        OnTickTriggerComponents();

        if (should_despawn_next_tick || lifetime_timer.Expired(Runner))
            DespawnObject();
        else if (zero_bonkedness)
            should_despawn_next_tick = true;
    }

    [Networked, OnChangedRender(nameof(OnTickClientCatchup))] public int tick { get; set; } //int that increments everytime a ticktrigger is called.
    // (client will probably be one tick behind...)

    public void OnTickClientCatchup()
    {
        // Only run for clients.
        // Pseudo tick tracker.
        // This is surely incorrect, 
        // since we'll run a load of missed 
        // ticks altogether.
        // Could just run one.
        if (HasStateAuthority)
            return;
        if (tick == Runner.Tick)
            return;

        int tick_diff = Mathf.Max(0, Runner.Tick - tick);
        OnTickTriggerComponents();
    }
    protected void OnTickTriggerComponents()
    {
        if (spelltriggers == null || spelltriggers.Length == 0)
            return;

        for (int i = 0; i < spelltriggers.Length; i++)
        {
            spelltriggers[i].OnTick();
        }
        tick = Runner.Tick;
    }

    protected override void OnZeroBonk()
    {
        base.OnZeroBonk();

        Debug.Log("OnZeroBonk");

        // trigger any on-death triggers of associated spell.

        // create some vfx of it breaking.
        // THis is done locally, since it doesn't matter.
        if (!Runner.IsForward)
            return;

        // despawn this.
        DespawnObject();
    }

    void CreateShatterParticles(float bonk_amount)
    {
        GameObject shatter_vfx_obj = Instantiate(shatterVFX, transform.position, transform.rotation);
        ShatterVFX shatter_vfx = shatter_vfx_obj.GetComponent<ShatterVFX>();
        if (shatter_vfx != null)
            shatter_vfx.AssignProperties(physicsObjectProperties, bonk_amount);

    }
    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        base.Despawned(runner, hasState);

        //Debug.Log($"Despawned with {current_bonkedness} bonkedness");

        // Can't read in current_bonkedness because this despawned
        // is called before the networked properties are updated
        // (strangely) if called altogether,
        // so, instead, zero-bonkedness is checked within fixedupdatenetwork
        // and destroyed if zero.
        if (zero_bonkedness)
            CreateShatterParticles(Mathf.Max(0, -current_bonkedness));

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

}
