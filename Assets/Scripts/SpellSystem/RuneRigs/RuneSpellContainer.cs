using Fusion;
using UnityEngine;

[RequireComponent(typeof(EquipableItem))]
public class RuneSpellContainer : MonoBehaviour
{
    [Header("Feed Points")]
    public Transform IntakePoint;
    public Transform EjectionPoint;

    [Header("Feed Settings")]
    [Min(0.1f)] public float FeedRange = 4f;
    public float EjectionSpeed = 2f;

    private EquipableItem _item;

    private void Awake()
    {
        _item = GetComponent<EquipableItem>();
    }

    public bool TryCommitFeed(NetworkInteractionTarget target)
    {
        if (_item == null || !_item.HasStateAuthority)
            return false;

        if (_item.PrimarySpellID.NotNull())
            return TryEject();

        return TryAbsorb(target);
    }

    private bool TryAbsorb(NetworkInteractionTarget target)
    {
        if (!target.IsValid || target.Type != InteractionTargetType.RuneNode)
            return false;

        if (!_item.Runner.TryFindObject(target.ObjectId, out NetworkObject targetObject))
            return false;

        if (!targetObject.TryGetComponent(out RuneRigObject targetRig) || !targetRig.HasRigData)
            return false;

        if (!targetRig.Object.HasStateAuthority)
            return false;

        RuneObject selectedRune = targetRig.GetRuneObject(target.PartIndex);

        if (selectedRune == null)
            return false;

        Vector3 intakePosition = IntakePoint != null ? IntakePoint.position : _item.transform.position;

        if ((selectedRune.transform.position - intakePosition).sqrMagnitude > FeedRange * FeedRange)
        {
            Debug.LogWarning($"[RuneFeed] Rig was outside feed range.", _item);
            return false;
        }

        RuneRigData looseRig = targetRig.GetRigDataCopy();

        if (!RuneSpellBlueprintBuilder.TryCreateBlueprint(looseRig, out RuneSpellBlueprintData blueprint, out string error))
        {
            Debug.LogWarning($"[RuneFeed] Could not build blueprint: {error}", _item);
            return false;
        }

        PlayerRef author = _item.HoldingPlayer != null ? _item.HoldingPlayer.InputAuthority : _item.Object.InputAuthority;

        if (author == PlayerRef.None)
        {
            Debug.LogWarning("[RuneFeed] Weapon has no valid player authority.", _item);
            return false;
        }

        SpellGraphId spellId = SpellStateManager.instance.RegisterRuneSpellOnHost(blueprint, author);

        if (spellId.IsNull())
            return false;

        int absorbedNodeCount = targetRig.NodeCount;

        _item.PrimarySpellID = spellId;
        targetRig.IsConsumed = true;
        _item.Runner.Despawn(targetRig.Object);

        Debug.Log($"[RuneFeed] Absorbed {absorbedNodeCount} runes into spell {spellId.BlueprintNumber}.", _item);
        return true;
    }

    private bool TryEject()
    {
        SpellGraphId spellId = _item.PrimarySpellID;

        if (!SpellStateManager.instance.TryGetRuneSpellBlueprint(spellId, out RuneSpellBlueprintData blueprint))
        {
            Debug.LogWarning($"[RuneFeed] Spell {spellId.BlueprintNumber} is not a rune blueprint.", _item);
            return false;
        }

        if (!RuneSpellBlueprintBuilder.TryCreateLooseRig(blueprint, out RuneRigData looseRig, out string error))
        {
            Debug.LogWarning($"[RuneFeed] Could not reconstruct loose rig: {error}", _item);
            return false;
        }

        if (ObjectBufferAllocator.Instance == null)
        {
            Debug.LogWarning("[RuneFeed] No ObjectBufferAllocator exists.", _item);
            return false;
        }

        PlayerRef bufferOwner = _item.HoldingPlayer != null ? _item.HoldingPlayer.InputAuthority : _item.Object.InputAuthority;
        ObjectBuffer runeBuffer = ObjectBufferAllocator.Instance.GetRuneRigBuffer(bufferOwner);

        if (runeBuffer == null)
        {
            Debug.LogWarning($"[RuneFeed] Player {bufferOwner} has no rune rig buffer.", _item);
            return false;
        }

        Transform ejectTransform = EjectionPoint != null ? EjectionPoint : (_item.projectileSpawnPoint != null ? _item.projectileSpawnPoint : _item.transform);
        NetworkObject ejectedObject = runeBuffer.GetBufferedObject(ejectTransform.position, ejectTransform.rotation, out _);

        if (ejectedObject == null)
        {
            Debug.LogWarning("[RuneFeed] Rune rig buffer returned no object.", _item);
            return false;
        }

        if (!ejectedObject.TryGetComponent(out RuneRigObject ejectedRig))
        {
            Debug.LogError("[RuneFeed] Buffered object has no RuneRigObject.", ejectedObject);

            if (ejectedObject.HasStateAuthority)
                _item.Runner.Despawn(ejectedObject);

            return false;
        }

        if (!ejectedRig.TryWriteRigData(looseRig, out string writeError))
        {
            Debug.LogWarning($"[RuneFeed] Could not initialize ejected rig: {writeError}", _item);

            if (ejectedObject.HasStateAuthority)
                _item.Runner.Despawn(ejectedObject);

            return false;
        }

        if (ejectedRig.rb != null)
        {
            Vector3 holderVelocity = _item.activeHolder != null ? _item.activeHolder.calculatedFixedVel : Vector3.zero;
            ejectedRig.rb.linearVelocity = holderVelocity + ejectTransform.forward * EjectionSpeed;
            ejectedRig.rb.angularVelocity = Vector3.zero;
            ejectedRig.rb.WakeUp();
        }

        _item.PrimarySpellID = default;

        Debug.Log($"[RuneFeed] Ejected spell {spellId.BlueprintNumber} as {looseRig.NodeCount} runes.", _item);
        return true;
    }
}