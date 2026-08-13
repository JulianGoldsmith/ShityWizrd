using Fusion;
using Fusion.Addons.Physics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;


public enum EquippedState : byte
{
    None,
    EquippedInactive,
    EquippedActive
}

public struct DerivedEquipmentContext
{
    public EquippedState State;
    public NetworkObject Holder;
    public byte Slot;
}


[DefaultExecutionOrder(150)]
public class EquipableItem : InteractableItem, IAfterRender
{
    public string itemName;

    [Header("Baked Spells (JSON)")]
    public TextAsset defaultPrimarySpellJSON;
    public TextAsset defaultSecondarySpellJSON;

    [Networked, OnChangedRender(nameof(OnPrimarySpellChanged))]
    public SpellGraphId PrimarySpellID { get; set; }

    [Networked]
    public SpellGraphId SecondarySpellID { get; set; }

    public SpellGraph primaryActionSpell => SpellStateManager.instance.GetSpellGraph(PrimarySpellID);
    public SpellGraph secondaryActionSpell => SpellStateManager.instance.GetSpellGraph(SecondarySpellID);



    [Header("Item Actions")]
    public CastMethods PrimaryCastMethods = new CastMethods();
    public CastMethods SecondaryCastMethods = new CastMethods();
    public ItemAction feedActionRef;

    public HandState heldHandState;

    public Transform primaryHandle, secondaryHandle;
    public Transform visualPrimaryHandle, visualSecondaryHandle;

    public List<HitBoxBehaviour> hitboxes = new List<HitBoxBehaviour>();
    private bool _isHitboxActive = false;
    public bool IsHitboxActive => _isHitboxActive;

    [Header("Pickup Variables")]
    public Transform visualModel;


    //public GameObject hitbox;
    [Header("Hitbox ponts for melee sweep")]
    [SerializeField] private Collider[] worldColliders;
    public Transform weaponBase, weaponEnd;

    public Transform projectileSpawnPoint;

    public Vector3 throwDir = Vector3.zero;

    [Header("Local Presentation PD")]
    [SerializeField] private bool useVisualPD = true;
    [SerializeField] private ItemPD visualPDSettings;
    [SerializeField, Range(0f, 1f)] private float visualTargetVelocityInfluence = 1f;
    [SerializeField, Range(0f, 1f)] private float visualTargetAccelerationInfluence = 0.2f;
    [SerializeField, Min(0.001f)] private float visualMaxDeltaTime = 0.05f;
    [SerializeField, Min(0f)] private float visualMaxTargetAcceleration = 100f;
    [SerializeField, Min(0.01f)] private float visualReseedDistance = 1.5f;
    [SerializeField, Range(1f, 180f)] private float visualReseedAngle = 120f;

    private bool _visualPDInitialized;
    private NetworkObject _visualPDHolder;
    private Vector3 _visualPosition;
    private Quaternion _visualRotation = Quaternion.identity;
    private Vector3 _visualLinearVelocity;
    private Vector3 _visualAngularVelocity;
    private Vector3 _previousVisualTargetPosition;
    private Quaternion _previousVisualTargetRotation = Quaternion.identity;
    private Vector3 _previousVisualTargetVelocity;
    private Transform _visualOriginalParent;

    public Transform worldRenderRoot;
    public bool usePD = true;

    private XPBDGlobalManager _xpbdGlobalManager;

    [Networked] public ActiveCastID CurrentCastID { get; set; }
    public SpellState activeCast{ get {
            if (CurrentCastID.IsValid && SpellStateManager.instance != null)
            {
                ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(CurrentCastID);
                return activeSpell?.State;
            }
            return null;
        }
    }

    public CastActionController activeCaster;
    public HybridCharacterController activeHolder;


    [NonSerialized] private bool _hasLocalSimState;

    [Networked] public Vector3 LinVel { get; set; }
    [Networked] public Vector3 AngVel { get; set; }

    [Networked] private Vector3 PDTargetPosition { get; set; }
    [Networked] private Quaternion PDTargetRotation { get; set; }
    [Networked] private NetworkBool PDTargetInitialized { get; set; }
    [Networked] private NetworkId PDHolderId { get; set; }


