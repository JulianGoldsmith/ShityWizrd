using Fusion;
using UnityEngine;

public static class StatusEffectExtensions
{
    public static float GetFloat(this ref ActiveStatusEffectData data, int localIndex, NetworkedMemoryAllocator memory)
    {
        return memory.FloatMemory.Get(data.FloatOffset + localIndex);
    }

    public static void SetFloat(this ref ActiveStatusEffectData data, int localIndex, float value, NetworkedMemoryAllocator memory)
    {
        memory.FloatMemory.Set(data.FloatOffset + localIndex, value);
    }
}

public struct ActiveStatusEffectData : INetworkStruct
{
    public byte EffectID;
    public EffectLifecycle EffectType; 
    public int StartTick;
    public int EndTick;

    public byte PresenceCount; //this is used for persistance checks - ie channeled spells that have 0 source are removed

    public byte FloatOffset;
    public byte IntOffset;

    public bool IsExpired(int currentTick)
    {
        if (EffectType == EffectLifecycle.Duration)
        {
            return EndTick > 0 && currentTick >= EndTick;
        }

        // Channeled effects expire when PresenceCount hits 0
        return PresenceCount == 0;
    }

}


public struct ProposedEffectPayload
{
    public int DurationInTicks;
    public float Magnitude;
    public NetworkId TargetId;
    public EffectLifecycle EffectType;
}

public interface IStatusEffect
{
    //StatusEffectType EffectType { get; }
    // The core Execution Loop. Runs every tick in FUN.
    // We pass 'ref' to the effectData so if the logic needs to update the EndTick, it can!
    void Tick(int tick, PhysicsObject target, NetworkedMemoryAllocator memory, ref ActiveStatusEffectData effectData, ref MaterialState currentState, PhysicsObjectMaterial mat);

    // Returns TRUE if absorbed. Returns FALSE if they should coexist.
    bool TryStack( NetworkRunner runner, ref ActiveStatusEffectData existingEffect, NetworkedMemoryAllocator memory, ProposedEffectPayload newPayload);

    void OnAllocated(NetworkedMemoryAllocator memory, ref ActiveStatusEffectData newEffectData, ProposedEffectPayload payload);
    void OnRemoved(NetworkedMemoryAllocator memory, ref ActiveStatusEffectData effectData);
}

