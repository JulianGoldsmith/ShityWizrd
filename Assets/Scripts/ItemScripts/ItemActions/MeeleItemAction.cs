using Fusion;
using UnityEngine;

[CreateAssetMenu(fileName = "MeeleItemAction", menuName = "Items/Actions/Meele Item Action")]
public class MeeleItemAction : ItemAction
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

    [Header("Charge timings")]
    [Min(0f)] public float minChargeTime = 0.1f;
    [Min(0.01f)] public float maxChargeTime = 1.5f;
    public float chargeMult = 50f;

    [Header("Cooldown & combo")]
    public float cooldown = 0.4f;
    public float comboWindow = 0.6f;

    public override void OnPress(int comboIndex, bool isAlreadyReleased)
    {

        Item.EnterNewPhaseAtTick((int)Phase.Windup, Item.Runner.Tick, comboIndex, Item.Runner.Tick);
        Item.activeCaster.isCasting = true;

        CreateAndRegisterSpellState(comboIndex);
    }

    public override void OnRelease(int comboIndex)
    {
        var pose =/* Item.activeCaster.HasInputAuthority ? Item.localItemActionData :*/ Item.ItemActionData;
        if (pose.actionID != comboIndex) return;
        if ((Phase)pose.phaseID == Phase.Idle) return;

        Item.EnterNewPhaseAtTick((int)Phase.Release, Item.Runner.Tick, comboIndex);

        Item.activeCaster.SetCoolDown(cooldown);
        Item.activeCaster.StartComboTimer(comboWindow);

    }

    public override void Tick(int comboIndex, float deltaTime)
    {
        var pose =/* Item.activeCaster.HasInputAuthority ? Item.localItemActionData : */Item.ItemActionData;
        if (pose.actionID != comboIndex) return;

        Phase currentPhase = (Phase)pose.phaseID;

        int ticksInPhase = Item.Runner.Tick - pose.phaseStartTick;
        float timeInPhase = ticksInPhase * Item.Runner.DeltaTime;

        ItemAnimation currentAnim = GetAnimationForPhase((int)currentPhase);

        switch (currentPhase)
        {
            case Phase.Windup:
                if (currentAnim.IsFinished(timeInPhase))
                {
                    Item.EnterNewPhaseAtTick((int)Phase.Hold, Item.Runner.Tick, comboIndex);
                }
                break;

            case Phase.Hold:
                // Wait for release...
                break;

            case Phase.Release:

                //if (!pose.hasFired && (Item.HasInputAuthority || Item.HasStateAuthority))
                //{
                //    if (currentAnim.HasPassedCastPoint(timeInPhase))
                //    {
                //        int chargeTicks = Item.Runner.Tick - pose.chargeStartTick;
                //        float chargeSeconds = chargeTicks * Item.Runner.DeltaTime;
                //        ExecuteSpell(comboIndex, chargeSeconds);
                //        Item.MarkFired();
                //    }
                //}

                if (currentAnim.IsInActiveWindowTicks(ticksInPhase))
                {
                    // Enable Hitbox if not already enabled
                    // You might need a flag on the item 'isHitboxActive' to avoid calling Enable every frame
                    if (!Item.IsHitboxActive)
                    {
                        Item.EnableHitbox(0); // Index 0 = Main Blade
                        //Item.IsHitboxActive = true;
                    }
                }
                else
                {
                    // Disable if we passed the window
                    if (Item.IsHitboxActive)
                    {
                        Item.DisableHitbox(0);
                        //Item.IsHitboxActive = false;
                    }
                }

                if (currentAnim.IsFinished(timeInPhase))
                {
                    Item.activeCaster.isCasting = false;
                    Item.ClearItemActionData();
                    RemoveSpellState();
                }
                break;
        }
    }



    private void ExecuteSpell(int comboIndex, float chargeDuration)
    {
        var controller = Item.activeCaster;
        SpellState state = Item.activeCast;
        if (state == null) return;

        // Apply Charge
        float chargeT = Mathf.InverseLerp(minChargeTime, maxChargeTime, chargeDuration);
        state.CastChargeLevel = Mathf.Clamp01(chargeT) * chargeMult;
        state.isHeld = false;

        // Fire Logic
        SpellGraph graph = Item.primaryActionSpell;

        Vector3 spawnPosition = Item.projectileSpawnPoint.position;
        Quaternion spawnRotation = Quaternion.LookRotation(controller.GetForward());

        var triggerInfo = new SpellTriggerInfo(
            true,
            controller.gameObject,
            state,
            spawnPosition,
            spawnRotation,
            controller.GetForward() * state.CastChargeLevel,
            controller.gameObject
        );
        triggerInfo.State.CastAimTargetPos = controller.GetAimTarget();
        state.CastRotation = spawnRotation;
        state.CastPosition = spawnPosition;

        // Execute
        graph.ExecuteComboIndex(comboIndex, state, controller);

        Debug.Log($"Fired at {chargeDuration}s charge.");
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



}