    public ItemPD pdSettings;
    

    [Header ("Animation Sampling")]
    private GameObject _ghostSamplerRoot;
    private Transform _ghostSamplerPivot;
    private PlayableGraph _samplerGraph;
    private AnimationPlayableOutput _samplerOutput;


    private DerivedEquipmentContext _pendingEquipmentContext;
    private DerivedEquipmentContext _equipmentContext;
    public EquippedState EquipmentState => _equipmentContext.State;
    public NetworkObject HoldingPlayer => _equipmentContext.Holder;
    public bool IsActivelyEquipped => _equipmentContext.State == EquippedState.EquippedActive;

    public void AssignEquipmentContext(NetworkObject holder, byte slot, bool active)
    {
        _pendingEquipmentContext = new DerivedEquipmentContext
        {
            State = active ? EquippedState.EquippedActive : EquippedState.EquippedInactive,
            Holder = holder,
            Slot = slot
        };
    }

    #region Equipping & Communicating
    public void EquipSpellToPrimary(SpellGraph graph)
    {
        //Debug.Log($"Sending '{graph.name}' to the Master Library...");
        // Send it to the Host, and tell the Host to attach it to THIS weapon's Network ID!
        SpellStateManager.instance.SubmitNewSpellToHost(graph, this.Object.Id);
    }
    public void OnPrimarySpellChanged()
    {
        if (PrimarySpellID.NotNull() && primaryActionSpell != null)
        {
          
        }
    }
    #endregion

    public void AfterRender()
    {
        if (visualModel == null) return;

        if (EquipmentState == EquippedState.EquippedInactive || (EquipmentState == EquippedState.EquippedActive && activeHolder == null))
        {
            if (visualModel.gameObject.activeSelf) visualModel.gameObject.SetActive(false);
            if (visualModel.parent != _visualOriginalParent) visualModel.SetParent(_visualOriginalParent, true);

            ResetLocalPresentation();
            return;
        }

        if (!visualModel.gameObject.activeSelf) visualModel.gameObject.SetActive(true);

        if (EquipmentState == EquippedState.None)
        {
            ResetLocalPresentation();
            if (visualModel.parent != _visualOriginalParent) visualModel.SetParent(_visualOriginalParent, true);
            visualModel.SetPositionAndRotation(worldRenderRoot.position, worldRenderRoot.rotation);

            return;
        }

        if (!IsLocalPlayerHoldingThisItem())
        {
            ResetLocalPresentation();
            if (visualModel.parent != _visualOriginalParent) visualModel.SetParent(_visualOriginalParent, true);
            visualModel.SetPositionAndRotation(worldRenderRoot.position, worldRenderRoot.rotation);
            return;
        }

        if (visualModel.parent != null) visualModel.SetParent(null, true);
        UpdateLocalPresentation();
    }

    private void ResetLocalPresentation()
    {
        _visualPDInitialized = false;
        _visualPDHolder = null;
        _visualLinearVelocity = Vector3.zero;
        _visualAngularVelocity = Vector3.zero;
        _previousVisualTargetVelocity = Vector3.zero;
    }

