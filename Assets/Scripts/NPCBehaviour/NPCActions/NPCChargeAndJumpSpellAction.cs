using Fusion;
using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NPCChargeSpellAction", menuName = "AI Commands/Actions/Charge Spell AND Jump")]
public class NPCChargeAndJumpSpellAction : NPCAction
{
    [Header("Animation Settings")]
    [Tooltip("The prefix for the animator triggers (e.g., 'Attack_1' will fire 'Attack_1_Windup')")]
    public string animPrefix = "Attack_1";

    [Header("Spell Settings")]
    [Tooltip("The index of the spell in the StaticSpellDatabase (e.g., 0 for Fireball)")]
    public int staticSpellIndex = 0;

    [Header("Action Timings ")]
    [Tooltip("How long the NPC plays the Windup animation before charging")]
    public float windupTime = 0.5f;

    [Tooltip("How long the AI holds the charge before releasing")]
    public float npcHoldTime = 1.0f;

    [Tooltip("The exact second during the release animation the fireball physically spawns")]
    public float firePointInRelease = 0.1f;

    [Tooltip("How long the Release animation takes before the AI can move again")]
    public float releaseTime = 0.5f;

    [Header("Charge Power Matrix")]
    [Min(0f)] public float minChargeTime = 0.1f;
    [Min(0.01f)] public float maxChargeTime = 1.5f;
    public float chargePowerMult = 50f;

    public override void OnStart(int comboIndex)
    {
        // 1. Everyone (Host & Proxy) fires the initial Windup trigger deterministically!
        Manager.networkAnimator.SetTrigger($"{animPrefix}_Windup");

        SpellGraphId staticId = new SpellGraphId(PlayerRef.None, staticSpellIndex + 1);
        SpellGraph graphToCast = SpellStateManager.instance.GetSpellGraph(staticId);

        if (graphToCast != null)
        {
            // 3. Generate the Networked SpellState so Host and Proxies start the VFX
            CreateAndRegisterSpellState(comboIndex, graphToCast);
        }
        else
        {
            Debug.LogError($"[NPCAction] Failed to find static spell at index {staticSpellIndex + 1}! Did you add it to the Database?");
        }
    }

    public override void OnEnd(int comboIndex)
    {
        // If the Behavior Tree aborts this action early (e.g. NPC got stunned), kill the spell safely.
        var state = Manager.GetActiveSpellState(comboIndex);
        if (state != null)
        {
            state.isHeld = false;
            RemoveCastingToken(state);
        }
    }

    public override void Tick(int comboIndex, float deltaTime)
    {
        var data = Manager.ActionData;
        int ticksInPhase = Manager.Runner.Tick - data.phaseStartTick;
        float timeInPhase = ticksInPhase * Manager.Runner.DeltaTime;

        // Everyone (Host & Proxy) runs this State Machine to forward-predict the visual transitions!
        switch (data.phaseID)
        {
            case 1: // WINDUP
                if (timeInPhase >= windupTime)
                {
                    // Everyone fires the Hold trigger
                    Manager.networkAnimator.SetTrigger($"{animPrefix}_Hold");
                    Manager.activeRagdollController.TriggerJump();
                    data.phaseID = 2;
                    data.phaseStartTick = Manager.Runner.Tick;
                    Manager.ActionData = data;
                }
                break;

            case 2: // HOLD (Charging)
                if (timeInPhase >= npcHoldTime)
                {
                    // Everyone fires the Release trigger
                    Manager.networkAnimator.SetTrigger($"{animPrefix}_Release");
                    Manager.activeRagdollController.TriggerJump();
                    data.phaseID = 3;
                    data.phaseStartTick = Manager.Runner.Tick;
                    Manager.ActionData = data;
                }
                break;

            case 3: // RELEASE
                // Has the physics clock reached the exact frame the fireball leaves the hand?
                if (!data.hasFired && timeInPhase >= firePointInRelease)
                {
                    ExecuteSpell(comboIndex, data);
                    
                    data.hasFired = true;
                    Manager.ActionData = data;
                }

                // Has the follow-through animation completely finished?
                if (timeInPhase >= releaseTime)
                {
                    // ONLY the Host officially ends the action to reset the state block cleanly
                    if (Manager.HasStateAuthority)
                    {
                        Manager.EndCurrentAction();
                    }
                }
                break;
        }
    }

    private void ExecuteSpell(int comboIndex, NetworkNPCActionData data)
    {
        if (!Manager.HasStateAuthority)
        {
            if (Manager.Runner.IsForward)
            {
                Debug.Log($"NPC spell Executed on {(Manager.HasInputAuthority ? "Client" : "Proxy")}");
            }
        }
        SpellState state = Manager.GetActiveSpellState(comboIndex);
        if (state == null) return;

        // 1. Calculate the exact math of the charge power
        int chargeTicks = Manager.Runner.Tick - data.chargeStartTick;
        float chargeDuration = chargeTicks * Manager.Runner.DeltaTime;
        float chargeT = Mathf.InverseLerp(minChargeTime, maxChargeTime, chargeDuration);

        state.CastChargeLevel = Mathf.Clamp01(chargeT) * chargePowerMult;
        state.isHeld = false;

        // 2. Build the Trigger Data (Aiming exactly where the Manager tells us to)
        SpellGraph graph = state.Spell;
        EyePosAndLookDir eyeInfo = Manager.GetEyePosAndLookDir();


        Vector3 spawnPosition = Manager.GetSpellCastPoint();
        Quaternion spawnRotation = Quaternion.LookRotation(Manager.GetForward());

        var triggerInfo = new SpellTriggerInfo(
            isCast: true,
            source: Manager.gameObject,
            state: state,
            position: spawnPosition,
            rotation: spawnRotation,
            triggerVector: Manager.GetForward() * state.CastChargeLevel,
            hitObject: Manager.gameObject
        );

        triggerInfo.State.CastAimTargetPos = Manager.GetAimTarget();
        state.CastRotation = spawnRotation;
        state.CastPosition = spawnPosition;

        // 3. FIRE THE SPELL!
        // (Host runs the actual physics colliders; Proxy locally spawns the visual VFX!)
        graph.ExecuteComboIndex(comboIndex, state, Manager);

        // 4. Clean up the garbage collection tokens
        RemoveCastingToken(state);
    }
}