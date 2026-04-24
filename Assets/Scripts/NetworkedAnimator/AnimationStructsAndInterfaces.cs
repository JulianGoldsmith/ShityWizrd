using Fusion;
using UnityEngine;

/// <summary>
/// The core state of the animator. Synced over the network. 
/// Contains zero logic and zero gameplay variables.
/// </summary>
/// 
#region structs
public struct NetworkedAnimState : INetworkStruct
{
    public byte CurrentStateID;
    public byte PreviousStateID;

    // For handling interrupted transitions (The Cascade Blend)
    public byte CascadeStateID;
    public float InterruptionProgress;

    // Time tracking for deterministic blending
    public int TransitionStartTick;
    public int TransitionEndTick;
}

/// <summary>
/// Used by AI or players to queue discrete actions (e.g., swinging a sword)
/// without relying on non-deterministic Unity Triggers.
/// </summary>
public struct AnimTriggerCommand : INetworkStruct
{
    public byte RequestedActionID;
    public int RequestTick;
}


[System.Serializable]
public struct DeterministicAnimEvent
{
    public string EventID;
    [Range(0f, 1f)]
    public float NormalizedTime;
}

#endregion

#region AnimVarible Interfaces for controllers to implement
/// <summary>
/// Implemented by any component that dictates how fast the character is moving.
/// </summary>
public interface IAnimVarSpeed
{
    float GetCurrentSpeed();
}

/// <summary>
/// Implemented by any component that dictates movement input/direction.
/// Usually a normalized X/Y vector for 8-way blends.
/// </summary>
public interface IAnimVarDirection
{
    Vector2 GetCurrentDirection();
}

/// <summary>
/// Implemented by any component that calculates floor collisions/raycasts.
/// </summary>
public interface IAnimVarGrounded
{
    bool AnimIsGrounded();
}


// ==========================================
// 3. EVENT SYSTEM INTERFACE
// ==========================================

/// <summary>
/// Implemented by gameplay scripts (like a spellcaster or weapon handler)
/// to listen for deterministically timed animation events.
/// </summary>
public interface IAnimEventListener
{
    void OnDeterministicAnimEvent(string eventID, bool isResimulation);
}

#endregion