using Fusion;
using System;
using UnityEngine;



public abstract class NPCAction : ScriptableObject
{
    [NonSerialized] public int ComboIndex;
    [NonSerialized] public NPCActionManager Manager;

    public virtual void InitializeRuntime(NPCActionManager manager, int comboIndex)
    {
        Manager = manager;
        ComboIndex = comboIndex;
    }

    public abstract void OnStart(int comboIndex);

    public abstract void OnEnd(int comboIndex);
    public virtual void Tick(int comboIndex, float deltaTime)
    {
       
    }

    protected virtual SpellState CreateAndRegisterSpellState(int comboIndex, SpellGraph graph)
    {
        if (Manager == null || graph == null) return null;

        graph.CompileSpell();

        var netObj = Manager.GetComponent<NetworkObject>();

        ActiveCastID newCastID = Manager.GenerateNewCastID();

        SpellState newCast = new SpellState(newCastID, Manager, null, graph, null, netObj);

        newCast.CastPosition = Manager.transform.position;
        newCast.ComboIndex = comboIndex;
        newCast.isHeld = true;

        Manager.RegisterAndTrackCast(newCast, graph);
        Manager.CurrentCastID = newCastID;

        return newCast;
    }

    protected virtual void RemoveCastingToken(SpellState state)
    {
        if (state == null) return;

        ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(state.ActiveCastID);
        if (activeSpell != null)
        {
            activeSpell.MarkInitialExecutionDone();
            activeSpell.RemoveToken();
        }
    }
}


public struct NetworkNPCActionData : INetworkStruct
{
    public NetworkBool isActive;
    public int actionID;
    public NetworkId targetID;
    public int revision;
    public int startTick;
    public int phaseID;
    public int phaseStartTick;
    public int chargeStartTick;
    public NetworkBool hasFired;
}

public struct NetworkNPCActionRequest : INetworkStruct
{
    public NetworkBool isValid;
    public int actionID;
    public NetworkId targetID;
    public int earliestStartTick;
    public int revision;
}

public struct NetworkNPCActionChannelState : INetworkStruct
{
    public NetworkNPCActionData activeAction;
    public NetworkNPCActionRequest pendingAction;
    public int revisionCounter;
}
