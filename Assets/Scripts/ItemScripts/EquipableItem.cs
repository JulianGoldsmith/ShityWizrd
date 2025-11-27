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

[DefaultExecutionOrder(5)]
public class EquipableItem : InteractableItem
{
    public string itemName;

    public string primarySpellID, secondarySpellID;

    public SpellGraph primaryActionSpell, secondaryActionSpell;

    [Header("Item Actions (Templates)")]
    public List<ItemAction> primaryActionsRef = new List<ItemAction>();
    public List<ItemAction> secondaryActionsRef = new List<ItemAction>();

    [NonSerialized] public List<ItemAction> primaryActions = new List<ItemAction>();
    [NonSerialized] public List<ItemAction> secondaryActions = new List<ItemAction>();

    public HandState heldHandState;

    public Transform primaryHandle, secondaryHandle;

    public NetworkObjectBuffer networkObjectBuffer;

    [Header("Pickup Variables")]
    public Transform visualModel;

    [HideInInspector] bool isKinematic;
    [HideInInspector] bool collideractive;
    Vector3 secretHiddenSpot = new Vector3(0, 200, 0); //lol dont look here

    //public GameObject hitbox;
    [Header("Hitbox ponts for melee sweep")]
    public Transform weaponBase, weaponEnd;

    public Transform projectileSpawnPoint;

    [Networked, OnChangedRender(nameof(PickUpOrDrop))] 
    public NetworkObject HoldingPlayer { get; set; }

    public NetworkObject lastHoldingPlayer;

    [Networked] public int HolderChangedCount {get; set; }

    private ChangeDetector _changeDetector;

    public Vector3 throwDir = Vector3.zero;

    int my_player_id { get { return Runner.LocalPlayer.PlayerId; } }

    public SpellState activeCast;
    public CastActionController activeCaster;
    public HybridCharacterController activeHolder;


    [NonSerialized] private bool _hasLocalSimState;

    [Networked] public NetworkItemActionData ItemActionData { get; set; }
    public NetworkItemActionData localItemActionData;
    [Networked] public Vector3 LinVel { get; set; }
    [Networked] public Vector3 AngVel { get; set; }
    public Vector3 visualLinVel, visualAngleVel;

    public ItemPD pdSettings;
    

    [Header ("Animation Sampling")]
    private GameObject _ghostSamplerRoot;
    private Transform _ghostSamplerPivot;
    private PlayableGraph _samplerGraph;
    private AnimationPlayableOutput _samplerOutput;


    #region Equipping & Communicating
    int sendingmessageid = 0;
    int receivingmessageid = 0;
    List<byte[]> received_chunks = null;
    public void EquipSpellToPrimary(SpellGraph graph)
    {
        Debug.Log("Equipping spell");
        // Set the spell as primary spell and communicate
        // the changes to all other instances via RPC call.

        // I assign the spell a new SpellGraphId.
        graph.spellGraphId = new SpellGraphId(my_player_id);

        // I equip my spell.
        SetAndInitialise(graph);

        string json = graph.ToJson();

        int playerid = my_player_id;
        // TODO:
        // Additional cleaning (pre and post) of the JSON
        // to reduce bandwidth usage.
        // Lots of variable names can be replace before,
        // then reintroduced via a mapping table (i.e. name->id).

        // chunk it
        byte[] data = Encoding.UTF8.GetBytes(json);

        // Max payload is 512.
        // The array has 
        int chunkSize = 450; 
        int totalChunks = (data.Length + chunkSize - 1) / chunkSize;

        // increment messageid
        sendingmessageid++;

        // send it
        for (int i = 0; i < totalChunks; i++)
        {
            int size = Mathf.Min(chunkSize, data.Length - (i * chunkSize));
            byte[] chunk = new byte[size];
            Buffer.BlockCopy(data, i * chunkSize, chunk, 0, size);

            // Send via RPC
            RPC_SendJsonChunk(sendingmessageid, playerid, graph.spellGraphId.id,  i, totalChunks, chunk);
        }
    }
    public void EquipSpellToPrimaryFromJSON(string json, SpellGraphId sgid)
    {
        Debug.Log("received spell, equipping to primary from json");
        SpellGraph graph = SpellGraph.FromJson(json);
        if(graph != null)
        {
            Debug.Log("received spell, equipping to primary from json");
            graph.spellGraphId = sgid;
            SetAndInitialise(graph);
        }
    }
    [Rpc(sources: RpcSources.All, targets: RpcTargets.All)]
    public void RPC_SendJsonChunk(int messageId, int player_ref, int spellgraphid_id, int chunkIndex, int totalChunks, byte[] chunkData)
    {
        // don't care about it if it's my spell that I'm sending.
        if (my_player_id == player_ref)
            return;

        Debug.Log($"Receiving JSON Spell Chunk {chunkIndex} / {totalChunks}");

        if (messageId > receivingmessageid)
        {
            receivingmessageid = messageId;
            received_chunks = new List<byte[]>(new byte[totalChunks][]);
        }
        if (messageId != receivingmessageid)
            return;

        received_chunks[chunkIndex] = chunkData;

        // Check if we have all chunks
        if (received_chunks.Count >= totalChunks && received_chunks.All(c => c != null))
        {
            // Recombine, turn into json, then equip locally.
            byte[] fullData = received_chunks.SelectMany(c => c).ToArray();
            string json = System.Text.Encoding.UTF8.GetString(fullData);
            
            // reconstruct spellgraphid
            SpellGraphId sgid = new SpellGraphId(player_ref, spellgraphid_id);

            EquipSpellToPrimaryFromJSON(json, sgid);
        }
    }
    #endregion

