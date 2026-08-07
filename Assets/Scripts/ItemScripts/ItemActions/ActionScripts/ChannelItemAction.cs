/*using Fusion;
using UnityEngine;

[CreateAssetMenu(fileName = "ChannelItemAction", menuName = "Items/Actions/Channel Item Action")]
public class ChannelItemAction : ItemAction
{
    private enum Phase { Idle, Windup, Hold, Release }

    [Header("Animations")]
    public ItemAnimation windupAnimation;
    public ItemAnimation holdAnimation;
    public ItemAnimation releaseAnimation;

    [Header("Settings")]
    [Tooltip("Max time the player can channel before it auto-cancels. 0 = Infinite.")]
    public float maxChannelTime = 0f;

    public override void OnPress(int comboIndex, bool isAlreadyReleased)
    {
        Item.EnterNewPhaseAtTick((int)Phase.Windup, Item.activeCaster.Runner.Tick, comboIndex, Item.Runner.Tick);
        Item.activeCaster.isCasting = true;

        CreateAndRegisterSpellState(comboIndex);
        if (!Item.HasStateAuthority && Item.Runner.IsResimulation) Debug.Log("Channel OnPress Resim");
    }

    public override void OnRelease(int comboIndex)
    {
        var pose = Item.ItemActionData;
        if (pose.actionID != comboIndex) return;
        if ((Phase)pose.phaseID == Phase.Idle || (Phase)pose.phaseID == Phase.Release) return;

        StopChanneling();

        Item.EnterNewPhaseAtTick((int)Phase.Release, Item.Runner.Tick, comboIndex);
    }

    public override void Tick(int comboIndex, float deltaTime)
    {
        var pose = Item.ItemActionData;
        if (pose.actionID != comboIndex) return;

        Phase currentPhase = (Phase)pose.phaseID;
        int ticksInPhase = Item.Runner.Tick - pose.phaseStartTick;
        float timeInPhase = ticksInPhase * Item.Runner.DeltaTime;

        ItemAnimation currentAnim = GetAnimationForPhase((int)currentPhase);

        switch (currentPhase)
        {
            case Phase.Windup:
                if (currentAnim == null || currentAnim.IsFinished(timeInPhase))
                {
                    Item.EnterNewPhaseAtTick((int)Phase.Hold, Item.Runner.Tick, comboIndex);

                    StartChanneling();
                }
                break;

            case Phase.Hold:
                if (maxChannelTime > 0 && timeInPhase >= maxChannelTime)
                {
                    StopChanneling();
                    Item.EnterNewPhaseAtTick((int)Phase.Release, Item.Runner.Tick, comboIndex);
                }
                break;

            case Phase.Release:
                if (currentAnim == null || currentAnim.IsFinished(timeInPhase))
                {
                    Item.activeCaster.isCasting = false;
                    Item.ClearItemActionData();
                    RemoveSpellState();
                }
                break;
        }
    }

    private void StartChanneling()
    {
        if (Item.activeCaster.TryGetComponent<VirtualCoreController>(out var vcc))
        {
            SpellState state = Item.activeCast;
            if (state == null) return;

            CoreContext context = new CoreContext()
            {
                SpawnPosition = Item.projectileSpawnPoint != null ? Item.projectileSpawnPoint.position : Item.activeCaster.transform.position,
                TriggerVector = Item.activeCaster.GetSpellCastDir(),
                CastChargeLevel = 1f,
                OriginalCaster = Item.activeCaster.Object.Id,
            };

            vcc.StartVirtualCore(state.ActiveCastID, state.SpellGraphIdFrom, 0, context);
        }
    }

    private void StopChanneling()
    {
        if (Item.activeCaster.TryGetComponent<VirtualCoreController>(out var vcc))
        {
            SpellState state = Item.activeCast;
            if (state != null)
            {
                vcc.StopVirtualCore(state.ActiveCastID);
                RemoveCastingToken(state); 
            }
        }
    }

    public override ItemAnimation GetAnimationForPhase(int phaseIndex)
    {
        Phase p = (Phase)phaseIndex;
        switch (p)
        {
            case Phase.Windup: return windupAnimation;
            case Phase.Hold: return holdAnimation;
            case Phase.Release: return releaseAnimation;
            default: return null;
        }
    }

    protected override void InitializeAnimationTickCache(float dt)
    {
        if (windupAnimation != null) windupAnimation.InitializeTickCache(dt);
        if (holdAnimation != null) holdAnimation.InitializeTickCache(dt);
        if (releaseAnimation != null) releaseAnimation.InitializeTickCache(dt);
    }
}*/

using UnityEngine;

[CreateAssetMenu(fileName = "ChannelItemAction", menuName = "Items/Actions/Channel Item Action")]
public class ChannelItemAction : ItemAction
{
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

    [Header("Legacy authoring values")]
    public float maxChannelTime;

    public override ItemAnimation GetAnimationForPhase(int phaseID)
    {
        Phase phase = (Phase)phaseID;

        switch (phase)
        {
            case Phase.Windup: return windupAnimation;
            case Phase.Hold: return holdAnimation;
            case Phase.Release: return releaseAnimation;
            default: return null;
        }
    }
}