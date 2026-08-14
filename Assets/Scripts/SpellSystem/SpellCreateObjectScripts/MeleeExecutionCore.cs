using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CastActionController))]
public class MeleeExecutionCore : NetworkBehaviour, ISpellExecutionCore
{
    private const int MaxHitTargets = 16;
    private const int IntMemoryCapacity = 8;
    private const int FloatMemoryCapacity = 8;
    private const int VectorMemoryCapacity = 4;

    [Networked] public ActiveCastID ActiveCastID { get; set; }
    [Networked] public SpellGraphId BlueprintID { get; set; }
    [Networked, Capacity(MaxHitTargets)] public NetworkArray<NetworkId> HitTargetIds { get; }

    public CoreContext Context { get; set; }
    public int SpawnTick { get; set; }
    public Vector3 Position => _sourceItem != null ? _sourceItem.transform.position : transform.position;
    public Quaternion Rotation => _sourceItem != null ? _sourceItem.transform.rotation : transform.rotation;
    public PlayerRef InputAuthority => Object.InputAuthority;
    public GameObject SourceObject => _sourceItem != null ? _sourceItem.gameObject : gameObject;
    public NetworkId CoreNetworkId => Object != null ? Object.Id : default;

    private bool _excludeSelfCollisions;

    public Dictionary<int, GameObject> ActiveVisuals { get; } = new Dictionary<int, GameObject>();

    private readonly List<PendingMeleeHit> _pendingHits = new List<PendingMeleeHit>();
    private readonly List<SpellTriggerInfo> _triggerInfos = new List<SpellTriggerInfo>();
    private readonly int[] _intMemory = new int[IntMemoryCapacity];
    private readonly float[] _floatMemory = new float[FloatMemoryCapacity];
    private readonly Vector3[] _vectorMemory = new Vector3[VectorMemoryCapacity];
    private int _boolMemory;

    private RunnerSimulatePhysics3D _physicsSimulator;
    private EquipableItem _sourceItem;
    private ItemHitBox _activeHitBox;
    private ActiveCastID _scheduledCastID;

    public override void Spawned()
    {
        if (!Runner.TryGetComponent(out _physicsSimulator)) Debug.LogError("[MeleeExecutionCore] RunnerSimulatePhysics3D was not found.", this);
    }

    public void BeginSwing(ActiveCastID castID, SpellGraphId blueprintID, EquipableItem item, ItemHitBox hitBox, SpellState state, int startTick, bool hitBoxActive, bool excludeSelfCollisions)
    {
        if (!castID.IsValid || blueprintID.IsNull() || item == null || hitBox == null || state == null) return;
        _excludeSelfCollisions = excludeSelfCollisions;

        if (!ActiveCastID.Equals(castID))
        {
            if (_activeHitBox != null) _activeHitBox.SetActive(this, false);

            ActiveCastID = castID;
            BlueprintID = blueprintID;
            SpawnTick = startTick;
            ClearHitTargets();
            ClearMemory();
        }

        _sourceItem = item;
        _activeHitBox = hitBox;
        Context = new CoreContext
        {
            SpawnPosition = item.transform.position,
            CastChargeLevel = state.CastChargeLevel,
            OriginalCaster = Object.Id,
            TriggerVector = item.networkedRB != null && item.networkedRB.Rigidbody != null ? item.networkedRB.Rigidbody.linearVelocity : Vector3.zero
        };

        _pendingHits.Clear();
        hitBox.SetActive(this, hitBoxActive);
        if (hitBoxActive && _physicsSimulator != null)
        {
            _scheduledCastID = castID;
            _physicsSimulator.QueueAfterSimulationCallback(ProcessHitsAfterPhysics);
        }
    }

    public void AccumulateHit(NetworkId targetID, NetworkId ragdollRootID, GameObject target, Vector3 point, Vector3 normal, Vector3 velocity)
    {
        if (!ActiveCastID.IsValid || !targetID.IsValid || target == null) return;
        if (_excludeSelfCollisions && ragdollRootID.IsValid && ragdollRootID == ActiveCastID.CasterId) return;

        _pendingHits.Add(new PendingMeleeHit
        {
            TargetID = targetID,
            Target = target,
            Point = point,
            Normal = normal,
            Velocity = velocity
        });
    }