    public void LoadSpells()
    {

        if (!string.IsNullOrEmpty(primarySpellID))
        {
            primaryActionSpell = SpellGraphController.Instance.GetSpellFromAssestsByName(primarySpellID);
            SetAndInitialise(primaryActionSpell);
        }


        if (!string.IsNullOrEmpty(secondarySpellID))
        {
            //secondaryActionSpell = SpellGraphController.Instance.GetSpellFromAssestsByName(secondarySpellID);
        }
    }

    public void Start()
    {
        LoadSpells();
        InitialiseRuntimeActions();
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        networkedRB = this.GetComponent<NetworkRigidbody3D>();
        networkObjectBuffer = this.GetComponent<NetworkObjectBuffer>();

        _hasLocalSimState = false;
        LinVel = Vector3.zero;
        AngVel = Vector3.zero;

        RestCastingState();
        localItemActionData = ItemActionData;

        InitializeAnimClipSampler();
        Runner.SetIsSimulated(this.Object, true);
    }

    public override void Render()
    {
        //if(HoldingPlayer!=null)
        //    Debug.Log($"Holding player in render is {HoldingPlayer.name}");
        //else
        //{
        //    Debug.Log($"Holding player in render is NULL");

        //}
        if (visualModel == null || HoldingPlayer == null) return;
        if (!HoldingPlayer.TryGetComponent(out HybridCharacterController hcc)) return;

        //if (HoldingPlayer.InputAuthority != Runner.LocalPlayer) return;

        bool local = IsLocalPlayerHoldingThisItem();

        var dataToUse = local ? localItemActionData : ItemActionData;

        EyePosAndLookDir eye = local ? hcc.GetEyePosAndLookDir(): hcc.GetEyePosAndLookDir();

        if (!GetTargetPose(dataToUse, eye, Time.deltaTime, out Vector3 targetPos, out Quaternion targetRot))
            return;

        Vector3 ownerVel = hcc.rendererVelocity;
        Vector3 ownerAccel = local ? hcc.rendererAccel : Vector3.zero;

        //Vector3 ownerVel = hcc.calculatedFixedVel;
        //Vector3 ownerAccel = hcc.calculatedFixedAccel;

        if (pdSettings != null)
        {
            pdSettings.CalculateStep(
                visualModel.position, visualModel.rotation,
                targetPos, targetRot,
                ownerVel, ownerAccel, 
                Time.deltaTime,
                ref visualLinVel, ref visualAngleVel,
                out Vector3 newPos, out Quaternion newRot
            );

            visualModel.position = newPos;
            visualModel.rotation = newRot;
        }
    }

    public override void FixedUpdateNetwork()
    {
        
        if (HoldingPlayer == null)
        {
            Debug.Log($"Item {this.name} holder is NULL");
            return;
        }
        else
        {
            Debug.Log($"Item {this.name} is held by {HoldingPlayer.name}");
        }


        TickActions();
        SimulatePhysics(HoldingPlayer.GetComponent<HybridCharacterController>(), Runner.DeltaTime);
        Debug.Log($"Simulating tick and hold pos on {this.name}");

        //SimulateHeldPose(hcc, cac, Runner.DeltaTime);
    }

