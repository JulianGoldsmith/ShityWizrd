
using Fusion;
using UnityEngine;
using UnityEngine.VFX;


public class SpellCreatedPhysicsObject : PhysicsObject
{
    [Networked] SpellStateID spell_state_id { get; set; }
    SpellState original_spell_state;
    [Networked] public NetworkString<_64> corresponding_node_instance_guid { get; set; }
    // note that the guids are 36 characters long but have four dashes ('-')
    // at regular index, so could be converted to a 32-length string and _32 used.
    // the dashes would need to then be added back in.
    SpellNode corresponding_spell_node;

    [SerializeField] GameObject shatterVFX;
    [Networked] private TickTimer lifetime_timer { get; set; }
    bool should_despawn_next_tick = false;

    private SpellTrigger[] spelltriggers;

    public void InitialiseOnSpawned(ObjectCore node, SpellTriggerInfo triggerInfo, SpellState state)
    {
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
        //for(int i = 0; i < spelltriggers.Length; i++)
        //{
        //    spelltriggers[i].OnAttach();
        //}
        spell_state_id = state.SpellStateID;
        original_spell_state = state;
        corresponding_spell_node = node;
        corresponding_node_instance_guid = node.InstanceGuid;
    }

    public void CheckSpellStateGUIDChanged()
    {
        if (HasStateAuthority)
            return;

        if (original_spell_state != null)
            return;

        if (spell_state_id.nb_id == NetworkBehaviourId.None)
            return;

        SpellState spellstate = SpellStateManager.instance.GetSpellState(spell_state_id);
        Debug.Log($"Get Spell state GUID {spellstate == null}");
        if (original_spell_state != spellstate)
        {
            original_spell_state = spellstate;
            corresponding_spell_node = null;
        }
    }
    void CheckNodeInstanceGUID()
    {
        if (original_spell_state == null)
            return;

        if (corresponding_spell_node != null)
            return;

        corresponding_spell_node = original_spell_state.OriginalCasterNode.GetNodeInChain(corresponding_node_instance_guid.Value);
        Debug.Log($"corresponding spell node {corresponding_spell_node == null} {corresponding_spell_node.nodeName}");
        if (corresponding_spell_node is ObjectCore corresponding_core)
        {
            InitialiseOnSpawned(corresponding_core, null, original_spell_state);
            // we don't have the trigger info...
            // for now, try null and see what happens...
            //corresponding_core.AttatchBehavioursAndTriggers(gameObject, original_spell_state);
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
    public override void Render()
    {
        base.Render();
        
        if (HasStateAuthority)
            return;

        CheckSpellStateGUIDChanged();
        CheckNodeInstanceGUID();
    }
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
        //for (int i = 0; i < tick_diff; i++)
        //{
        //}
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
    }


}