    private void UpdateLocalPresentation()
    {
        double renderTime = Runner.LocalRenderTime;
        int renderTick = (int)Math.Floor(renderTime / Runner.DeltaTime);

        GetDerivedActionPose(renderTick, renderTime, out ItemAction action, out int phaseID, out float phaseTime);

        Transform cameraTransform = activeHolder.camController.cameraTransform;
        Quaternion cameraRotation = cameraTransform.rotation;

        EyePosAndLookDir cameraEye = new EyePosAndLookDir(
            cameraTransform.position,
            cameraRotation * Vector3.forward,
            cameraRotation * Vector3.up
        );

        if (!GetTargetPose(action, phaseID, phaseTime, cameraEye, out Vector3 targetPosition, out Quaternion targetRotation)) return;

        float frameDt = Time.deltaTime;
        bool reseed = !_visualPDInitialized || _visualPDHolder != HoldingPlayer || frameDt <= 0f || frameDt > visualMaxDeltaTime;

        if (reseed)
        {
            _visualPDInitialized = true;
            _visualPDHolder = HoldingPlayer;
            _visualPosition = worldRenderRoot.position;
            _visualRotation = worldRenderRoot.rotation;

            NetworkBehaviourBufferInterpolator interpolator = new NetworkBehaviourBufferInterpolator(this);

            if (interpolator.Valid)
            {
                Vector3 interpolatedPDTargetPosition = interpolator.Vector3(nameof(PDTargetPosition));
                Quaternion interpolatedPDTargetRotation = Quaternion.Normalize(interpolator.Quaternion(nameof(PDTargetRotation)));
                Quaternion inversePDTargetRotation = Quaternion.Inverse(interpolatedPDTargetRotation);
                Vector3 localPositionError = inversePDTargetRotation * (worldRenderRoot.position - interpolatedPDTargetPosition);
                Quaternion localRotationError = Quaternion.Normalize(inversePDTargetRotation * worldRenderRoot.rotation);

                _visualPosition = targetPosition + targetRotation * localPositionError;
                _visualRotation = Quaternion.Normalize(targetRotation * localRotationError);
            }

            _visualLinearVelocity = activeHolder.hipsRb.linearVelocity;
            _visualAngularVelocity = Vector3.zero;
            _previousVisualTargetPosition = targetPosition;
            _previousVisualTargetRotation = targetRotation;
            _previousVisualTargetVelocity = _visualLinearVelocity;
            visualModel.SetPositionAndRotation(_visualPosition, _visualRotation);
            return;
        }

        float safeDt = Mathf.Max(frameDt, 0.0001f);
        Vector3 targetLinearVelocity = (targetPosition - _previousVisualTargetPosition) / safeDt;
        Vector3 targetAngularVelocity = CalculateAngularVelocity(targetRotation, _previousVisualTargetRotation, safeDt);
        Vector3 targetAcceleration = (targetLinearVelocity - _previousVisualTargetVelocity) / safeDt;

        if (visualMaxTargetAcceleration > 0f) targetAcceleration = Vector3.ClampMagnitude(targetAcceleration, visualMaxTargetAcceleration);

        ItemPD settings = null;
        if (useVisualPD) settings = visualPDSettings != null ? visualPDSettings : pdSettings;

        if (settings == null)
        {
            _visualPosition = targetPosition;
            _visualRotation = targetRotation;
            _visualLinearVelocity = targetLinearVelocity;
            _visualAngularVelocity = targetAngularVelocity;
        }
        else
        {
            ItemPDStepResult result = settings.CalculateStep(
                _visualPosition,
                _visualRotation,
                _visualLinearVelocity,
                _visualAngularVelocity,
                targetPosition,
                targetRotation,
                targetLinearVelocity * visualTargetVelocityInfluence,
                targetAngularVelocity * visualTargetVelocityInfluence,
                targetAcceleration * visualTargetAccelerationInfluence,
                safeDt
            );

            _visualPosition = result.Position;
            _visualRotation = result.Rotation;
            _visualLinearVelocity = result.LinearVelocity;
            _visualAngularVelocity = result.AngularVelocity;

            if (Vector3.Distance(_visualPosition, targetPosition) > visualReseedDistance || Quaternion.Angle(_visualRotation, targetRotation) > visualReseedAngle)
            {
                _visualPosition = targetPosition;
                _visualRotation = targetRotation;
                _visualLinearVelocity = targetLinearVelocity;
                _visualAngularVelocity = targetAngularVelocity;
            }
        }

        _previousVisualTargetPosition = targetPosition;
        _previousVisualTargetRotation = targetRotation;
        _previousVisualTargetVelocity = targetLinearVelocity;

        visualModel.SetPositionAndRotation(_visualPosition, _visualRotation);
    }





