using Fusion;
using UnityEngine;

public enum EInputButton
{
    LEFT_CLICK = 0,
    RIGHT_CLICK = 1,
    JUMP = 2,
    PICKUP = 3,
    RELEASE = 4,
    SPRINT = 5,
    ADD = 6, 
    SUBTRACT = 7,
    SELF_BONK = 8,
    UN_SELF_BONK = 9,
    TEST_COUNT = 10,
    FEED = 11,
    ROTATE = 12,
    SLOT_1 = 13,
    SLOT_2 = 14,
    SLOT_3 = 15
}
public struct NetworkInputData : INetworkInput
{
    public Vector3 direction;
    public Quaternion lookRotation;
    public NetworkButtons buttons;

    public NetworkId grabControlItemId;
    public float grabTargetDistance;
    public Quaternion grabRotationOffset;

    public NetworkId levitationRotationItemId;
    public Quaternion levitationTargetRotation;

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
    RuneBay,
    PhysicsBody,
    WorldPoint
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
    public Vector3 HitPoint;
    public Vector3 HitNormal;

    public InteractionTargetType Type => (InteractionTargetType)((PackedData >> TypeShift) & TypeMask);
    public byte PartIndex => (byte)((PackedData >> PartShift) & PartMask);
    public byte BayIndex => (byte)((PackedData >> BayShift) & BayMask);
    public bool IsValid => Type == InteractionTargetType.WorldPoint || (ObjectId.IsValid && Type != InteractionTargetType.None);

    public static NetworkInteractionTarget CreateItem(NetworkId objectId, Vector3 hitPoint, Vector3 hitNormal)
    {
        return Create(objectId, InteractionTargetType.Item, 0, 0, hitPoint, hitNormal);
    }

    public static NetworkInteractionTarget CreateRuneNode(NetworkId objectId, byte nodeIndex, Vector3 hitPoint, Vector3 hitNormal)
    {
        return Create(objectId, InteractionTargetType.RuneNode, nodeIndex, 0, hitPoint, hitNormal);
    }

    public static NetworkInteractionTarget CreateRuneBay(NetworkId objectId, byte nodeIndex, byte bayIndex)
    {
        return Create(objectId, InteractionTargetType.RuneBay, nodeIndex, bayIndex, default, default);
    }

    public static NetworkInteractionTarget CreatePhysicsBody(NetworkId objectId, Vector3 hitPoint, Vector3 hitNormal)
    {
        return Create(objectId, InteractionTargetType.PhysicsBody, 0, 0, hitPoint, hitNormal);
    }

    public static NetworkInteractionTarget CreateWorldPoint(Vector3 hitPoint, Vector3 hitNormal)
    {
        return Create(default, InteractionTargetType.WorldPoint, 0, 0, hitPoint, hitNormal);
    }

    private static NetworkInteractionTarget Create(NetworkId objectId, InteractionTargetType type, byte partIndex, byte bayIndex, Vector3 hitPoint, Vector3 hitNormal)
    {
        return new NetworkInteractionTarget
        {
            ObjectId = objectId,
            PackedData = ((uint)type << TypeShift) | ((uint)partIndex << PartShift) | ((uint)bayIndex << BayShift),
            HitPoint = hitPoint,
            HitNormal = hitNormal
        };
    }
}