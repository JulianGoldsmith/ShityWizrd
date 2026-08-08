using UnityEngine;

[CreateAssetMenu(fileName = "RuneFeedItemAction", menuName = "Items/Actions/Rune Feed")]
public class RuneFeedItemAction : ItemAction
{
    private enum Phase
    {
        Feed
    }

    public override bool IsImplemented => true;

    [Header("Animation")]
    public ItemAnimation feedAnimation;

    [Header("Deterministic timing")]
    [TickDuration(1)] public int durationTicks = 32;
    [TickDuration(0)] public int commitTick = 16;

    public override bool TryDeriveActionContext(in NetworkPlayerActionData actionData, int currentTick, out DerivedActionContext context)
    {
        context = default;

        if (!actionData.IsValid) return false;
        if (currentTick < actionData.StartTick) return false;

        int endTick = actionData.StartTick + durationTicks;
        bool isComplete = currentTick >= endTick;

        context = CreateDerivedContext(actionData, currentTick, (int)Phase.Feed, actionData.StartTick, durationTicks, isComplete);
        return true;
    }

    public override void Tick(PlayerActionManager manager, EquipableItem item, in DerivedActionContext context)
    {
        if (context.IsComplete) return;
        if (!context.IsPhaseTick(commitTick)) return;
        if (manager == null || manager.Runner == null) return;
        if (!manager.HasStateAuthority || !manager.Runner.IsForward) return;
        if (!item.TryGetComponent(out RuneSpellContainer container)) return;

        container.TryCommitFeed(context.ActionData.InteractionTarget);
    }

    public override ItemAnimation GetAnimationForPhase(int phaseID)
    {
        return feedAnimation;
    }

    private void OnValidate()
    {
        durationTicks = Mathf.Max(1, durationTicks);
        commitTick = Mathf.Clamp(commitTick, 0, durationTicks - 1);
    }
}