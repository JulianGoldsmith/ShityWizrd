using UnityEngine;

[CreateAssetMenu(fileName = "ChannelItemAction", menuName = "Items/Actions/Channel Item Action")]
public class ChannelItemAction : ItemAction
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

    [Tooltip("Maximum number of ticks spent channeling. Zero means infinite.")]
    [TickDuration(0)] public int maxChannelTicks;

    public override bool TryDeriveActionContext(in NetworkPlayerActionData actionData, int currentTick, out DerivedActionContext context)
    {
        context = default;

        if (!actionData.IsValid) return false;
        if (currentTick < actionData.StartTick) return false;

        int windupEndTick = actionData.StartTick + windupTicks;
        int releaseStartTick = GetReleaseStartTick(actionData, windupEndTick);

        if (releaseStartTick < windupEndTick)
        {
            if (currentTick < releaseStartTick)
            {
                context = CreateDerivedContext(actionData, currentTick, (int)Phase.Windup, actionData.StartTick, windupTicks);
                return true;
            }

            return CreateReleaseContext(actionData, currentTick, releaseStartTick, out context);
        }

        if (currentTick < windupEndTick)
        {
            context = CreateDerivedContext(actionData, currentTick, (int)Phase.Windup, actionData.StartTick, windupTicks);
            return true;
        }

        if (currentTick < releaseStartTick)
        {
            int holdDurationTicks = releaseStartTick == int.MaxValue ? 0 : releaseStartTick - windupEndTick;
            context = CreateDerivedContext(actionData, currentTick, (int)Phase.Hold, windupEndTick, holdDurationTicks);
            return true;
        }

        return CreateReleaseContext(actionData, currentTick, releaseStartTick, out context);
    }

    public override void Tick(PlayerActionManager manager, EquipableItem item, in DerivedActionContext context)
    {
        if (context.IsComplete) return;

        SpellState state = EnsureSpellState(manager, item, context);
        if (state == null) return;

        manager.CastController.isCasting = true;

        Phase phase = (Phase)context.PhaseID;

        if (phase == Phase.Hold)
        {
            state.isHeld = true;

            if (context.IsPhaseStart) StartChanneling(manager, item, state);
            return;
        }

        if (phase == Phase.Release)
        {
            state.isHeld = false;

            if (context.IsPhaseStart) StopChanneling(manager, state);
        }
    }

    private int GetReleaseStartTick(in NetworkPlayerActionData actionData, int windupEndTick)
    {
        if (actionData.HasReleased) return actionData.ReleaseTick;
        if (maxChannelTicks > 0) return windupEndTick + maxChannelTicks;

        return int.MaxValue;
    }

    private bool CreateReleaseContext(in NetworkPlayerActionData actionData, int currentTick, int releaseStartTick, out DerivedActionContext context)
    {
        int releaseEndTick = releaseStartTick + releaseTicks;
        bool isComplete = currentTick >= releaseEndTick;

        context = CreateDerivedContext(actionData, currentTick, (int)Phase.Release, releaseStartTick, releaseTicks, isComplete);
        return true;
    }

    private void StartChanneling(PlayerActionManager manager, EquipableItem item, SpellState state)
    {
        CastActionController controller = manager.CastController;

        if (controller == null) return;
        if (!controller.TryGetComponent(out VirtualCoreController virtualCoreController)) return;

        Vector3 spawnPosition = item.projectileSpawnPoint != null ? item.projectileSpawnPoint.position : controller.GetSpellCastPoint();
        Vector3 castDirection = controller.GetSpellCastDir();

        state.CastPosition = spawnPosition;
        state.CastRotation = Quaternion.LookRotation(castDirection);
        state.CastAimTargetPos = controller.GetAimTarget();
        state.CastVelocity = castDirection;
        state.CastChargeLevel = 1f;

        CoreContext coreContext = new CoreContext
        {
            SpawnPosition = spawnPosition,
            TriggerVector = castDirection,
            CastChargeLevel = 1f,
            OriginalCaster = manager.Object.Id
        };

        virtualCoreController.StartVirtualCore(state.ActiveCastID, state.SpellGraphIdFrom, 0, coreContext);
    }

    private void StopChanneling(PlayerActionManager manager, SpellState state)
    {
        if (manager.CastController != null && manager.CastController.TryGetComponent(out VirtualCoreController virtualCoreController))
            virtualCoreController.StopVirtualCore(state.ActiveCastID);

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

    private void OnValidate()
    {
        windupTicks = Mathf.Max(0, windupTicks);
        releaseTicks = Mathf.Max(1, releaseTicks);
        maxChannelTicks = Mathf.Max(0, maxChannelTicks);
    }
}