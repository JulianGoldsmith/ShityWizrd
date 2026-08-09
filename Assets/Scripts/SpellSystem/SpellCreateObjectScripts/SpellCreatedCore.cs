using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpellCreatedCore : NetworkBehaviour, ISpellExecutionCore, IBufferableComponent
{
    private const int IntMemoryCapacity = 8;
    private const int FloatMemoryCapacity = 8;
    private const int VectorMemoryCapacity = 4;

    [Header("Generated Payload")]
    public Transform PhysicsContainer;
    public Transform VisualContainer;

    public static int ActiveCount;
    public static int NetworkWritesThisSecond;

    [Networked] public ActiveCastID ActiveCastID { get; set; }
    [Networked] public SpellGraphId BlueprintID { get; set; }
    [Networked] public int NodeArrayIndex { get; set; }
    [Networked] public int SpawnTick { get; set; }
    [Networked] public TickTimer LifetimeTimer { get; set; }
    [Networked] public CoreContext Context { get; set; }
    [Networked] public Vector3 NetworkVelocity { get; set; }
    [Networked, Capacity(IntMemoryCapacity)] public NetworkArray<int> IntMemory { get; }
    [Networked, Capacity(FloatMemoryCapacity)] public NetworkArray<float> FloatMemory { get; }
    [Networked, Capacity(VectorMemoryCapacity)] public NetworkArray<Vector3> VectorMemory { get; }
    [Networked] public int BoolMemory { get; set; }
    [Networked] public int GlobalBufferIndex { get; set; }

    public List<PendingContact> TickContacts = new List<PendingContact>();
    public Dictionary<int, GameObject> ActiveVisuals { get; private set; } = new Dictionary<int, GameObject>();

    private BufferedObject _bufferedObject;
    private NetworkRigidbody3D _networkRigidbody;
    private NetworkTransform _networkTransform;
    private Rigidbody _rigidbody;
    private PhysicsObject _physicsObject;
    private GameObject _attachedComponents;
    private GameObject _attachedVisual;
    private CoreExecutionPlan _myPlan;
    private GameObject _payloadPrefab;
    private SpellGraphId _payloadBlueprintId;
    private ActiveCastID _runtimeCastId;
    private int _payloadNodeArrayIndex = -1;
    private float _configuredLifetime;
    private ActiveSpell _tokenOwnerSpell;
    private ActiveCastID _tokenOwnerCastId;
    private bool _isInitialized;
    private bool _isCountedActive;

    public Vector3 Position => transform.position;
    public Quaternion Rotation => transform.rotation;
    public PlayerRef InputAuthority => Object.InputAuthority;
    public GameObject SourceObject => gameObject;
    public NetworkId CoreNetworkId => Object != null ? Object.Id : default;
    public bool IsLocallyAwake => _bufferedObject != null ? _bufferedObject.IsAwake : _isCountedActive;
    public bool IsRuntimeActive => IsLocallyAwake && _isInitialized;

    public bool TryGetCoreComponent<T>(out T component) where T : class
    {
        return TryGetComponent(out component);
    }

    public override void Spawned()
    {
        _networkRigidbody = GetComponent<NetworkRigidbody3D>();
        _networkTransform = GetComponent<NetworkTransform>();
        _rigidbody = GetComponent<Rigidbody>();
        _physicsObject = GetComponent<PhysicsObject>();

        if (_bufferedObject == null && TryGetComponent(out BufferedObject bufferedObject)) BindBufferedObject(bufferedObject);
    }

    public override void FixedUpdateNetwork()
    {
        ReconcileUnbufferedWakeFromTick();
        if (!IsLocallyAwake) return;
        if (!EnsureLocalRuntimeReady()) return;

        ReconcileTokenOwnership();

        if (LifetimeTimer.Expired(Runner))
        {
            DeactivateCore();
            return;
        }

        foreach (IBehaviour behaviour in _myPlan.Behaviours)
            behaviour.Tick(this, Runner.DeltaTime);

        for (int i = _myPlan.Triggers.Count - 1; i >= 0; i--)
        {
            ITrigger trigger = _myPlan.Triggers[i];

            if (!trigger.Tick(this, Runner.DeltaTime, out List<SpellTriggerInfo> hitInfos))
                continue;

            if (trigger is not RuntimeTriggerBase runtimeTrigger)
                continue;

            foreach (IRuntimeNode outcome in runtimeTrigger.Outcomes)
            {
                if (outcome is IEffect effect)
                    effect.Execute(this, hitInfos);

                if (outcome is IRuntimeCore downstreamCore)
                {
                    foreach (SpellTriggerInfo hitInfo in hitInfos)
                        downstreamCore.ExecuteCore(hitInfo);
                }

                if (!IsLocallyAwake)
                    return;
            }
        }

        TickContacts.Clear();
    }

    public override void Render()
    {
        if (!IsLocallyAwake || !_isInitialized || _myPlan == null) return;

        foreach (IBehaviour behaviour in _myPlan.Behaviours)
            behaviour.TickVFX(this);

        foreach (ITrigger trigger in _myPlan.Triggers)
            trigger.TickVFX(this);
    }

    public bool Initialize(ActiveCastID castId, SpellGraphId blueprintId, CoreContext initialContext, int arrayIndex, int globalBufferIndex, Vector3 spawnPosition, Quaternion spawnRotation)
    {
        if (Object == null || !Object.IsValid) return false;

        if (_bufferedObject != null && _bufferedObject.IsAwake)
        {
            Debug.LogError("[SpellCreatedCore] A buffer returned an already-awake spell core.", this);
            return false;
        }

        GlobalBufferIndex = globalBufferIndex;
        NodeArrayIndex = arrayIndex;
        ActiveCastID = castId;
        BlueprintID = blueprintId;
        Context = initialContext;
        SpawnTick = Runner.Tick;
        NetworkVelocity = Vector3.zero;

        PreparePose(spawnPosition, spawnRotation);
        ResetActivationMemory();

        if (!EnsureCastHydrated() || !EnsurePayloadHydrated())
        {
            if (HasStateAuthority) Runner.Despawn(Object);
            return false;
        }

        ReconcileTokenOwnership();

        if (_bufferedObject != null)
        {
            _bufferedObject.BeginWakeInitialization();
            ResetPhysicsVelocity();
            InitializeActivationTick();
            _bufferedObject.CompleteWakeInitialization();
        }
        else
        {
            InitializeActivationTick();
            ActivateLocalState();
        }

        return true;
    }

    public void DeactivateCore()
    {
        if (!IsLocallyAwake) return;

        if (_bufferedObject != null)
            _bufferedObject.Sleep();
        else
            DeactivateLocalState();

        if (HasStateAuthority && Object != null && Object.IsValid)
            Runner.Despawn(Object);
    }

    public void OnCollisionEnter(Collision collision)
    {
        if (!IsLocallyAwake || !_isInitialized || _myPlan == null) return;
        if (collision.contactCount == 0) return;

        GameObject hitObject = SpellSystemHelpers.GetHitGameObject(collision.collider);
        TickContacts.Add(new PendingContact
        {
            Target = hitObject,
            Point = collision.contacts[0].point,
            Normal = collision.contacts[0].normal
        });
    }

    public void OnTriggerEnter(Collider other)
    {
        if (!IsLocallyAwake || !_isInitialized || _myPlan == null) return;

        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitNormal = (transform.position - other.transform.position).normalized;
        if (hitNormal == Vector3.zero) hitNormal = Vector3.up;

        TickContacts.Add(new PendingContact
        {
            Target = SpellSystemHelpers.GetHitGameObject(other),
            Point = hitPoint,
            Normal = hitNormal
        });
    }

    public void BindBufferedObject(BufferedObject bufferedObject)
    {
        _bufferedObject = bufferedObject;
    }

    public void OnBufferedWake(int wakeTick, bool isActivationTick)
    {
        ActivateLocalState();
        EnsureLocalRuntimeReady();
        ReconcileTokenOwnership();
    }

    public void OnBufferedSleep(int sleepTick)
    {
        DeactivateLocalState();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        DeactivateLocalState();
        base.Despawned(runner, hasState);
    }

    private bool EnsureLocalRuntimeReady()
    {
        bool identityChanged = !_runtimeCastId.Equals(ActiveCastID) || !_payloadBlueprintId.Equals(BlueprintID) || _payloadNodeArrayIndex != NodeArrayIndex;
        if (_isInitialized && !identityChanged) return true;
        if (!EnsureCastHydrated()) return false;
        return EnsurePayloadHydrated();
    }

    private bool EnsureCastHydrated()
    {
        if (!ActiveCastID.IsValid || SpellStateManager.instance == null) return false;
        if (SpellStateManager.instance.GetActiveSpell(ActiveCastID) != null) return true;
        if (SpellBlueprintLibrary.Get(BlueprintID) == null) return false;
        if (!Runner.TryFindObject(ActiveCastID.CasterId, out NetworkObject casterObject)) return false;
        if (!casterObject.TryGetComponent(out ActiveCastTracker tracker)) return false;

        NetworkCastData syncedData = tracker.GetCastData(ActiveCastID);

        if (!syncedData.CastID.IsValid)
            return false;

        SpellGraph legacyBlueprint = SpellStateManager.instance.GetSpellGraph(BlueprintID);
        SpellState proxyState = legacyBlueprint != null ? new SpellState(Runner, syncedData, legacyBlueprint) : new SpellState(Runner, syncedData);
        ActiveSpell activeSpell = legacyBlueprint != null ? new ActiveSpell(ActiveCastID, legacyBlueprint, proxyState) : new ActiveSpell(ActiveCastID, BlueprintID, proxyState);
        SpellStateManager.instance.RegisterNewCast(ActiveCastID, activeSpell);
        return true;
    }

    private bool EnsurePayloadHydrated()
    {
        if (SpellStateManager.instance == null) return false;
        RuntimeSpell runtimeSpell = SpellBlueprintLibrary.Get(BlueprintID);
        if (runtimeSpell == null) return false;

        IRuntimeNode runtimeNode = runtimeSpell.GetNode(NodeArrayIndex);
        if (runtimeNode is not RuntimeObjectCore runtimeCore) return false;

        bool payloadMissing = runtimeCore.AttachedSpellComponentsPrefab != null && _attachedComponents == null;
        bool payloadChanged = _myPlan == null || !_payloadBlueprintId.Equals(BlueprintID) || _payloadNodeArrayIndex != NodeArrayIndex || _payloadPrefab != runtimeCore.AttachedSpellComponentsPrefab || payloadMissing;

        if (payloadChanged)
        {
            if (_isInitialized) CleanupPlanVFX();
            DestroyCurrentPayload();
            _myPlan = new CoreExecutionPlan
            {
                Behaviours = new List<IBehaviour>(runtimeCore.Behaviours),
                Triggers = new List<ITrigger>(runtimeCore.Triggers)
            };
            _payloadBlueprintId = BlueprintID;
            _payloadNodeArrayIndex = NodeArrayIndex;
            _payloadPrefab = runtimeCore.AttachedSpellComponentsPrefab;
            CreatePayload(runtimeCore.AttachedSpellComponentsPrefab);
        }
        else
        {
            if (_attachedComponents != null) _attachedComponents.SetActive(true);
            if (_attachedVisual != null) _attachedVisual.SetActive(true);
        }

        SpellState spellState = SpellStateManager.instance.GetActiveSpell(ActiveCastID)?.State;
        SpellTriggerInfo evaluationInfo = new SpellTriggerInfo(false, gameObject, spellState, Context.SpawnPosition, transform.rotation, Context.TriggerVector, null);
        float finalSize = runtimeCore.size.GetValue(evaluationInfo);
        _configuredLifetime = runtimeCore.lifetime.GetValue(evaluationInfo);
        ushort finalMaterial = runtimeCore.material.GetValue(evaluationInfo);
        AttatchedSpellComponent attachedScript = _attachedComponents != null ? _attachedComponents.GetComponent<AttatchedSpellComponent>() : null;

        if (attachedScript != null)
            attachedScript.parentSpellCore = this;

        if (TryGetComponent(out PhysicsObjectProperties properties))
        {
            properties.Size = finalSize;
            properties.Material_label = finalMaterial;

            if (payloadChanged && attachedScript != null && _physicsObject != null)
                _physicsObject.RegisterAttachedVisuals(attachedScript, properties.physicsobjectmaterial);

            if (_physicsObject != null)
            {
                _physicsObject.InitialisePhysicsObject();

                if (_physicsObject.rb != null)
                {
                    _physicsObject.rb.ResetCenterOfMass();
                    _physicsObject.rb.ResetInertiaTensor();
                }
            }
        }

        _runtimeCastId = ActiveCastID;
        _isInitialized = true;
        return true;
    }

    private void CreatePayload(GameObject payloadPrefab)
    {
        if (payloadPrefab == null) return;

        Transform physicsParent = PhysicsContainer != null ? PhysicsContainer : transform;
        _attachedComponents = Instantiate(payloadPrefab, physicsParent);
        _attachedComponents.transform.localPosition = Vector3.zero;
        _attachedComponents.transform.localRotation = Quaternion.identity;
        _attachedComponents.transform.localScale = Vector3.one;

        if (!_attachedComponents.TryGetComponent(out AttatchedSpellComponent attachedScript)) return;

        attachedScript.parentSpellCore = this;

        if (attachedScript.VisualRoot == null || VisualContainer == null) return;

        _attachedVisual = attachedScript.VisualRoot.gameObject;
        attachedScript.VisualRoot.SetParent(VisualContainer, false);
    }

    private void DestroyCurrentPayload()
    {
        AttatchedSpellComponent attachedScript = _attachedComponents != null ? _attachedComponents.GetComponent<AttatchedSpellComponent>() : null;
        if (attachedScript != null && _physicsObject != null) _physicsObject.UnregisterAttachedVisuals(attachedScript);

        if (_attachedVisual != null)
        {
            _attachedVisual.SetActive(false);
            Destroy(_attachedVisual);
            _attachedVisual = null;
        }

        if (_attachedComponents != null)
        {
            _attachedComponents.SetActive(false);
            Destroy(_attachedComponents);
            _attachedComponents = null;
        }
    }

    private void PreparePose(Vector3 position, Quaternion rotation)
    {
        if (_networkRigidbody != null)
            _networkRigidbody.Teleport(position, rotation);
        else if (_networkTransform != null)
            _networkTransform.Teleport(position, rotation);
        else
            transform.SetPositionAndRotation(position, rotation);

        if (_rigidbody == null) return;
        if (_rigidbody.isKinematic) return;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    private void ResetPhysicsVelocity()
    {
        if (_rigidbody == null) return;
        if (_rigidbody.isKinematic) return;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
    }

    private void ResetActivationMemory()
    {
        for (int i = 0; i < IntMemoryCapacity; i++) IntMemory.Set(i, 0);
        for (int i = 0; i < FloatMemoryCapacity; i++) FloatMemory.Set(i, 0f);
        for (int i = 0; i < VectorMemoryCapacity; i++) VectorMemory.Set(i, Vector3.zero);
        BoolMemory = 0;
    }

    private void InitializeActivationTick()
    {
        SpawnTick = Runner.Tick;
        LifetimeTimer = TickTimer.CreateFromSeconds(Runner, _configuredLifetime);

        foreach (IBehaviour behaviour in _myPlan.Behaviours)
            behaviour.InitTick(this);

        foreach (ITrigger trigger in _myPlan.Triggers)
            trigger.InitTick(this);
    }

    private void ActivateLocalState()
    {
        if (_isCountedActive) return;
        ActiveCount++;
        _isCountedActive = true;
    }

    private void ReconcileUnbufferedWakeFromTick()
    {
        if (_bufferedObject != null || _isCountedActive || !ActiveCastID.IsValid || LifetimeTimer.Expired(Runner)) return;
        ActivateLocalState();
    }

    private void DeactivateLocalState()
    {
        if (_isInitialized) CleanupPlanVFX();
        ReleaseTokenOwnership();
        TickContacts.Clear();
        ActiveVisuals.Clear();
        _isInitialized = false;

        if (_attachedVisual != null) _attachedVisual.SetActive(false);
        if (_attachedComponents != null) _attachedComponents.SetActive(false);

        if (_isCountedActive)
        {
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
            _isCountedActive = false;
        }
    }

    private void CleanupPlanVFX()
    {
        if (_myPlan == null) return;

        foreach (IBehaviour behaviour in _myPlan.Behaviours)
            behaviour.CleanupVFX(this);

        foreach (ITrigger trigger in _myPlan.Triggers)
            trigger.CleanupVFX(this);
    }

    private void ReconcileTokenOwnership()
    {
        ActiveSpell activeSpell = SpellStateManager.instance != null ? SpellStateManager.instance.GetActiveSpell(ActiveCastID) : null;

        if (_tokenOwnerSpell != null && (_tokenOwnerSpell != activeSpell || !_tokenOwnerCastId.Equals(ActiveCastID)))
            ReleaseTokenOwnership();

        if (activeSpell == null || _tokenOwnerSpell == activeSpell) return;

        activeSpell.AddToken(CoreNetworkId);
        _tokenOwnerSpell = activeSpell;
        _tokenOwnerCastId = ActiveCastID;
    }

    private void ReleaseTokenOwnership()
    {
        if (_tokenOwnerSpell != null)
            _tokenOwnerSpell.RemoveToken(CoreNetworkId);

        _tokenOwnerSpell = null;
        _tokenOwnerCastId = default;
    }

    public bool GetBool(int bitIndex) => (BoolMemory & (1 << bitIndex)) != 0;

    public void SetBool(int bitIndex, bool value)
    {
        if (value)
            BoolMemory |= 1 << bitIndex;
        else
            BoolMemory &= ~(1 << bitIndex);
    }

    public int GetInt(int index) => IntMemory.Get(index);
    public void SetInt(int index, int value) => IntMemory.Set(index, value);
    public float GetFloat(int index) => FloatMemory.Get(index);
    public void SetFloat(int index, float value) => FloatMemory.Set(index, value);
    public Vector3 GetVector(int index) => VectorMemory.Get(index);
    public void SetVector(int index, Vector3 value) => VectorMemory.Set(index, value);
}

public class SpellCompilationContext
{
    public int CurrentNodeIndex { get; set; }

    public SpellNetworkData GraphData;
    public List<SpellNode>[] DownstreamNodeDefinitions;

    private int _nextIntSlot;
    private int _nextFloatSlot;
    private int _nextVectorSlot;
    private int _nextBoolBit;
    private int _nextVfxId;

    public int ClaimIntSlot() => _nextIntSlot++;
    public int ClaimFloatSlot() => _nextFloatSlot++;
    public int ClaimVectorSlot() => _nextVectorSlot++;
    public int ClaimVFXId() => _nextVfxId++;

    public int ClaimBoolBit()
    {
        if (_nextBoolBit >= 32) Debug.LogError("Too many booleans on this core!");
        return _nextBoolBit++;
    }

    public IEnumerable<SpellNode> GetDownstreamDefinitions()
    {
        if (DownstreamNodeDefinitions == null || CurrentNodeIndex < 0 || CurrentNodeIndex >= DownstreamNodeDefinitions.Length)
            return Enumerable.Empty<SpellNode>();

        return DownstreamNodeDefinitions[CurrentNodeIndex];
    }
}

public struct PendingContact
{
    public GameObject Target;
    public Vector3 Point;
    public Vector3 Normal;
}
