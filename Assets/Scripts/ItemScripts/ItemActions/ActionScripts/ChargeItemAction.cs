using Fusion;
using UnityEngine;

[CreateAssetMenu(fileName = "ChargeItemAction", menuName = "Items/Actions/Charge Item Action")]
public class ChargeItemAction : ItemAction
{
    public override bool IsImplemented => true;
    public override bool CreatesSpellState => true;

    private enum Phase
    {
        Idle,
        Windup,
        Hold,
        Release
    }

    [Header("Animations")]
    public ItemAnimation windupAnimation;
    public ItemAnimation holdAnimation;
    public ItemAnimation releaseAnimation;

    [Header("Deterministic phase timings")]
    [TickDuration(0)] public int windupTicks = 10;
    [TickDuration(1)] public int releaseTicks = 10;
    [TickDuration(0)] public int spellTickInRelease = 3;

    [Header("Charge")]
    [TickDuration(0)] public int minChargeTicks = 6;
    [TickDuration(1)] public int maxChargeTicks = 90;
    public float chargeMult = 50f;

    [Header("Cooldown & combo")]
    public float cooldown = 0.4f;
    public float comboWindow = 0.6f;

    public override bool TryDeriveActionContext(in NetworkPlayerActionData actionData, int currentTick, out DerivedActionContext context)
    {
        context = default;

        if (!actionData.IsValid) return false;
        if (currentTick < actionData.StartTick) return false;

        int windupEndTick = actionData.StartTick + windupTicks;

        if (currentTick < windupEndTick)
        {
            context = CreateDerivedContext(actionData, currentTick, (int)Phase.Windup, actionData.StartTick, windupTicks);
            return true;
        }

        if (!actionData.HasReleased)
        {
            context = CreateDerivedContext(actionData, currentTick, (int)Phase.Hold, windupEndTick, 0);
            return true;
        }

        int releaseStartTick = Mathf.Max(windupEndTick, actionData.ReleaseTick);

        if (currentTick < releaseStartTick)
        {
            context = CreateDerivedContext(actionData, currentTick, (int)Phase.Hold, windupEndTick, releaseStartTick - windupEndTick);
            return true;
        }

        int releaseEndTick = releaseStartTick + releaseTicks;
        bool isComplete = currentTick >= releaseEndTick;

        context = CreateDerivedContext(actionData, currentTick, (int)Phase.Release, releaseStartTick, releaseTicks, isComplete);
        return true;
    }

    public override void Tick(PlayerActionManager manager, EquipableItem item, in DerivedActionContext context)
    {
        if (context.IsComplete) return;

        SpellState state = EnsureSpellState(manager, item, context);
        if (state == null) return;

        manager.CastController.isCasting = true;

        bool spellHasReleased = context.PhaseID == (int)Phase.Release && context.TickInPhase >= spellTickInRelease;
        state.isHeld = !spellHasReleased;

        if (context.PhaseID != (int)Phase.Release) return;
        if (!context.IsPhaseTick(spellTickInRelease)) return;

        ExecuteSpell(manager, item, state, context);
    }

    public int GetChargeTicks(in DerivedActionContext context)
    {
        if (!context.ActionData.HasReleased) return Mathf.Max(0, context.CurrentTick - context.ActionData.StartTick);
        return Mathf.Max(0, context.ActionData.ReleaseTick - context.ActionData.StartTick);
    }

    public float GetNormalizedCharge(in DerivedActionContext context)
    {
        int chargeTicks = GetChargeTicks(context);

        if (maxChargeTicks <= minChargeTicks) return chargeTicks >= minChargeTicks ? 1f : 0f;
        return Mathf.Clamp01(Mathf.InverseLerp(minChargeTicks, maxChargeTicks, chargeTicks));
    }

    private void OnValidate()
    {
        windupTicks = Mathf.Max(0, windupTicks);
        releaseTicks = Mathf.Max(1, releaseTicks);
        spellTickInRelease = Mathf.Clamp(spellTickInRelease, 0, releaseTicks - 1);
        maxChargeTicks = Mathf.Max(minChargeTicks, maxChargeTicks);
    }


    private void ExecuteSpell(PlayerActionManager manager, EquipableItem item, SpellState state, in DerivedActionContext context)
    {
        CastActionController controller = manager.CastController;
        if (controller == null || state == null) return;

        int chargeTicks = GetChargeTicks(context);
        state.CastChargeLevel = GetNormalizedCharge(context) * chargeMult;
        state.isHeld = false;

        EyePosAndLookDir eyeInfo = controller.GetEyePosAndLookDir();

        Vector3 castDirection = controller.GetSpellCastDir();
        Vector3 spawnPosition = eyeInfo.EyePosition + eyeInfo.Forward;
        Quaternion spawnRotation = Quaternion.LookRotation(castDirection);

        state.CastPosition = spawnPosition;
        state.CastRotation = spawnRotation;
        state.CastAimTargetPos = controller.GetAimTarget();

        // This counter is local mutable state, so restore its deterministic
        // event-start value whenever this event is replayed.
        state.SpawnedCoresCounter = 0;

        SpellTriggerInfo triggerInfo = new SpellTriggerInfo(
            true,
            controller.gameObject,
            state,
            spawnPosition,
            spawnRotation,
            castDirection * state.CastChargeLevel,
            controller.gameObject
        );

        ExecuteSpawnCoreSpell(context.ActionData.SpellID, triggerInfo);
        RemoveCastingToken(state);
    }


    public override ItemAnimation GetAnimationForPhase(int phaseID)
    {
        Phase phase = (Phase)phaseID;

        switch (phase)
        {
            case Phase.Windup: return windupAnimation;
            case Phase.Hold: return holdAnimation;
            case Phase.Release: return releaseAnimation;
            default: return default;
        }
    }



}