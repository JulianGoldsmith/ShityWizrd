using System.Collections.Generic;
using UnityEngine;

public class ActiveSpell
{
    public ActiveCastID CastID { get; private set; }
    public SpellGraph SpellBluePrint { get; private set; }
    public SpellState State { get; private set; }

    // Garbage Collection properties
    public int ActiveTokens { get; private set; }
    public bool InitialGraphExecutionFinished { get; private set; }

    public ActiveSpell(ActiveCastID castId, SpellGraph blueprint, SpellState state)
    {
        CastID = castId;
        SpellBluePrint = blueprint;
        State = state;
        ActiveTokens = 0;
        InitialGraphExecutionFinished = false;
    }

    // --- Token Management ---
    public void AddToken() => ActiveTokens++;
    public void RemoveToken() => ActiveTokens--;
    public void MarkInitialExecutionDone() => InitialGraphExecutionFinished = true;

    public bool IsSafeToDelete()
    {
        return InitialGraphExecutionFinished && ActiveTokens <= 0;
    }

    // --- Graph Execution Bridge ---
    // We will flesh this out in Phase 4 when we link it to the actual Node execution
    public void ExecuteContactNode(string nodeGuid, SpellTriggerInfo triggerInfo)
    {
        Debug.Log($"[ActiveSpell] Executing Contact Node {nodeGuid} for Cast {CastID.CastNumber}");
        // Example: SpellBluePrint.ExecuteNode(nodeGuid, triggerInfo);
    }
}