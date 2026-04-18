using Fusion;
using System;

public struct ActiveCastID : INetworkStruct, IEquatable<ActiveCastID>
{
    public NetworkId CasterId;
    public int CastNumber;

    public ActiveCastID(NetworkId casterId, int castNumber)
    {
        CasterId = casterId;
        CastNumber = castNumber;
    }

    public bool Equals(ActiveCastID other)
    {
        return CasterId == other.CasterId && CastNumber == other.CastNumber;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(CasterId, CastNumber);
    }

    public bool IsValid => CasterId.IsValid;
}