    void PickUpOrDrop()
    {
        Debug.Log($"PickUpOrDropCalled On this client new holdingPLayer = {HoldingPlayer}");
        if (HasStateAuthority) return;
        if (HoldingPlayer != null)
        {
            PickUpItem(HoldingPlayer);
        }
        else
        {
            DropItem(lastHoldingPlayer, HasInputAuthority, HasStateAuthority);
        }
        lastHoldingPlayer = HoldingPlayer;
    }

    public void TickActions()
    {
        for (int i = 0; i < primaryActions.Count; i++)
        {
            var action = primaryActions[i];
            if (action == null) continue;

            action.Tick(i, Runner.DeltaTime);
        }
        for (int i = 0; i < secondaryActions.Count; i++)
        {
            var action = secondaryActions[i];
            if (action == null) continue;

            action.Tick(i, Runner.DeltaTime);
        }
    }

    #region PickUpDrop

    public override void PickUpItem(NetworkObject playerObject)
    {
        bool localPlayer = playerObject.InputAuthority == Runner.LocalPlayer; //if youre the local player

        if (playerObject.TryGetComponent(out NetworkedInventoryManager inventory))
        {
            inventory.activeItem = gameObject;
            inventory.currentItemInHand = this.GetComponent<NetworkObject>();
        }

        if(playerObject.TryGetComponent<PlayerCastActionController>(out PlayerCastActionController cac))
        {
            UpdateActionsToNewCaster(cac);
        }

        //this.Object.AssignInputAuthority(playerObject.InputAuthority);

        if (HasStateAuthority || HasInputAuthority)
        {
            HoldingPlayer = playerObject;
            HolderChangedCount++;

            LinVel = Vector3.zero;
            AngVel = Vector3.zero;
        }
        _hasLocalSimState = false;
        //networkedRB.Rigidbody.isKinematic = true;
        networkedRB.RBIsKinematic = true;
        networkedRB.GetComponent<Collider>().enabled = false;

        networkedRB.Rigidbody.angularVelocity = Vector3.zero;
        networkedRB.Rigidbody.linearVelocity = Vector3.zero;
   

        if (playerObject.TryGetComponent(out NetworkedHandsController hands))
        {
            Debug.Log($"Holding player {playerObject} picked up item {this.name}");

            //Transform handPalm = hands.rightHand.palmTransform;
            //Transform itemHandle = this.primaryHandle;

            //Quaternion handleRelRot = Quaternion.Inverse(visualModel.transform.rotation) * itemHandle.rotation;
            //Vector3 handleRelPos = Quaternion.Inverse(visualModel.transform.rotation) * (itemHandle.position - visualModel.transform.position);
            //Quaternion modelRot = (handPalm.rotation * Quaternion.Euler(hands.pickUpItemRotOffset) *  Quaternion.Inverse(handleRelRot));
            //Vector3 modelPos = handPalm.position - (modelRot * handleRelPos);

            //transform.SetPositionAndRotation(modelPos, modelRot);

            visualModel.localPosition = Vector3.zero;
            visualModel.localRotation = Quaternion.identity;

            //if (localPlayer)
            //{
                visualModel.transform.SetParent(null);
                //visualModel.transform.SetPositionAndRotation(modelPos, modelRot);
            //}
           
            hands.SetHandTarget_ToHold(false, heldHandState);
        }

        RestCastingState();
        activeCaster = playerObject.GetComponent<PlayerCastActionController>();
        activeHolder = playerObject.GetComponent<HybridCharacterController>();

        Debug.Log($"{this.name} item picked up by {playerObject.name}");
    }
    
