using Fusion;
using UnityEngine;

public class CoreLifecycleManager : NetworkBehaviour
{
    [Networked] public ActiveCastID ActiveCastID { get; set; }
    [Networked] public SpellGraphId BlueprintID { get; set; }
    [Networked] public NetworkBool IsActiveInBuffer { get; set; }

    private ChangeDetector _changes;

    public override void Spawned()
    {
        _changes = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void FixedUpdateNetwork()
    {
        // Proxies monitor this. When the Host sets IsActiveInBuffer = true, the Proxy wakes up.
        foreach (var change in _changes.DetectChanges(this))
        {
            if (change == nameof(IsActiveInBuffer))
            {
                if (IsActiveInBuffer) WakeUp();
                else GoToSleep();
            }
        }
    }

    public void Initialize(ActiveCastID castId, SpellGraphId blueprintId)
    {
        // 1. If we were somehow already active (buffer overlap), clean up the old spell first!
        if (IsActiveInBuffer)
        {
            DeactivateCore();
        }

        ActiveCastID = castId;
        BlueprintID = blueprintId;
        IsActiveInBuffer = true;

        // 3. Claim the Token for the NEW spell!
        ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(ActiveCastID);
        if (activeSpell != null)
        {
            activeSpell.AddToken();
        }
    }

    // Call this when the fireball hits a wall, runs out of lifetime, or is destroyed
    public void DeactivateCore()
    {
        if (!IsActiveInBuffer) return;

        IsActiveInBuffer = false; // Replicates to proxies, triggering GoToSleep!
        GoToSleep();
    }

    private void WakeUp()
    {
        ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(ActiveCastID);

        if (activeSpell == null)
        {
            // We are a proxy! We didn't get the input, so we build it right now.
            if (SpellStateManager.instance.active_spellblueprints.TryGetValue(BlueprintID, out SpellGraph blueprint))
            {
                SpellState dummyProxyState = new SpellState(ActiveCastID, null, null, blueprint, null, null);
                activeSpell = new ActiveSpell(ActiveCastID, blueprint, dummyProxyState);

                SpellStateManager.instance.RegisterNewCast(ActiveCastID, activeSpell);
                Debug.Log($"[Proxy Sync] Lazy-loaded Cast {ActiveCastID.CastNumber} from physical core!");
                if (activeSpell != null) activeSpell.AddToken();
            }
            else
            {
                Debug.LogWarning($"[Proxy Sync] Failed to load. Blueprint {BlueprintID.id} not found on Proxy!");
            }
        }

        // Claim the token for the physical core
        
    }

    private void GoToSleep()
    {
        ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(ActiveCastID);
        if (activeSpell != null) activeSpell.RemoveToken();

        ActiveCastID = default;
        BlueprintID = default;
    }
}