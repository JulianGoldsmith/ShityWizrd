using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class ActiveSpell
{
    public ActiveCastID CastID { get; private set; }
    public SpellGraphId BlueprintID { get; private set; }
    public SpellGraph SpellBluePrint { get; private set; } //legacy
    public SpellState State { get; private set; }

    private readonly HashSet<NetworkId> _ownedTokens = new HashSet<NetworkId>();
    private int _unownedTokens;

    public int ActiveTokens => _unownedTokens + _ownedTokens.Count;
    public bool InitialGraphExecutionFinished { get; private set; }

    public ActiveSpell(ActiveCastID castId, SpellGraphId blueprintID, SpellState state)
    {
        CastID = castId;
        BlueprintID = blueprintID;
        State = state;
        SpellBluePrint = null;
        _unownedTokens = 0;
        InitialGraphExecutionFinished = false;
    }

    public ActiveSpell(ActiveCastID castId, SpellGraph legacyBlueprint, SpellState state) : this(castId, legacyBlueprint != null ? legacyBlueprint.spellGraphId : default, state)
    {
        SpellBluePrint = legacyBlueprint;
    }

    public void AddToken() => _unownedTokens++;
    public void RemoveToken() => _unownedTokens = Mathf.Max(0, _unownedTokens - 1);
    public void AddToken(NetworkId ownerId) => _ownedTokens.Add(ownerId);
    public void RemoveToken(NetworkId ownerId) => _ownedTokens.Remove(ownerId);
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
