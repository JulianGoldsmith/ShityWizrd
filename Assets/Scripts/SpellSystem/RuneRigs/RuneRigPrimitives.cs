using System;
using Fusion;

public static class RuneRigLimits
{
    public const int MaxNodes = 64;
    public const int MaxBayCapacity = 64;
}

public static class RuneParent
{
    public const byte Root = 254;
    public const byte None = 255;

    public static bool IsNodeIndex(byte value)
    {
        return value < Root;
    }
}

public static class RuneConnection
{
    public const byte BayIndexMask = 0b00111111;
    public const byte LockedMask = 0b01000000;
    public const byte ReservedMask = 0b10000000;

    public static byte Pack(byte bayIndex, bool locked)
    {
        if (bayIndex >= RuneRigLimits.MaxBayCapacity)
            throw new ArgumentOutOfRangeException(nameof(bayIndex), $"Bay index must be between 0 and {RuneRigLimits.MaxBayCapacity - 1}.");

        return (byte)(bayIndex | (locked ? LockedMask : 0));
    }

    public static byte GetBayIndex(byte packedConnection)
    {
        return (byte)(packedConnection & BayIndexMask);
    }

    public static bool IsLocked(byte packedConnection)
    {
        return (packedConnection & LockedMask) != 0;
    }

    public static bool HasReservedFlag(byte packedConnection)
    {
        return (packedConnection & ReservedMask) != 0;
    }

    public static byte SetLocked(byte packedConnection, bool locked)
    {
        byte result = (byte)(packedConnection & ~LockedMask);
        return locked ? (byte)(result | LockedMask) : result;
    }
}

[Serializable]
public struct RuneNodeData : INetworkStruct
{
    public ushort RuneDefinitionId;
    public byte ParentNodeIndex;
    public byte ParentBayConnection;
    public byte BayCapacity;

    public byte ParentBayIndex => RuneConnection.GetBayIndex(ParentBayConnection);
    public bool ConnectionIsLocked => RuneConnection.IsLocked(ParentBayConnection);
    public bool IsLooseRoot => ParentNodeIndex == RuneParent.None;
    public bool IsBlueprintRoot => ParentNodeIndex == RuneParent.Root;

    public RuneNodeData(ushort runeDefinitionId, byte parentNodeIndex, byte parentBayConnection, byte bayCapacity)
    {
        RuneDefinitionId = runeDefinitionId;
        ParentNodeIndex = parentNodeIndex;
        ParentBayConnection = parentBayConnection;
        BayCapacity = bayCapacity;
    }

    public static RuneNodeData CreateLooseRoot(ushort runeDefinitionId, byte bayCapacity)
    {
        return new RuneNodeData(runeDefinitionId, RuneParent.None, 0, bayCapacity);
    }

    public static RuneNodeData CreateBlueprintRoot(ushort runeDefinitionId, byte bayCapacity)
    {
        return new RuneNodeData(runeDefinitionId, RuneParent.Root, 0, bayCapacity);
    }

    public static RuneNodeData CreateChild(ushort runeDefinitionId, byte parentNodeIndex, byte parentBayIndex, bool locked, byte bayCapacity)
    {
        return new RuneNodeData(runeDefinitionId, parentNodeIndex, RuneConnection.Pack(parentBayIndex, locked), bayCapacity);
    }
}

[Serializable]
public struct RuneRigData
{
    public RuneNodeData[] Nodes;

    public int NodeCount => Nodes == null ? 0 : Nodes.Length;

    public RuneRigData(RuneNodeData[] nodes)
    {
        Nodes = nodes;
    }
}

public enum RuneRigRootMode
{
    LooseRig,
    Blueprint
}