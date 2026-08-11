using Fusion;
using Fusion.Addons.Physics;
using NUnit.Framework;
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

    [Header("Visual Interpolation")]
    [SerializeField] private Transform worldRenderRoot;

    private bool _visualPDInitialized;
    private bool _visualUsesLocalPose;
    private NetworkObject _visualHolder;
    private Vector3 _visualPosition;
    private Quaternion _visualRotation = Quaternion.identity;
    private Vector3 _visualLinVel;
    private Vector3 _visualAngVel;
    private Vector3 _previousVisualTargetPosition;


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

        UpdateItemVisability();
       // UpdateVisualWithRenderPD();
    }

    private void UpdateItemVisability()
    {
        if (visualModel == null) return;

        if (EquipmentState == EquippedState.None)
        {
            _visualPDInitialized = false;

            if (!visualModel.gameObject.activeSelf) visualModel.gameObject.SetActive(true);

            visualModel.SetPositionAndRotation(worldRenderRoot.position, worldRenderRoot.rotation);
            return;
        }

        if (EquipmentState == EquippedState.EquippedInactive || activeHolder == null)
        {
            _visualPDInitialized = false;

            if (visualModel.gameObject.activeSelf) visualModel.gameObject.SetActive(false);
            return;
        }

        if (!visualModel.gameObject.activeSelf) visualModel.gameObject.SetActive(true);

    }

    private void UpdateVisualWithRenderPD()
    {
        if (EquipmentState != EquippedState.EquippedActive) return;

        NetworkObject localPlayer = Runner.GetPlayerObject(Runner.LocalPlayer);
        bool useLocalPose = HoldingPlayer == localPlayer;

        Vector3 eyePosition;
        Quaternion eyeRotation;

        if (useLocalPose)
        {
            Transform cameraTransform = activeHolder.camController.cameraTransform;
            eyePosition = cameraTransform.position;
            eyeRotation = cameraTransform.rotation;
        }
        else
        {
            eyeRotation = Quaternion.Slerp(activeHolder.previousLookRot, activeHolder.lookRot, Runner.LocalAlpha);
            eyePosition = activeHolder.smoothedNetworkedRenderRoot.position
                + activeHolder.camController.localEyeOffset
                + activeHolder.camController.GetEyePosBasedOnPitch(eyeRotation);
        }

        EyePosAndLookDir eye = new EyePosAndLookDir(eyePosition, eyeRotation * Vector3.forward, eyeRotation * Vector3.up);

        double renderTime = Runner.LocalRenderTime;
        int renderTick = (int)Math.Floor(renderTime / Runner.DeltaTime);

        GetDerivedActionPose(renderTick, renderTime, out ItemAction action, out int phaseID, out float phaseTime);

        if (!GetTargetPose(action, phaseID, phaseTime, eye, out Vector3 targetPosition, out Quaternion targetRotation)) return;

        bool presentationChanged = !_visualPDInitialized || _visualHolder != HoldingPlayer || _visualUsesLocalPose != useLocalPose;

        if (presentationChanged)
        {
            _visualPDInitialized = true;
            _visualHolder = HoldingPlayer;
            _visualUsesLocalPose = useLocalPose;
            _visualPosition = targetPosition;
            _visualRotation = targetRotation;
            _visualLinVel = Vector3.zero;
            _visualAngVel = Vector3.zero;
            _previousVisualTargetPosition = targetPosition;

            visualModel.SetPositionAndRotation(_visualPosition, _visualRotation);
            return;
        }

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 targetVelocity = activeHolder.camController.RenderVelocity;
        _previousVisualTargetPosition = targetPosition;

        //if (pdSettings == null)
        if (true)
        {
            _visualPosition = targetPosition;
            _visualRotation = targetRotation;
        }
        else
        {
            Vector3 currentPosition = _visualPosition;
            Quaternion currentRotation = _visualRotation;

            ItemPDStepResult result = pdSettings.CalculateStep(
                 _visualPosition,
                 _visualRotation,
                 _visualLinVel,
                 _visualAngVel,
                 targetPosition,
                 targetRotation,
                 targetVelocity,
                 Vector3.zero,
                 Vector3.zero,
                 dt
             );
            _visualPosition = result.Position;
            _visualRotation = result.Rotation;
            _visualLinVel = result.LinearVelocity;
            _visualAngVel = result.AngularVelocity;
        }

        visualModel.SetPositionAndRotation(_visualPosition, _visualRotation);
    }

    public override void Spawned()
    {
        networkedRB = this.GetComponent<NetworkRigidbody3D>();

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
    }

   /* public void UpdateModelVisuals()
    {
        if (!IsActivelyEquipped) return;

        if (visualModel == null || HoldingPlayer == null) return;
        if (!HoldingPlayer.TryGetComponent(out HybridCharacterController hcc)) return;

        bool local = IsLocalPlayerHoldingThisItem();
        // 1. GET THE PERFECTLY SMOOTH VISUAL TARGET
        Vector3 eyePos;
        Quaternion eyeRot;

        if (local)
        {
            // The local player's camera is the ONLY perfectly smooth reference point
            Transform camTransform = hcc.camController.cameraTransform;
            eyePos = camTransform.position;
            eyeRot = camTransform.rotation;
        }
        else
        {
            // Proxies must use the specifically smoothed visual root
            eyePos = hcc.smoothedNetworkedRenderRoot.position + hcc.camController.localEyeOffset + hcc.camController.GetEyePosBasedOnPitch(hcc.lookRot);
            eyeRot = hcc.lookRot;
        }

        EyePosAndLookDir eye = new EyePosAndLookDir(eyePos, eyeRot * Vector3.forward, eyeRot * Vector3.up);
        double renderTime = Runner.LocalRenderTime;
        int renderTick = (int)Math.Floor(renderTime / Runner.DeltaTime);
        GetDerivedActionPose(renderTick, renderTime, out ItemAction action, out int phaseID, out float phaseTime);

        if (!GetTargetPose(action, phaseID, phaseTime, eye, out Vector3 targetPos, out Quaternion targetRot))
            return;

        // 2. CALCULATE CONTINUOUS VISUAL VELOCITY
        // Just like the HandsController, we MUST calculate velocity in Render time 
        // to keep the PD spring mathematically stable at monitor refresh rates.
        Vector3 currentVisualPos = local ? hcc.camController.cameraTransform.position : hcc.smoothedNetworkedRenderRoot.position;

        float dtRender = Mathf.Max(Time.deltaTime, 1e-6f);
        Vector3 ownerVel = (currentVisualPos - _cashedVisualPos) / dtRender;
        _cashedVisualPos = currentVisualPos;

        // Safety: If the network teleports the player, prevent the velocity from exploding
        if (ownerVel.sqrMagnitude > 1000f) ownerVel = Vector3.zero;

        if (pdSettings != null)
        {
            pdSettings.CalculateStep(
                visualModel.position, visualModel.rotation,
                targetPos, targetRot,
                ownerVel, Vector3.zero, // Zero out acceleration; velocity delta handles the sway naturally
                dtRender,
                ref visualLinVel, ref visualAngleVel,
                out Vector3 newPos, out Quaternion newRot
            );

            visualModel.position = newPos;
            visualModel.rotation = newRot;
        }
    }*/

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

        SimulatePhysics(activeHolder, Runner.DeltaTime);
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
        Vector3 targetLinVel = Vector3.zero;
        Vector3 targetAngVel = Vector3.zero;

        if (PDTargetInitialized)
        {
            targetLinVel = (targetPos - PDTargetPosition) / safeDt;

            Quaternion targetRotationDelta = Quaternion.Normalize(targetRot * Quaternion.Inverse(PDTargetRotation));

            if (targetRotationDelta.w < 0f)
            {
                targetRotationDelta.x = -targetRotationDelta.x;
                targetRotationDelta.y = -targetRotationDelta.y;
                targetRotationDelta.z = -targetRotationDelta.z;
                targetRotationDelta.w = -targetRotationDelta.w;
            }

            targetRotationDelta.ToAngleAxis(out float targetAngleDeg, out Vector3 targetAxis);

            if (targetAngleDeg > 180f) targetAngleDeg -= 360f;

            if (targetAxis.sqrMagnitude > 0.000001f && Mathf.Abs(targetAngleDeg) > 0.0001f)
            {
                targetAngVel = targetAxis.normalized * targetAngleDeg * Mathf.Deg2Rad / safeDt;
            }
        }
        else
        {
            PDTargetInitialized = true;
        }

        PDTargetPosition = targetPos;
        PDTargetRotation = targetRot;

        if (pdSettings == null)
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

    //private void CalculatePD(Vector3 currentPos, Quaternion currentRot,Vector3 targetPos, Quaternion targetRot,
    //    Vector3 ownerVelocity, Vector3 ownerAcceleration, float dt, ref Vector3 linVel, ref Vector3 angVel,  out Vector3 newPos, out Quaternion newRot)
    //{
    //    float safeDt = Mathf.Max(dt, 1e-4f);

    //    Vector3 posError = targetPos - currentPos;

    //    float inertiaScale = 0.5f;
    //    Vector3 inertialForce = ownerAcceleration * inertiaScale;

    //    Vector3 relativeVel = linVel - ownerVelocity;

    //    Vector3 accel = (positionStiffness * posError) - (positionDamping * relativeVel);
    //    accel += inertialForce;

    //    linVel += accel * safeDt;


    //    float speed = linVel.magnitude;
    //    if (speed > maxLinearSpeed && speed > 1e-5f)
    //        linVel *= (maxLinearSpeed / speed);

    //    newPos = currentPos + linVel * safeDt;

    //    Quaternion rotError = targetRot * Quaternion.Inverse(currentRot);
    //    rotError.ToAngleAxis(out float angleDeg, out Vector3 axis);
    //    if (angleDeg > 180f) angleDeg -= 360f;

    //    if (axis.sqrMagnitude < 1e-6f || Mathf.Abs(angleDeg) < 0.05f)
    //    {
    //        newRot = targetRot;
    //        angVel *= (1.0f - (rotationDamping * safeDt));
    //        return;
    //    }

    //    axis.Normalize();
    //    Vector3 angError = axis * (angleDeg * Mathf.Deg2Rad);

    //    Vector3 angAccel = rotationStiffness * angError - rotationDamping * angVel;
    //    angVel += angAccel * safeDt;

    //    float angSpeed = angVel.magnitude;
    //    if (angSpeed > maxAngularSpeed && angSpeed > 1e-6f)
    //    {
    //        angVel *= (maxAngularSpeed / angSpeed);
    //    }

    //    Quaternion deltaRot = Quaternion.identity;
    //    if (angVel.magnitude > 1e-6f)
    //    {
    //        float deltaAngleDeg = angVel.magnitude * Mathf.Rad2Deg * safeDt;
    //        deltaRot = Quaternion.AngleAxis(deltaAngleDeg, angVel.normalized);
    //    }

    //    newRot = deltaRot * currentRot;
    //}



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