    public void EndSwing(ActiveCastID expectedCastID)
    {
        if (!ActiveCastID.IsValid || !ActiveCastID.Equals(expectedCastID)) return;

        if (_activeHitBox != null) _activeHitBox.SetActive(this, false);

        _pendingHits.Clear();
        _triggerInfos.Clear();
        _sourceItem = null;
        _activeHitBox = null;
        ActiveCastID = default;
        BlueprintID = default;
        SpawnTick = 0;
        Context = default;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_activeHitBox != null) _activeHitBox.SetActive(this, false);
        base.Despawned(runner, hasState);
    }

    private void ProcessHitsAfterPhysics()
    {
        if (!ActiveCastID.Equals(_scheduledCastID)) return;
        if (!ActiveCastID.IsValid || _pendingHits.Count == 0) return;

        ActiveSpell activeSpell = SpellStateManager.instance != null ? SpellStateManager.instance.GetActiveSpell(ActiveCastID) : null;
        IEffect effect = GetEffect();

        if (activeSpell == null || effect == null)
        {
            _pendingHits.Clear();
            return;
        }

        _pendingHits.Sort((a, b) => a.TargetID.Raw.CompareTo(b.TargetID.Raw));
        _triggerInfos.Clear();

        for (int i = 0; i < _pendingHits.Count; i++)
        {
            PendingMeleeHit hit = _pendingHits[i];
            if (_sourceItem != null && _sourceItem.Object != null && hit.TargetID == _sourceItem.Object.Id) continue;
            if (!TryRecordHit(hit.TargetID)) continue;

            Vector3 direction = hit.Velocity.sqrMagnitude > 0.0001f ? hit.Velocity.normalized : hit.Normal;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

            _triggerInfos.Add(new SpellTriggerInfo
            {
                IsValid = true,
                IsCast = false,
                Source = SourceObject,
                State = activeSpell.State,
                HasOverridePosition = true,
                TriggerPoint = hit.Point,
                TriggerRotation = Quaternion.LookRotation(direction),
                TriggerNormal = hit.Normal.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(hit.Normal) : Quaternion.identity,
                TriggerVector = hit.Velocity,
                HitObject = hit.Target
            });
        }

        if (_triggerInfos.Count > 0) effect.Execute(this, _triggerInfos);
        _pendingHits.Clear();
    }

    private IEffect GetEffect()
    {
        RuntimeSpell runtimeSpell = SpellBlueprintLibrary.Get(BlueprintID);
        if (runtimeSpell == null) return null;

        IRuntimeNode root = runtimeSpell.RootNode;
        if (root is RuntimeEntryPoint entryPoint)
        {
            if (entryPoint.ExpectedType != EntryPointType.Effect) return null;
            root = entryPoint.ConnectedLogic;
        }

        return root as IEffect;
    }

    private bool TryRecordHit(NetworkId targetID)
    {
        for (int i = 0; i < MaxHitTargets; i++)
        {
            NetworkId existingID = HitTargetIds.Get(i);
            if (existingID == targetID) return false;

            if (!existingID.IsValid)
            {
                HitTargetIds.Set(i, targetID);
                return true;
            }
        }

        return false;
    }

    private void ClearHitTargets()
    {
        for (int i = 0; i < MaxHitTargets; i++) HitTargetIds.Set(i, default);
    }

    private void ClearMemory()
    {
        for (int i = 0; i < IntMemoryCapacity; i++) _intMemory[i] = 0;
        for (int i = 0; i < FloatMemoryCapacity; i++) _floatMemory[i] = 0f;
        for (int i = 0; i < VectorMemoryCapacity; i++) _vectorMemory[i] = Vector3.zero;
        _boolMemory = 0;
    }

    public bool TryGetCoreComponent<T>(out T component) where T : class
    {
        return TryGetComponent(out component);
    }

    public bool GetBool(int bitIndex) => (_boolMemory & (1 << bitIndex)) != 0;

    public void SetBool(int bitIndex, bool value)
    {
        if (value) _boolMemory |= 1 << bitIndex;
        else _boolMemory &= ~(1 << bitIndex);
    }

    public int GetInt(int index) => _intMemory[index];
    public void SetInt(int index, int value) => _intMemory[index] = value;
    public float GetFloat(int index) => _floatMemory[index];
    public void SetFloat(int index, float value) => _floatMemory[index] = value;
    public Vector3 GetVector(int index) => _vectorMemory[index];
    public void SetVector(int index, Vector3 value) => _vectorMemory[index] = value;
}

public struct PendingMeleeHit
{
    public NetworkId TargetID;
    public GameObject Target;
    public Vector3 Point;
    public Vector3 Normal;
    public Vector3 Velocity;
}
