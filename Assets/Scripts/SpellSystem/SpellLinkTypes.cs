using Fusion;
using UnityEngine;

public enum LinkEndpointKind : byte
{
    NetworkBody,
    WorldPoint
}

public enum LinkPhase : byte
{
    None,
    WaitingForB,
    Active
}

public struct LinkEndpoint : INetworkStruct
{
    public LinkEndpointKind Kind;
    public NetworkId ObjectId;
    public Vector3 Anchor;
    public Quaternion AnchorRotation;
}

public struct ActiveLinkState : INetworkStruct
{
    public LinkPhase Phase;
    public ActiveCastID CastId;
    public NetworkId ManifestationCoreId;

    public LinkEndpoint A;
    public LinkEndpoint B;

    public int StartTick;
    public int EndTick;

    public float MaximumLength;
    public float BreakForce;
    public float Compliance;
    public float Damping;
    public float SpellLoad;

    public bool Exists => Phase != LinkPhase.None;
    public bool IsActive => Phase == LinkPhase.Active;
}