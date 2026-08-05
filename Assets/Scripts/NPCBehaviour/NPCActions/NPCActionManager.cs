using Fusion;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ActiveCastTracker))]
[RequireComponent(typeof(NetworkObjectBuffer))]
public class NPCActionManager : CastActionController
{
    [Header("Core Components")]
    public NetworkAnimator networkAnimator;
    public NPCMovementManager movementManager;
    public NetworkObjectBuffer networkObjectBuffer;
    public NPCActiveRagdollController activeRagdollController;

    [Header("Actions")]
    public List<NPCAction> actionTemplates = new List<NPCAction>();
    private List<NPCAction> _runtimeActions = new List<NPCAction>();

    public List<Vector3> spellCastPoints = new List<Vector3>();

    [Tooltip("Hitboxes for melee actions")]
    public List<HitBoxBehaviour> hitboxes = new List<HitBoxBehaviour>();

    [Header("Networked State")]
    [Networked] public NetworkNPCActionChannelState ActionChannel { get; set; }
    [Networked] public ActiveCastID CurrentCastID { get; set; }

    private int _localActiveRevision;
    private int _localActiveActionID = -1;

    public NetworkNPCActionData ActionData
    {
        get => ActionChannel.activeAction;
        set
        {
            NetworkNPCActionChannelState channel = ActionChannel;
            channel.activeAction = value;
            ActionChannel = channel;
        }
    }

    public bool HasActiveAction => ActionData.isActive;
    public bool HasPendingAction => ActionChannel.pendingAction.isValid;

    public override void Spawned()
    {
        base.Spawned(); // This initializes CastTracker in the base class!

        if (networkAnimator == null) networkAnimator = GetComponent<NetworkAnimator>();
        if (movementManager == null) movementManager = GetComponent<NPCMovementManager>();
        if (networkObjectBuffer == null) networkObjectBuffer = GetComponent<NetworkObjectBuffer>();
        if (activeRagdollController == null) activeRagdollController = GetComponent<NPCActiveRagdollController>();

        if (HasStateAuthority) ActionChannel = default;

        _localActiveRevision = 0;
        _localActiveActionID = -1;
        isCasting = false;

        _runtimeActions.Clear();
        for (int i = 0; i < actionTemplates.Count; i++)
        {
            if (actionTemplates[i] == null)
            {
                _runtimeActions.Add(null);
                continue;
            }

            NPCAction runtimeInstance = Instantiate(actionTemplates[i]);
            runtimeInstance.name = actionTemplates[i].name + " (Runtime)";
            runtimeInstance.InitializeRuntime(this, i);
            _runtimeActions.Add(runtimeInstance);
        }

        // (Note: Static Spell Hydration Logic remains exactly as you had it!)
        foreach (var action in _runtimeActions)
        {
            if (action is NPCChargeSpellAction spellAction)
            {
                SpellGraphId staticId = new SpellGraphId(PlayerRef.None, spellAction.staticSpellIndex + 1);
                SpellGraph graph = SpellStateManager.instance.GetSpellGraph(staticId);
                if (graph != null && networkObjectBuffer != null) networkObjectBuffer.Initialise(graph);
            }
            else if (action is NPCChargeAndJumpSpellAction spellAction2)
            {
                SpellGraphId staticId = new SpellGraphId(PlayerRef.None, spellAction2.staticSpellIndex + 1);
                SpellGraph graph = SpellStateManager.instance.GetSpellGraph(staticId);
                if (graph != null && networkObjectBuffer != null) networkObjectBuffer.Initialise(graph);
            }
        }
    }

    public void TickActionChannel()
    {
        if (Runner == null) return;

        TryPromotePendingAction();
        SynchronizeLocalActionLifecycle();

        NetworkNPCActionData actionData = ActionData;
        if (actionData.isActive && IsValidAction(actionData.actionID))
        {
            _runtimeActions[actionData.actionID].Tick(actionData.actionID, Runner.DeltaTime);
        }

        TryPromotePendingAction();
        SynchronizeLocalActionLifecycle();
    }

    // ==========================================
    // ACTION CHANNEL CONTROL API
    // ==========================================

    public bool TryScheduleAction(int actionID, NetworkId targetID, int earliestStartTick)
    {
        if (!HasStateAuthority || Runner == null) return false;
        if (!IsValidAction(actionID)) return false;

        NetworkNPCActionChannelState channel = ActionChannel;
        if (channel.pendingAction.isValid) return false;

        channel.revisionCounter++;
        channel.pendingAction = new NetworkNPCActionRequest
        {
            isValid = true,
            actionID = actionID,
            targetID = targetID,
            earliestStartTick = Mathf.Max(Runner.Tick, earliestStartTick),
            revision = channel.revisionCounter
        };

        ActionChannel = channel;
        return true;
    }

    public bool TryCancelPendingAction()
    {
        if (!HasStateAuthority || Runner == null) return false;

        NetworkNPCActionChannelState channel = ActionChannel;
        if (!channel.pendingAction.isValid) return true;

        channel.revisionCounter++;
        channel.pendingAction = default;
        ActionChannel = channel;
        return true;
    }