    public override void DropItem(NetworkObject playerObject, bool hasInputAuthority, bool hasStateAuthority)
    {

        var characterController = playerObject.GetComponent<HybridCharacterController>();
        var handController = playerObject.GetComponent<NetworkedHandsController>();

        if (playerObject.TryGetComponent(out NetworkedInventoryManager inventory))
        {
            inventory.activeItem = null;
            inventory.currentItemInHand = null;
        }


        if (HasStateAuthority || HasInputAuthority)
        {
            HoldingPlayer = null;
            HolderChangedCount++;

            LinVel = Vector3.zero;
            AngVel = Vector3.zero;
           
        }
        _hasLocalSimState = false;

        Vector3 dropPosition = visualModel.transform.position;
        Quaternion dropRotation = visualModel.rotation;

        networkedRB.Teleport(dropPosition, dropRotation);
        networkedRB.RBIsKinematic = false;
        networkedRB.GetComponent<Collider>().enabled = true;

        throwDir = characterController.GetLookRot() * Vector3.forward;
        networkedRB.Rigidbody.AddForce((throwDir * 5f), ForceMode.Impulse);

        
        visualModel.SetParent(this.transform);
        visualModel.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

        handController.SetHandTarget_ToArmature(false);

        RestCastingState();

        Debug.Log($"dropped item {this.name}");
    }

    #endregion

    void SetAndInitialise(SpellGraph graph)
    {
        if(primaryActionSpell != graph)
        {
            if(primaryActionSpell != null)
            {
                SpellStateManager.instance.OnUnequipSpellGraph(primaryActionSpell.spellGraphId);
            }

            if(graph != null)
            {
                SpellStateManager.instance.OnEquipSpellGraph(graph.spellGraphId, graph);
            }
        }

        primaryActionSpell = graph;

        if (networkObjectBuffer != null)
            networkObjectBuffer.Initialise(graph);
    }


    #region ActionsAndCasting

    public void InitialiseRuntimeActions()
    {
        primaryActions = CloneActionList(primaryActionsRef);
        secondaryActions = CloneActionList(secondaryActionsRef);
    }

    private List<ItemAction> CloneActionList(List<ItemAction> templates)
    {
        var list = new List<ItemAction>();
        if (templates == null) return list;

        for (int i = 0; i < templates.Count; i++)
        {
            var template = templates[i];
            if (template == null)
            {
                list.Add(null);
                continue;
            }

            var clone = Instantiate(template);
            clone.InitializeRuntimeForItem( this, i);
            list.Add(clone);
        }

        return list;
    }

    private void UpdateActionsToNewCaster(CastActionController cAC)
    {
        for (int i = 0; i < primaryActions.Count; i++)
        {
            var prima = primaryActions[i];
            if (prima != null)
                prima.InitializeRuntimeForItem( this, i);
        }

        for (int i = 0; i < secondaryActions.Count; i++)
        {
            var seconda = secondaryActions[i];
            if (seconda != null)
                seconda.InitializeRuntimeForItem( this, i);
        }
    }

    #endregion



    #region posesAndItemAnims

    [Header("Idle Pose Settings")]
    public Vector3 idleLocalPos = new Vector3(0.3f, -0.25f, 0.6f);
    public Vector3 idleLocalRotEuler = Vector3.zero;

