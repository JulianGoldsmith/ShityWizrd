/*using UnityEngine;

[CreateAssetMenu(fileName = "RuneFeedItemAction", menuName = "Items/Actions/Rune Feed")]
public class RuneFeedItemAction : ItemAction
{
    private enum Phase
    {
        Feed
    }

    public ItemAnimation feedAnimation;

    public override void OnPress(int comboIndex, bool isAlreadyReleased)
    {
        NetworkInteractionTarget target = Item.ItemActionData.interactionTarget;
        bool isEjecting = Item.PrimarySpellID.NotNull();

        if (!isEjecting && (!target.IsValid || target.Type != InteractionTargetType.RuneNode))
        {
            Item.ClearItemActionData();
            return;
        }

        Item.EnterNewPhaseAtTick((int)Phase.Feed, Item.Runner.Tick, comboIndex);
    }

    public override void OnRelease(int comboIndex)
    {
    }

    public override void Tick(int comboIndex, float deltaTime)
    {
        NetworkItemActionData data = Item.ItemActionData;

        if (data.channel != Channel || data.actionID != comboIndex)
            return;

        int ticksInPhase = Item.Runner.Tick - data.phaseStartTick;
        float timeInPhase = ticksInPhase * Item.Runner.DeltaTime;
        bool animationFinished = feedAnimation == null || feedAnimation.IsFinished(timeInPhase);
        bool commitReady = feedAnimation == null || feedAnimation.HasPassedCastTick(ticksInPhase) || animationFinished;

        if (!data.hasFired && commitReady)
        {
            if (Item.HasStateAuthority)
            {
                if (!Item.TryGetComponent(out RuneSpellContainer container))
                    Debug.LogWarning($"[RuneFeed] '{Item.name}' has no RuneSpellContainer.", Item);
                else if (!container.TryCommitFeed(data.interactionTarget))
                    Debug.LogWarning($"[RuneFeed] Feed transaction was rejected.", Item);
            }

            Item.MarkFired();
        }

        if (animationFinished)
            Item.ClearItemActionData();
    }

    public override ItemAnimation GetAnimationForPhase(int phaseIndex)
    {
        return feedAnimation;
    }

    protected override void InitializeAnimationTickCache(float dt)
    {
        if (feedAnimation != null)
            feedAnimation.InitializeTickCache(dt);
    }
}*/

using UnityEngine;

[CreateAssetMenu(fileName = "RuneFeedItemAction", menuName = "Items/Actions/Rune Feed")]
public class RuneFeedItemAction : ItemAction
{
    private enum Phase
    {
        Feed
    }

    public ItemAnimation feedAnimation;

    public override ItemAnimation GetAnimationForPhase(int phaseID)
    {
        return feedAnimation;
    }
}