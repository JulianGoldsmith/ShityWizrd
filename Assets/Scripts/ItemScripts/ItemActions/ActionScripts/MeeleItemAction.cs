using UnityEngine;

[CreateAssetMenu(fileName = "MeleeItemAction", menuName = "Items/Actions/Melee Item Action")]
public class MeeleItemAction : ItemAction
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
    [TickDuration(1)] public int releaseTicks = 20;
    [TickDuration(0)] public int hitStartTick = 4;
    [TickDuration(1)] public int hitEndTick = 10;

    [Header("Hitbox")]
    [Min(0)] public int hitBoxIndex;

    [Header("Charge")]
    [TickDuration(0)] public int minChargeTicks = 6;
    [TickDuration(1)] public int maxChargeTicks = 90;
    public float chargeMult = 50f;

    public override bool TryDeriveActionContext(in NetworkPlayerActionData actionData, int currentTick, out DerivedActionContext context)
    {
        context = default;

        if (!actionData.IsValid || currentTick < actionData.StartTick) return false;

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

        bool isComplete = currentTick >= releaseStartTick + releaseTicks;
        context = CreateDerivedContext(actionData, currentTick, (int)Phase.Release, releaseStartTick, releaseTicks, isComplete);
        return true;
    }

    public override void Tick(PlayerActionManager manager, EquipableItem item, in DerivedActionContext context)
    {
        MeleeExecutionCore meleeCore = manager.MeleeCore;

        if (context.IsComplete)
        {
            if (meleeCore != null) meleeCore.EndSwing(context.ActionData.CastID);

            ActiveSpell activeSpell = SpellStateManager.instance != null ? SpellStateManager.instance.GetActiveSpell(context.ActionData.CastID) : null;
            if (activeSpell != null)
            {
                activeSpell.State.isHeld = false;
                RemoveCastingToken(activeSpell.State);
            }
            return;
        }

        SpellState state = EnsureSpellState(manager, item, context);
        ItemHitBox hitBox = item.GetMeleeHitBox(hitBoxIndex);
        if (state == null || meleeCore == null || hitBox == null) return;

        manager.CastController.isCasting = true;
        state.CastChargeLevel = GetNormalizedCharge(context) * chargeMult;
        state.isHeld = context.PhaseID != (int)Phase.Release;

        bool hitBoxActive = context.PhaseID == (int)Phase.Release && context.TickInPhase >= hitStartTick && context.TickInPhase < hitEndTick;
        meleeCore.BeginSwing(context.ActionData.CastID, context.ActionData.SpellID, item, hitBox, state, context.ActionData.StartTick, hitBoxActive);
    }

    private int GetChargeTicks(in DerivedActionContext context)
    {
        if (!context.ActionData.HasReleased) return Mathf.Max(0, context.CurrentTick - context.ActionData.StartTick);
        return Mathf.Max(0, context.ActionData.ReleaseTick - context.ActionData.StartTick);
    }

    private float GetNormalizedCharge(in DerivedActionContext context)
    {
        int chargeTicks = GetChargeTicks(context);
        if (maxChargeTicks <= minChargeTicks) return chargeTicks >= minChargeTicks ? 1f : 0f;
        return Mathf.Clamp01(Mathf.InverseLerp(minChargeTicks, maxChargeTicks, chargeTicks));
    }

    public override ItemAnimation GetAnimationForPhase(int phaseID)
    {
        switch ((Phase)phaseID)
        {
            case Phase.Windup: return windupAnimation;
            case Phase.Hold: return holdAnimation;
            case Phase.Release: return releaseAnimation;
            default: return null;
        }
    }

    private void OnValidate()
    {
        windupTicks = Mathf.Max(0, windupTicks);
        releaseTicks = Mathf.Max(1, releaseTicks);
        hitStartTick = Mathf.Clamp(hitStartTick, 0, releaseTicks - 1);
        hitEndTick = Mathf.Clamp(hitEndTick, hitStartTick + 1, releaseTicks);
        maxChargeTicks = Mathf.Max(minChargeTicks, maxChargeTicks);
    }
}
