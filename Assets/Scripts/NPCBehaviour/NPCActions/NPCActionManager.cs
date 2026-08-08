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

    public List<Vector3> spellCastPoints = new List<Vector3>();

    [Tooltip("Hitboxes for melee actions")]
    public List<HitBoxBehaviour> hitboxes = new List<HitBoxBehaviour>();

    [Header("Networked State")]
    [Networked] public NetworkNPCActionChannelState ActionChannel { get; set; }
    [Networked] public ActiveCastID CurrentCastID { get; set; }

    public NetworkNPCActionData ActionData => ActionChannel.activeAction;

    public bool HasActiveAction => ActionData.isActive;
    public bool HasPendingAction => ActionChannel.pendingAction.isValid;
    public bool IsActionChannelBusy => HasActiveAction || HasPendingAction;

    public bool CanStartAction(int actionID)
    {
        if (!HasStateAuthority || Runner == null) return false;
        if (IsActionChannelBusy) return false;

        return IsValidAction(actionID);
    }

    public override void Spawned()
    {
        base.Spawned();

        if (networkAnimator == null) networkAnimator = GetComponent<NetworkAnimator>();
        if (movementManager == null) movementManager = GetComponent<NPCMovementManager>();
        if (networkObjectBuffer == null) networkObjectBuffer = GetComponent<NetworkObjectBuffer>();
        if (activeRagdollController == null) activeRagdollController = GetComponent<NPCActiveRagdollController>();

        if (HasStateAuthority) ActionChannel = default;

        isCasting = false;
    }

    public void TickActionChannel()
    {
        if (Runner == null) return;

        if (HasStateAuthority) TryPromotePendingAction();

        NetworkNPCActionData actionData = ActionData;

        if (!actionData.IsValid)
        {
            isCasting = false;
            return;
        }

        NPCAction action = GetAction(actionData.actionID);

        if (action == null || !action.IsImplemented)
        {
            isCasting = false;

            if (HasStateAuthority) CompleteActiveAction(actionData.revision);
            return;
        }

        if (!action.TryDeriveActionContext(actionData, Runner.Tick, out DerivedNPCActionContext context))
        {
            isCasting = false;

            if (HasStateAuthority) CompleteActiveAction(actionData.revision);
            return;
        }

        isCasting = action.CreatesSpellState && !context.IsComplete;

        action.Tick(this, context);

        if (context.IsComplete && HasStateAuthority)
        {
            CompleteActiveAction(actionData.revision);
            TryPromotePendingAction();
        }
    }

    private void CompleteActiveAction(int expectedRevision)
    {
        NetworkNPCActionChannelState channel = ActionChannel;

        if (!channel.activeAction.isActive) return;
        if (channel.activeAction.revision != expectedRevision) return;

        channel.activeAction = default;
        ActionChannel = channel;
    }

    public bool TryStartAction(int actionID, NetworkId targetID, int earliestStartTick)
    {
        if (!CanStartAction(actionID)) return false;

        return TryScheduleAction(actionID, targetID, earliestStartTick);
    }

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
        if (Runner == null) return;

        TryStartAction(actionID, targetID, Runner.Tick);
    }

    public void EndCurrentAction()
    {
        if (!HasStateAuthority) return;

        NetworkNPCActionData actionData = ActionData;
        if (!actionData.isActive) return;

        CompleteActiveAction(actionData.revision);
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

    public NPCAction GetAction(int actionID)
    {
        if (actionID < 0 || actionID >= actionTemplates.Count) return null;
        return actionTemplates[actionID];
    }

    private bool IsValidAction(int actionID)
    {
        NPCAction action = GetAction(actionID);
        return action != null && action.IsImplemented;
    }

    private bool TryPromotePendingAction()
    {
        NetworkNPCActionChannelState channel = ActionChannel;

        if (channel.activeAction.isActive) return false;
        if (!channel.pendingAction.isValid) return false;
        if (Runner.Tick < channel.pendingAction.earliestStartTick) return false;

        NetworkNPCActionRequest request = channel.pendingAction;
        NPCAction action = GetAction(request.actionID);

        if (action == null || !action.IsImplemented)
        {
            channel.pendingAction = default;
            ActionChannel = channel;
            return false;
        }

        ActiveCastID castID = default;

        if (action.CreatesSpellState)
            castID = GenerateNewCastID();

        channel.activeAction = new NetworkNPCActionData
        {
            isActive = true,
            actionID = request.actionID,
            targetID = request.targetID,
            revision = request.revision,
            startTick = Runner.Tick,
            castID = castID
        };

        channel.pendingAction = default;
        ActionChannel = channel;
        return true;
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
