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

    public int StartTick;
    public int EndTick;

    public byte FloatOffset;
    public byte IntOffset;

    public bool IsExpired(int currentTick)
    {
        return EndTick > 0 && currentTick >= EndTick;
    }

}


public struct ProposedEffectPayload
{
    public int DurationInTicks;
    public float Magnitude;
    public NetworkId TargetId;
    // Add anything else here if needed in the future (like Vectors), 
    // it won't affect network bandwidth since this is offline!
}

public interface IStatusEffect
{
    // The core Execution Loop. Runs every tick in FUN.
    // We pass 'ref' to the effectData so if the logic needs to update the EndTick, it can!
    void Tick( NetworkRunner runner, PhysicsObject target, NetworkedMemoryAllocator memory, ref ActiveStatusEffectData effectData, ref StatusEffectPropertyModifiers currentMods);

    // Returns TRUE if absorbed. Returns FALSE if they should coexist.
    bool TryStack( NetworkRunner runner, ref ActiveStatusEffectData existingEffect, NetworkedMemoryAllocator memory, ProposedEffectPayload newPayload);

    void OnAllocated(NetworkedMemoryAllocator memory, ref ActiveStatusEffectData newEffectData, ProposedEffectPayload payload);
    void OnRemoved(NetworkedMemoryAllocator memory, ref ActiveStatusEffectData effectData);
}

