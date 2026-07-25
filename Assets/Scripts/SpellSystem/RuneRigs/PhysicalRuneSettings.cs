using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PhysicalRuneSettings
{
    [Tooltip("The visual and collider prefab used when this rune exists in a physical rig.")]
    public GameObject PhysicalPrefab;

    [Range(0, RuneRigLimits.MaxBayCapacity)]
    [Tooltip("The largest bay capacity permitted for an instance of this rune.")]
    public int MaximumBayCapacity;

    [Tooltip("If empty, every capacity from zero to Maximum Bay Capacity is allowed.")]
    public List<byte> AllowedBayCapacities = new List<byte>();

    [Tooltip("Rune categories which may be attached to this rune's bays.")]
    public List<NodeType> AcceptedChildTypes = new List<NodeType>();

    public bool IsCapacityAllowed(byte capacity)
    {
        if (MaximumBayCapacity < 0 || MaximumBayCapacity > RuneRigLimits.MaxBayCapacity)
            return false;

        if (capacity > MaximumBayCapacity)
            return false;

        return AllowedBayCapacities == null || AllowedBayCapacities.Count == 0 || AllowedBayCapacities.Contains(capacity);
    }

    public bool AcceptsChild(NodeType childType)
    {
        return AcceptedChildTypes != null && AcceptedChildTypes.Contains(childType);
    }
}