
using Fusion;
using UnityEngine;
using UnityEngine.VFX;


public class SpellCreatedPhysicsObject : PhysicsObject
{
    [SerializeField] GameObject shatterVFX;
    [Networked] private TickTimer lifetime_timer { get; set; }
    bool should_despawn_next_tick = false;
    public void AssignProperties(ObjectCore createdby)
    {
        // Carryover any spell-modifiers into the properties?

        // Probably also need to pass on relevant triggers
        // or the rest of the spell, so that this can
        // trigger when necessary, e.g. when it breaks or despawns.
        lifetime_timer = TickTimer.CreateFromSeconds(Runner, createdby.lifetime);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        
        if (should_despawn_next_tick || lifetime_timer.Expired(Runner))
            DespawnObject();
        else if (zero_bonkedness)
            should_despawn_next_tick = true;
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

        Debug.Log($"Despawned with {current_bonkedness} bonkedness");

        // Can't read in current_bonkedness because this despawned
        // is called before the networked properties are updated
        // (strangely) if called altogether,
        // so, instead, zero-bonkedness is checked within fixedupdatenetwork
        // and destroyed if zero.
        if (zero_bonkedness)
            CreateShatterParticles(Mathf.Max(0, -current_bonkedness));
    }
}