    public void StartAction(int actionID, NetworkId targetID)
    {
        if (!HasStateAuthority || Runner == null) return;
        if (HasActiveAction || HasPendingAction) return;
        TryScheduleAction(actionID, targetID, Runner.Tick);
    }

    public void EndCurrentAction()
    {
        NetworkNPCActionChannelState channel = ActionChannel;
        if (!channel.activeAction.isActive) return;

        EndLocalAction();
        channel.activeAction = default;
        ActionChannel = channel;
        isCasting = false;
    }

    public override void EndCast()
    {
        EndCurrentAction();
    }

    public void ClearActionState()
    {
        if (!HasStateAuthority) return;

        NetworkNPCActionChannelState channel = ActionChannel;
        channel.activeAction = default;
        ActionChannel = channel;
    }

    private bool IsValidAction(int actionID)
    {
        return actionID >= 0 && actionID < _runtimeActions.Count && _runtimeActions[actionID] != null;
    }

    private bool TryPromotePendingAction()
    {
        NetworkNPCActionChannelState channel = ActionChannel;
        if (channel.activeAction.isActive || !channel.pendingAction.isValid) return false;
        if (Runner.Tick < channel.pendingAction.earliestStartTick) return false;

        NetworkNPCActionRequest request = channel.pendingAction;
        if (!IsValidAction(request.actionID))
        {
            channel.pendingAction = default;
            ActionChannel = channel;
            return false;
        }

        channel.activeAction = new NetworkNPCActionData
        {
            isActive = true,
            actionID = request.actionID,
            targetID = request.targetID,
            revision = request.revision,
            startTick = Runner.Tick,
            phaseID = 1,
            phaseStartTick = Runner.Tick,
            chargeStartTick = Runner.Tick,
            hasFired = false
        };
        channel.pendingAction = default;

        ActionChannel = channel;
        return true;
    }

    private void SynchronizeLocalActionLifecycle()
    {
        NetworkNPCActionData actionData = ActionData;

        if (!actionData.isActive)
        {
            EndLocalAction();
            isCasting = false;
            return;
        }

        if (_localActiveRevision == actionData.revision && _localActiveActionID == actionData.actionID)
        {
            isCasting = true;
            return;
        }

        EndLocalAction();

        if (!IsValidAction(actionData.actionID))
        {
            isCasting = false;
            return;
        }

        _localActiveRevision = actionData.revision;
        _localActiveActionID = actionData.actionID;
        isCasting = true;
        _runtimeActions[actionData.actionID].OnStart(actionData.actionID);
    }

    private void EndLocalAction()
    {
        if (!IsValidAction(_localActiveActionID))
        {
            _localActiveRevision = 0;
            _localActiveActionID = -1;
            return;
        }

        int endedActionID = _localActiveActionID;
        _localActiveRevision = 0;
        _localActiveActionID = -1;
        _runtimeActions[endedActionID].OnEnd(endedActionID);
    }

    // ==========================================
    // THE SPATIAL CONTRACT (Fulfilling the Base)
    // ==========================================

    public bool TryGetActionTarget(out NetworkObject targetObject)
    {
        targetObject = null;
        if (Runner == null || !ActionData.isActive || !ActionData.targetID.IsValid) return false;

        return Runner.TryFindObject(ActionData.targetID, out targetObject);
    }

    public override Vector3 GetAimTarget()
    {
        if (TryGetActionTarget(out NetworkObject targetObject))
        {
            return targetObject.transform.position + (Vector3.up * 1.2f);
        }

        return transform.position + (transform.forward * 10f);
    }

    public override EyePosAndLookDir GetEyePosAndLookDir()
    {
        Vector3 eyePos = activeRagdollController.coreRB.transform.position + (Vector3.up * 1.6f);
        Vector3 lookForward = (GetAimTarget() - eyePos).normalized;

        return new EyePosAndLookDir(eyePos, lookForward, Vector3.up);
    }

    public override Vector3 GetSpellCastPoint()
    {
        if (spellCastPoints != null && spellCastPoints.Count > 0)
            return activeRagdollController.coreRB.transform.position + activeRagdollController.coreRB.transform.TransformDirection(spellCastPoints[0]);

        return activeRagdollController.coreRB.transform.position;
    }

    // ==========================================
    // THE HARDWARE CONTRACT (Hitboxes)
    // ==========================================

    public override void ActivateHitbox(int hitBoxID, SpellState state)
    {
        if (hitBoxID >= 0 && hitBoxID < hitboxes.Count && hitboxes[hitBoxID] != null)
        {
            hitboxes[hitBoxID].Initialize(this, state);
            hitboxes[hitBoxID].ResetHitBox();
            hitboxes[hitBoxID].EnableHitBox();
        }
    }

    public override void DeactivateHitbox(int hitBoxID)
    {
        if (hitBoxID >= 0 && hitBoxID < hitboxes.Count && hitboxes[hitBoxID] != null)
        {
            hitboxes[hitBoxID].DisableHitBox();
            hitboxes[hitBoxID].InitializeNull();
        }
    }
}
