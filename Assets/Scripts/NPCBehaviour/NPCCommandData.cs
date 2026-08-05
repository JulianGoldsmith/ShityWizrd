using Fusion;
using UnityEngine;

public struct NPCCommandData : INetworkStruct
{
    // --- Routing & Resolution ---
    public CommandType CommandID;
    public byte Priority; // Higher number overrides lower numbers in the Manager

    // --- Timing ---
    public int SetTick;
    public int StartTick;
    public int EndTick;

    // --- The Generic Payload ---
    // These mean different things depending on the BehaviourID
    public NetworkId TargetID;
    public Vector3 VectorData;
    public float FloatData;
    public int IntData;
    public NPCMovementMode MovementMode;
}

public struct NPCCommandChannelState : INetworkStruct
{
    public NPCCommandData ActiveCommand; //    Current instruction being executed

    public int ActiveRevision;

    public NPCCommandData PendingCommand; //predicted replacement
    public NPCCommandChannelOperation PendingOperation;
    public int PendingStartTick;
    public int PendingRevision;
}

public enum NPCMovementMode : byte
{
    Stop,
    Walk,
    Run
}

public enum NPCCommandChannel : byte
{
    None = 0,
    Locomotion = 1,
    BodyFacing = 2,
    Gaze = 3,
    Posture = 4
}

public enum NPCCommandChannelOperation : byte
{
    None = 0, //no chnage -- ie for patch
    Set = 1, //replace the active command
    Clear = 2 //remove the active command
}

public enum CommandType
{
    None = 0,
    Move_PathfindToID = 1,
    Move_PathfindToPoint = 2,
    Move_Forward = 3,
    Move_Stop = 4,
    Look_InMoveDirection = 5,
    Action_Request = 6,
    Look_InDirection = 7,
    Look_AtPoint = 8,
    Look_AtID = 9
}
