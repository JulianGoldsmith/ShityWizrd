using UnityEngine;
using System.Collections.Generic;
using Fusion;

public abstract class CastActionController : NetworkBehaviour
{
    public ActiveCastTracker CastTracker { get; private set; }

    public bool isCasting;

    public List<SpellState> activeCasts = new List<SpellState>();

    [Networked] public int TotalSpellCasts { get; set; }

    public override void Spawned()
    {
        base.Spawned();
        CastTracker = GetComponent<ActiveCastTracker>();
    }

    // 1. THE NETWORK LEDGER

    public ActiveCastID GenerateNewCastID()
    {
        TotalSpellCasts++;
        return new ActiveCastID(this.Object.Id, TotalSpellCasts);
    }

    public void RegisterAndTrackCast(SpellState newCast, SpellGraph graph)
    {
        if (!activeCasts.Contains(newCast))
        {
            activeCasts.Add(newCast);
        }

        if (CastTracker != null)
        {
            CastTracker.RegisterNetworkedCast(newCast.NetCastData);
        }

        ActiveSpell newActiveSpell = new ActiveSpell(newCast.ActiveCastID, graph, newCast);
        newActiveSpell.AddToken();
        SpellStateManager.instance.RegisterNewCast(newCast.ActiveCastID, newActiveSpell);
    }

    public void ClearCastsForItem(EquipableItem item)
    {
        for (int i = activeCasts.Count - 1; i >= 0; i--)
        {
            if (activeCasts[i].CastItem == item)
            {
                activeCasts.RemoveAt(i);
            }
        }
    }

    public SpellState GetActiveSpellState(int comboIndex)
    {
        for (int i = activeCasts.Count - 1; i >= 0; i--)
        {
            if (activeCasts[i].ComboIndex == comboIndex && activeCasts[i].isHeld)
                return activeCasts[i];
        }
        return null;
    }


    // 2. THE SPATIAL CONTRACT

    public abstract Vector3 GetAimTarget();

    public abstract EyePosAndLookDir GetEyePosAndLookDir();

    public virtual Vector3 GetSpellCastPoint() { return transform.position; }

    public virtual Vector3 GetSpellCastDir()
    {
        Vector3 direction = GetAimTarget() - GetSpellCastPoint();
        return direction.normalized;
    }


    // 3. THE Hit CONTRACT (To be refined later)
    public abstract void ActivateHitbox(int hitBoxID, SpellState state);
    public abstract void DeactivateHitbox(int hitBoxID);

    // Sub-classes dictate how spells end (Player via Mouse Release, NPC via Animation)
    public abstract void EndCast();
}