    public override void Spawned()
    {
        networkedRB = this.GetComponent<NetworkRigidbody3D>();
        if (visualModel != null) _visualOriginalParent = visualModel.parent;

        _hasLocalSimState = false;
        LinVel = Vector3.zero;
        AngVel = Vector3.zero;

        RestCastingState();

        InitializeAnimClipSampler();
        Runner.SetIsSimulated(this.Object, true);

        if (Object.HasStateAuthority)
        {
            InitializeBakedSpells();
        }

        if (GameController.Instance != null && GameController.Instance.xPBDGlobalManager != null)
        {
            _xpbdGlobalManager = GameController.Instance.xPBDGlobalManager;
            _xpbdGlobalManager.AfterXPBDBeforePhysics += SimulateBeforePhysics;
        }

        ResetLocalPresentation();
    }

    public override void FixedUpdateNetwork()
    {
        DerivedEquipmentContext context = _pendingEquipmentContext;
        _pendingEquipmentContext = default;
        _equipmentContext = context;

        bool equipped = context.State != EquippedState.None;
        networkedRB.RBIsKinematic = equipped;

        foreach (Collider worldCollider in worldColliders)
        {
            worldCollider.enabled = !equipped;
        }

        if (context.State != EquippedState.EquippedActive)
        {
            activeCaster = null;
            activeHolder = null;

            PDTargetInitialized = false;
            PDHolderId = default;
            LinVel = Vector3.zero;
            AngVel = Vector3.zero;
            return;
        }

        activeCaster = context.Holder.GetComponent<PlayerCastActionController>();
        activeHolder = context.Holder.GetComponent<HybridCharacterController>();

        if (PDHolderId != context.Holder.Id)
        {
            PDTargetInitialized = false;
            PDHolderId = context.Holder.Id;
            LinVel = Vector3.zero;
            AngVel = Vector3.zero;
        }

        //SimulatePhysics(activeHolder, Runner.DeltaTime);
    }


    #region PickUpDrop
    

    #endregion


    #region ActionsAndCasting

    public ItemAction GetAction(ItemActionChannel channel, int actionID)
    {
        if (channel == ItemActionChannel.Feed) return actionID == 0 ? feedActionRef : null;
        if (actionID < 0 || actionID > (int)EntryPointType.Effect) return null;

        EntryPointType entryPointType = (EntryPointType)actionID;

        switch (channel)
        {
            case ItemActionChannel.Primary: return PrimaryCastMethods.GetAction(entryPointType);
            case ItemActionChannel.Secondary: return SecondaryCastMethods.GetAction(entryPointType);
            default: return null;
        }
    }

    public SpellGraphId GetEquippedSpellID(ItemActionChannel channel)
    {
        switch (channel)
        {
            case ItemActionChannel.Primary: return PrimarySpellID;
            case ItemActionChannel.Secondary: return SecondarySpellID;
            default: return default;
        }
    }

    public bool TrySetEquippedSpellID(ItemActionChannel channel, SpellGraphId spellID)
    {
        if (!HasStateAuthority) return false;

        switch (channel)
        {
            case ItemActionChannel.Primary:
                PrimarySpellID = spellID;
                return true;

            case ItemActionChannel.Secondary:
                SecondarySpellID = spellID;
                return true;

            default:
                return false;
        }
    }

    public bool SupportsEntryPoint(ItemActionChannel channel, EntryPointType entryPointType)
    {
        switch (channel)
        {
            case ItemActionChannel.Primary: return PrimaryCastMethods.Supports(entryPointType);
            case ItemActionChannel.Secondary: return SecondaryCastMethods.Supports(entryPointType);
            default: return false;
        }
    }

    public bool TryFindCompatibleChannel(EntryPointType entryPointType, out ItemActionChannel channel)
    {
        channel = ItemActionChannel.None;

        bool primarySupports = PrimaryCastMethods.Supports(entryPointType);
        bool secondarySupports = SecondaryCastMethods.Supports(entryPointType);

        if (primarySupports == secondarySupports) return false;

        channel = primarySupports ? ItemActionChannel.Primary : ItemActionChannel.Secondary;
        return true;
    }

