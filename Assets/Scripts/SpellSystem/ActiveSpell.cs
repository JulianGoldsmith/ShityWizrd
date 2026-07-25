using System.Collections.Generic;
using UnityEngine;

public class ActiveSpell
{
    public ActiveCastID CastID { get; private set; }
    public SpellGraphId BlueprintID { get; private set; }
    public SpellGraph SpellBluePrint { get; private set; } //legacy
    public SpellState State { get; private set; }

    public int ActiveTokens { get; private set; }
    public bool InitialGraphExecutionFinished { get; private set; }

    public ActiveSpell(ActiveCastID castId, SpellGraphId blueprintID, SpellState state)
    {
        CastID = castId;
        BlueprintID = blueprintID;
        State = state;
        SpellBluePrint = null;
        ActiveTokens = 0;
        InitialGraphExecutionFinished = false;
    }

    public ActiveSpell(ActiveCastID castId, SpellGraph legacyBlueprint, SpellState state) : this(castId, legacyBlueprint != null ? legacyBlueprint.spellGraphId : default, state)
    {
        SpellBluePrint = legacyBlueprint;
    }

    public void AddToken() => ActiveTokens++;
    public void RemoveToken() => ActiveTokens--;
    public void MarkInitialExecutionDone() => InitialGraphExecutionFinished = true;

    public bool IsSafeToDelete()
    {
        return InitialGraphExecutionFinished && ActiveTokens <= 0;
    }

    public void ExecuteContactNode(string nodeGuid, SpellTriggerInfo triggerInfo)
    {
        Debug.Log($"[ActiveSpell] Executing Contact Node {nodeGuid} for Cast {CastID.CastNumber}");
    }
}