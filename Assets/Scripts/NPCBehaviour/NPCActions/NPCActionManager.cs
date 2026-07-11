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
    public NPCAggroController aggroController;
    public NetworkObjectBuffer networkObjectBuffer;
    public NPCActiveRagdollController activeRagdollController;

    [Header("Actions")]
    public List<NPCAction> actionTemplates = new List<NPCAction>();
    private List<NPCAction> _runtimeActions = new List<NPCAction>();

    public List<Vector3> spellCastPoints = new List<Vector3>();

    [Tooltip("Hitboxes for melee actions")]
    public List<HitBoxBehaviour> hitboxes = new List<HitBoxBehaviour>();

    [Header("Networked State")]
    [Networked] public NetworkNPCActionData ActionData { get; set; }
    [Networked] public ActiveCastID CurrentCastID { get; set; }

    public override void Spawned()
    {
        base.Spawned(); // This initializes CastTracker in the base class!

        if (networkAnimator == null) networkAnimator = GetComponent<NetworkAnimator>();
        if (movementManager == null) movementManager = GetComponent<NPCMovementManager>();
        if (aggroController == null) aggroController = GetComponent<NPCAggroController>();
        if (networkObjectBuffer == null) networkObjectBuffer = GetComponent<NetworkObjectBuffer>();
        if (activeRagdollController == null) activeRagdollController = GetComponent<NPCActiveRagdollController>();

        ClearActionState();

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

    public override void FixedUpdateNetwork()
    {
        // If we are currently executing an action, tick it!
        if (ActionData.actionID != -1 && ActionData.actionID < _runtimeActions.Count)
        {
            NPCAction activeAction = _runtimeActions[ActionData.actionID];
            if (activeAction != null)
            {
                // Pass the delta time down to the Action Brain
                activeAction.Tick(ActionData.actionID, Runner.DeltaTime);
            }
        }
    }

    public void Tick()
    {
        if (ActionData.actionID != -1 && ActionData.actionID < _runtimeActions.Count)
        {
            NPCAction activeAction = _runtimeActions[ActionData.actionID];
            if (activeAction != null)
            {
                activeAction.Tick(ActionData.actionID, Runner.DeltaTime);
            }
        }
    }

    // ==========================================
    // ACTION CONTROL API (Called by BT Commands)
    // ==========================================

    public void StartAction(int actionID)
    {
        if (isCasting) return;
        if (actionID < 0 || actionID >= _runtimeActions.Count || _runtimeActions[actionID] == null) return;

        if (HasStateAuthority)
        {
            ActionData = new NetworkNPCActionData
            {
                actionID = actionID,
                phaseID = 1, // Start at Phase 1 (Windup)
                phaseStartTick = Runner.Tick,
                chargeStartTick = Runner.Tick,
                hasFired = false
            };
        }

        isCasting = true;
        _runtimeActions[actionID].OnStart(actionID);
    }

    public void EndCurrentAction()
    {
        if (!isCasting) return;

        int currentID = ActionData.actionID;
        if (currentID >= 0 && currentID < _runtimeActions.Count && _runtimeActions[currentID] != null)
        {
            _runtimeActions[currentID].OnEnd(currentID);
        }

        ClearActionState();
        isCasting = false;
    }

    public override void EndCast()
    {
        EndCurrentAction();
    }

    public void ClearActionState()
    {
        if (HasStateAuthority)
        {
            ActionData = new NetworkNPCActionData
            {
                actionID = -1,
                phaseID = -1,
                phaseStartTick = 0,
                chargeStartTick = 0,
                hasFired = false
            };
        }
    }

    // ==========================================
    // THE SPATIAL CONTRACT (Fulfilling the Base)
    // ==========================================

    public override Vector3 GetAimTarget()
    {
        if (aggroController != null && aggroController.CurrentTarget != null)
        {
            // Aim at the chest, not the toes
            return aggroController.CurrentTarget.transform.position + (Vector3.up * 1.2f);
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