    public bool TryResolveCast(ItemActionChannel channel, out EntryPointType entryPointType, out ItemAction action, out SpellGraphId spellID)
    {
        entryPointType = default;
        action = null;
        spellID = GetEquippedSpellID(channel);

        if (spellID.IsNull()) return false;
        if (SpellStateManager.instance == null) return false;
        RuntimeSpell runtimeSpell = SpellBlueprintLibrary.Get(spellID);
        if (runtimeSpell == null) return false;

        entryPointType = runtimeSpell.EntryType;

        action = GetAction(channel, (int)entryPointType);
        return action != null && action.IsImplemented;
    }

    public bool TryFindSingleLoadedChannel(out ItemActionChannel channel)
    {
        channel = ItemActionChannel.None;

        bool primaryLoaded = PrimarySpellID.NotNull();
        bool secondaryLoaded = SecondarySpellID.NotNull();

        if (primaryLoaded == secondaryLoaded) return false;

        channel = primaryLoaded ? ItemActionChannel.Primary : ItemActionChannel.Secondary;
        return true;
    }

    #endregion



    #region posesAndItemAnims

    private void SimulateBeforePhysics()
    {
        if (_equipmentContext.State != EquippedState.EquippedActive || activeHolder == null) return;

        SimulatePhysics(activeHolder, Runner.DeltaTime);
    }


    [Header("Idle Pose Settings")]
    public Vector3 idleLocalPos = new Vector3(0.3f, -0.25f, 0.6f);
    public Vector3 idleLocalRotEuler = Vector3.zero;

    private void SimulatePhysics(HybridCharacterController hcc, float dt)
    {
        EyePosAndLookDir eye = hcc.GetEyePosAndLookDirSim();
        double simulationTime = Runner.Tick * (double)Runner.DeltaTime;

        GetDerivedActionPose(Runner.Tick, simulationTime, out ItemAction action, out int phaseID, out float phaseTime);

        if (!GetTargetPose(action, phaseID, phaseTime, eye, out Vector3 targetPos, out Quaternion targetRot)) return;

        float safeDt = Mathf.Max(dt, 0.0001f);
        Vector3 targetLinVel = hcc.hipsRb.linearVelocity;
        Vector3 targetAngVel = Vector3.zero;

        if (PDTargetInitialized)
        {
            int previousTick = Runner.Tick - 1;
            double previousSimulationTime = simulationTime - safeDt;

            GetDerivedActionPose(previousTick, previousSimulationTime, out ItemAction previousAction, out int previousPhaseID, out float previousPhaseTime);

            Vector3 previousEyePosition = hcc.GetEyePosSim(hcc.hipsRb.position, hcc.previousLookRot);
            EyePosAndLookDir previousEye = new EyePosAndLookDir(
                previousEyePosition,
                hcc.previousLookRot * Vector3.forward,
                hcc.previousLookRot * Vector3.up
            );

            if (GetTargetPose(previousAction, previousPhaseID, previousPhaseTime, previousEye, out Vector3 previousTargetPosition, out Quaternion previousTargetRotation))
            {
                targetLinVel += (targetPos - previousTargetPosition) / safeDt;
                targetAngVel = CalculateAngularVelocity(targetRot, previousTargetRotation, safeDt);
            }
        }
        else
        {
            PDTargetInitialized = true;
        }

        PDTargetPosition = targetPos;
        PDTargetRotation = targetRot;

        if (pdSettings == null || !usePD)
        {
            networkedRB.Rigidbody.MovePosition(targetPos);
            networkedRB.Rigidbody.MoveRotation(targetRot);

            LinVel = targetLinVel;
            AngVel = targetAngVel;
            return;
        }

        ItemPDStepResult result = pdSettings.CalculateStep(
            networkedRB.Rigidbody.position,
            networkedRB.Rigidbody.rotation,
            LinVel,
            AngVel,
            targetPos,
            targetRot,
            targetLinVel,
            targetAngVel,
            Vector3.zero,
            safeDt
        );

        networkedRB.Rigidbody.MovePosition(result.Position);
        networkedRB.Rigidbody.MoveRotation(result.Rotation);

        LinVel = result.LinearVelocity;
        AngVel = result.AngularVelocity;
    }

