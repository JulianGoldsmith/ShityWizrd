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
}
public enum CommandType : byte
{
    None,
    Move_PathfindToID,
    Move_PathfindToPoint,
    Move_Forward,
    Move_Stop,
    Look_InMoveDirection,
    Action_MeleeAttack,
     // <-- ADD THIS
}