    private void SimulatePhysics(HybridCharacterController hcc, float dt)
    {
        EyePosAndLookDir eye = hcc.GetEyePosAndLookDirSmoothed();

        if (!GetTargetPose(ItemActionData, eye, dt, out Vector3 targetPos, out Quaternion targetRot))
            return; 

        Vector3 currentLinVel = LinVel;
        Vector3 currentAngVel = AngVel;

        Vector3 ownerVel = hcc.calculatedFixedVel;
        Vector3 ownerAccel = hcc.calculatedFixedAccel;
        //Vector3 ownerVel = hcc.rendererVelocity * hcc.lastRenderDt / Runner.DeltaTime;
        //Vector3 ownerAccel = hcc.rendererAccel * hcc.lastRenderDt / Runner.DeltaTime; 

        if (pdSettings != null)
        {
            pdSettings.CalculateStep(
                networkedRB.Rigidbody.position, networkedRB.Rigidbody.rotation,
                targetPos, targetRot,
                ownerVel, ownerAccel,
                dt,
                ref currentLinVel, ref currentAngVel,
                out Vector3 newPos, out Quaternion newRot
            );

            networkedRB.Rigidbody.MovePosition(newPos);
            networkedRB.Rigidbody.MoveRotation(newRot);
        }

        LinVel = currentLinVel;
        AngVel = currentAngVel;
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

    private bool GetTargetPose(NetworkItemActionData data, EyePosAndLookDir eye, float dt, out Vector3 pos, out Quaternion rot)
    {
        Quaternion viewRot = Quaternion.LookRotation(eye.Forward, eye.Up);
        Quaternion idleRotOffset = Quaternion.Euler(idleLocalRotEuler); 

        Vector3 idleWorldPos = eye.EyePosition + (eye.Right * idleLocalPos.x) + (eye.Up * idleLocalPos.y) + (eye.Forward * idleLocalPos.z);
        Quaternion idleWorldRot = viewRot * idleRotOffset;

        int actionIndex = data.actionID;
        int phaseIndex = data.phaseID;

        if (actionIndex < 0 || phaseIndex < 0 || primaryActions == null || actionIndex >= primaryActions.Count)
        {
            pos = idleWorldPos;
            rot = idleWorldRot;
            return true; 
        }

        ItemAction action = primaryActions[actionIndex];
        if (action == null)
        {
            pos = idleWorldPos;
            rot = idleWorldRot;
            return true;
        }

        ItemAnimation anim = action.GetAnimationForPhase(phaseIndex);

        if (anim == null)
        {
            pos = idleWorldPos;
            rot = idleWorldRot;
            return true;
        }

        int ticksInPhase = Runner.Tick - data.phaseStartTick;
        float phaseTime = ticksInPhase * Runner.DeltaTime;

        float sampleTime = phaseTime * anim.speedMultiplier;

        Vector3 sampledLocalPos;
        Quaternion sampledLocalRot;

        if (TrySampleFromClip(anim, sampleTime, out sampledLocalPos, out sampledLocalRot))
        {
            pos = eye.EyePosition + (viewRot * sampledLocalPos);
            rot = viewRot * sampledLocalRot;

            return true;
        }

        pos = idleWorldPos;
        rot = idleWorldRot;
        return true;
    }

    public void EnterNewPhaseAtTick(int phase, int tick, int actionId = -1, int chargeStart = -1)
    {
        if (HasInputAuthority || IsLocalPlayerHoldingThisItem())
        {
            var currentlocal = localItemActionData;
            localItemActionData = new NetworkItemActionData()
            {
                actionID = actionId == -1 ? currentlocal.actionID : actionId,
                phaseID = phase,
                phaseStartTick = tick,
                chargeStartTick = chargeStart == -1 ? currentlocal.chargeStartTick : chargeStart,
                hasFired = currentlocal.hasFired
            };
        }

        var current = ItemActionData;
        ItemActionData = new NetworkItemActionData()
        {
            actionID = actionId == -1 ? current.actionID : actionId,
            phaseID = phase,
            phaseStartTick = tick,
            chargeStartTick = chargeStart == -1 ? current.chargeStartTick : chargeStart,
            hasFired = current.hasFired
        };
    }

    public void MarkFired()
    {
        if (HasInputAuthority || IsLocalPlayerHoldingThisItem())
        {
            var currentlocal = localItemActionData;
            currentlocal.hasFired = true;
            localItemActionData = currentlocal;
        }

        var current = ItemActionData;
        current.hasFired = true;
        ItemActionData = current;
    }

    public void ClearItemActionData()
    {
        if (HasInputAuthority || IsLocalPlayerHoldingThisItem())
        {
            localItemActionData = new NetworkItemActionData()
            {
                actionID = -1,
                phaseID = -1,
                phaseStartTick = 0,
                hasFired = false,
                chargeStartTick = 0
            };
        }

        ItemActionData = new NetworkItemActionData()
        {
            actionID = -1,
            phaseID = -1,
            phaseStartTick = 0,
            hasFired = false,
            chargeStartTick = 0
        };
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

        if (itemAnim == null || itemAnim.clip == null || _ghostSamplerPivot == null)
            return false;

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
        if (activeCaster == null) return false;
        return activeCaster.GetComponent<NetworkObject>().InputAuthority == Runner.LocalPlayer;
    }

    #endregion

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Clean up the unparented visual model to prevent scene leaks
        if (visualModel != null)
        {
            Destroy(visualModel.gameObject);
        }

        CleanupAnimClipSampler();
    }

    public void RestCastingState()
    {
        ClearItemActionData();
        ClearSpellState();
        activeCaster = null;
        activeHolder = null;
    }

    public void ClearSpellState()
    {
        activeCast = null;
    }

    public Transform GetHandle(bool isLeft)
    {
        if (isLeft && secondaryHandle != null) return secondaryHandle;
        return primaryHandle;
    }

}

public struct NetworkItemActionData : INetworkStruct
{
    public int actionID;
    public int phaseID;
    public int phaseStartTick;

    public int chargeStartTick;

    public NetworkBool hasFired;
}