    private Vector3 CalculateAngularVelocity(Quaternion currentRotation, Quaternion previousRotation, float dt)
    {
        Quaternion delta = Quaternion.Normalize(currentRotation * Quaternion.Inverse(previousRotation));

        if (delta.w < 0f) delta = new Quaternion(-delta.x, -delta.y, -delta.z, -delta.w);

        delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);

        if (angleDegrees > 180f) angleDegrees -= 360f;
        if (axis.sqrMagnitude <= 0.000001f || Mathf.Abs(angleDegrees) <= 0.0001f) return Vector3.zero;

        return axis.normalized * angleDegrees * Mathf.Deg2Rad / Mathf.Max(dt, 0.0001f);
    }



    private void GetDerivedActionPose(int sampleTick, double sampleTime, out ItemAction action, out int phaseID, out float phaseTime)
    {
        action = null;
        phaseID = -1;
        phaseTime = 0f;

        if (HoldingPlayer == null) return;
        if (!HoldingPlayer.TryGetComponent(out PlayerActionManager actionManager)) return;

        if (!actionManager.TryGetActionContextForItem(this, sampleTick, out action, out DerivedActionContext context))
        {
            action = null;
            return;
        }

        if (context.IsComplete)
        {
            action = null;
            return;
        }

        phaseID = context.PhaseID;
        phaseTime = (float)Math.Max(0.0, sampleTime - context.PhaseStartTick * (double)Runner.DeltaTime);
    }

    private bool GetTargetPose(ItemAction action, int phaseID, float phaseTime, EyePosAndLookDir eye, out Vector3 pos, out Quaternion rot)
    {
        Quaternion viewRot = Quaternion.LookRotation(eye.Forward, eye.Up);
        Quaternion idleRotOffset = Quaternion.Euler(idleLocalRotEuler);

        Vector3 idleWorldPos = eye.EyePosition + eye.Right * idleLocalPos.x + eye.Up * idleLocalPos.y + eye.Forward * idleLocalPos.z;
        Quaternion idleWorldRot = viewRot * idleRotOffset;

        if (action == null || phaseID < 0)
        {
            pos = idleWorldPos;
            rot = idleWorldRot;
            return true;
        }

        ItemAnimation animation = action.GetAnimationForPhase(phaseID);

        if (animation.clip == null)
        {
            pos = idleWorldPos;
            rot = idleWorldRot;
            return true;
        }

        float sampleTime = phaseTime * animation.speedMultiplier;

        if (TrySampleFromClip(animation, sampleTime, out Vector3 sampledLocalPos, out Quaternion sampledLocalRot))
        {
            pos = eye.EyePosition + viewRot * sampledLocalPos;
            rot = viewRot * sampledLocalRot;
            return true;
        }

        pos = idleWorldPos;
        rot = idleWorldRot;
        return true;
    }



    private void InitializeAnimClipSampler()
    {
        _ghostSamplerRoot = new GameObject($"{this.name}_Sampler_Root");
        _ghostSamplerRoot.hideFlags = HideFlags.HideAndDontSave;

        _ghostSamplerRoot.SetActive(true);

        GameObject child = new GameObject("ItemPivot");
        child.transform.SetParent(_ghostSamplerRoot.transform);
        _ghostSamplerPivot = child.transform;

        Animator animator = child.AddComponent<Animator>();

        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        _samplerGraph = PlayableGraph.Create($"{this.name}_Graph");
        _samplerGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

        _samplerOutput = AnimationPlayableOutput.Create(_samplerGraph, "GhostOutput", animator);
    }

    private void CleanupAnimClipSampler()
    {
        if (_samplerGraph.IsValid())
        {
            _samplerGraph.Destroy();
        }

        if (_ghostSamplerRoot != null)
        {
            Destroy(_ghostSamplerRoot);
        }
    }

    private bool TrySampleFromClip(ItemAnimation itemAnim, float time, out Vector3 localPos, out Quaternion localRot)
    {
        localPos = default;
        localRot = default;

        if (itemAnim.clip == null || _ghostSamplerPivot == null) return false;

        if (!_samplerGraph.IsValid()) return false;

        _ghostSamplerRoot.transform.position = Vector3.zero;
        _ghostSamplerRoot.transform.rotation = Quaternion.identity;

        var clipPlayable = AnimationClipPlayable.Create(_samplerGraph, itemAnim.clip);

        clipPlayable.SetDuration(itemAnim.clip.length);
        clipPlayable.SetTime(time);

        _samplerOutput.SetSourcePlayable(clipPlayable);
        _samplerGraph.Evaluate();

        localPos = _ghostSamplerPivot.localPosition;
        localRot = _ghostSamplerPivot.localRotation;

        if (clipPlayable.IsValid())
        {
            clipPlayable.Destroy();
        }

        return true;
    }

    public bool IsLocalPlayerHoldingThisItem()
    {
        if (HoldingPlayer == null) return false;

        return HoldingPlayer == Runner.GetPlayerObject(Runner.LocalPlayer);
    }

    #endregion

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_xpbdGlobalManager != null)
        {
            _xpbdGlobalManager.AfterXPBDBeforePhysics -= SimulateBeforePhysics;
        }

        if (visualModel != null && visualModel.parent != _visualOriginalParent) visualModel.SetParent(_visualOriginalParent, true);

        CleanupAnimClipSampler();
    }

    public void RestCastingState()
    {
        ClearSpellState();
        activeCaster = null;
        activeHolder = null;
    }

    public void ClearSpellState()
    {
        CurrentCastID = default;
    }

    public Transform GetHandle(bool isLeft, bool visual)
    {
        if (visual)
        {
            if (isLeft && visualSecondaryHandle != null) return visualSecondaryHandle;
            return visualPrimaryHandle;
        }

        if (isLeft && secondaryHandle != null) return secondaryHandle;
        return primaryHandle;
    }

    #region

    public void EnableHitbox(int index)
    {
        if (index < 0 || index >= hitboxes.Count) return;

        hitboxes[index].Initialize(this, activeCast);
        hitboxes[index].EnableHitBox();
        _isHitboxActive = true;
    }

    public void DisableHitbox(int index)
    {
        if (index < 0 || index >= hitboxes.Count) return;
        hitboxes[index].DisableHitBox();
        _isHitboxActive = false;
    }

    public void OnMeleeHit(GameObject target, SpellState state, Vector3 hitPoint, Vector3 momentum)
    {
        // Only the Server should deal damage to prevent cheating/desync
        if (Object.HasStateAuthority || activeCaster.HasInputAuthority)
        {
            // Create the trigger context
            var triggerInfo = new SpellTriggerInfo(
                isCast: false,
                source: HoldingPlayer.gameObject,
                state: state,
                position: hitPoint,
                rotation: Quaternion.LookRotation(momentum.normalized + Vector3.up * 0.01f), // Safety for zero vector
                triggerVector: momentum,
                hitObject: target
            );

            // Update State context
            state.CastPosition = hitPoint;
            state.CastAimTargetPos = target.transform.position;

            // Execute the Spell Graph (Hit Logic)
            // Assuming your graph has logic connected to the Combo Index or a specific "OnHit" node
            primaryActionSpell.ExecuteComboIndex(state.ComboIndex, triggerInfo);

            Debug.Log($"[Server] {itemName} hit {target.name}");
        }
        else
        {
            // Client Side: Play local hit sound / sparks immediately if you want zero latency feel
        }
    }



    #endregion

    private void InitializeBakedSpells()
    {
        if (SpellStateManager.instance == null) return;

        if (defaultPrimarySpellJSON != null)
        {
            PrimarySpellID = SpellStateManager.instance.LoadBakedWeaponSpell(defaultPrimarySpellJSON);
        }
        if (defaultSecondarySpellJSON != null)
        {
            SecondarySpellID = SpellStateManager.instance.LoadBakedWeaponSpell(defaultSecondarySpellJSON);
        }
    }
}


public enum ItemActionChannel : byte
{
    None,
    Primary,
    Secondary,
    Feed
}
