using Fusion;
using UnityEngine;

public enum EInputButton
{
    LEFT_CLICK = 0,
    RIGHT_CLICK = 1,
    JUMP = 2,
    PICKUP = 3,
    DROP = 4,
    SPRINT = 5,
    ADD = 6, 
    SUBTRACT = 7,
    SELF_BONK = 8,
    UN_SELF_BONK = 9,
    TEST_COUNT = 10,
}
public struct NetworkInputData : INetworkInput
{
    public Vector3 direction;
    public Quaternion lookRotation;
    public NetworkButtons buttons;

    public NetworkId grabControlItemId;
    public float grabTargetDistance;
    public Quaternion grabRotationOffset;

    public Vector2 yawpitch;
    public float scroll;

    public Vector3 dragTargetPos; 
    public Vector3 dragFacingDir;

    public NetworkInteractionTarget interactionTarget;

    public uint runeRigSpawnCommand;
}

public enum InteractionTargetType : byte
{
    None,
    Item,
    RuneNode,
    RuneBay
}

public struct NetworkInteractionTarget : INetworkStruct
{
    private const int TypeShift = 0;
    private const int PartShift = 3;
    private const int BayShift = 11;

    private const uint TypeMask = 0b111;
    private const uint PartMask = 0xFF;
    private const uint BayMask = 0x3F;

    public NetworkId ObjectId;
    public uint PackedData;

    public InteractionTargetType Type => (InteractionTargetType)((PackedData >> TypeShift) & TypeMask);
    public byte PartIndex => (byte)((PackedData >> PartShift) & PartMask);
    public byte BayIndex => (byte)((PackedData >> BayShift) & BayMask);
    public bool IsValid => ObjectId.IsValid && Type != InteractionTargetType.None;

    public static NetworkInteractionTarget CreateItem(NetworkId objectId)
    {
        return Create(objectId, InteractionTargetType.Item, 0, 0);
    }

    public static NetworkInteractionTarget CreateRuneNode(NetworkId objectId, byte nodeIndex)
    {
        return Create(objectId, InteractionTargetType.RuneNode, nodeIndex, 0);
    }

    public static NetworkInteractionTarget CreateRuneBay(NetworkId objectId, byte nodeIndex, byte bayIndex)
    {
        return Create(objectId, InteractionTargetType.RuneBay, nodeIndex, bayIndex);
    }

    private static NetworkInteractionTarget Create(NetworkId objectId, InteractionTargetType type, byte partIndex, byte bayIndex)
    {
        return new NetworkInteractionTarget
        {
            ObjectId = objectId,
            PackedData =
                ((uint)type << TypeShift) |
                ((uint)partIndex << PartShift) |
                ((uint)bayIndex << BayShift)
        };
    }
}
