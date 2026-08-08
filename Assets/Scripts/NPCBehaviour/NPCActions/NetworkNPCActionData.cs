using Fusion;

public struct NetworkNPCActionData : INetworkStruct
{
    public NetworkBool isActive;
    public int actionID;
    public NetworkId targetID;
    public int revision;
    public int startTick;
    public ActiveCastID castID;

    public bool IsValid => isActive && actionID >= 0;
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