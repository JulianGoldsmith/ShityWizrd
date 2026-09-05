using Fusion;
using UnityEngine;

[CreateAssetMenu(fileName = "PlaceLinkItemAction", menuName = "Items/Actions/Place Link")]
public class PlaceLinkItemAction : ItemAction
{
    private enum Phase
    {
        Place
    }

    public override bool IsImplemented => true;
    public override bool CreatesSpellState => true;

    public ItemAnimation placeAnimation;

    public override bool TryDeriveActionContext(in NetworkPlayerActionData actionData, int currentTick, out DerivedActionContext context)
    {
        context = default;

        if (!actionData.IsValid) return false;
        if (currentTick < actionData.StartTick) return false;

        bool isComplete = currentTick > actionData.StartTick;
        context = CreateDerivedContext(actionData, currentTick, (int)Phase.Place, actionData.StartTick, 1, isComplete);
        return true;
    }

    public override void Tick(PlayerActionManager manager, EquipableItem item, in DerivedActionContext context)
    {
        if (context.IsComplete || !context.IsActionStart) return;
        if (!manager.HasStateAuthority || !manager.Runner.IsForward) return;
        if (!context.ActionData.InteractionTarget.IsValid) return;

        SpellState state = EnsureSpellState(manager, item, context);
        if (state == null) return;

        NetworkInteractionTarget target = context.ActionData.InteractionTarget;
        GameObject hitObject = null;

        if (target.Type != InteractionTargetType.WorldPoint)
        {
            if (!manager.Runner.TryFindObject(target.ObjectId, out NetworkObject targetObject)) return;
            hitObject = targetObject.gameObject;
        }

        Vector3 castDirection = manager.CastController.GetSpellCastDir();
        Quaternion placementRotation = Quaternion.LookRotation(-castDirection);
        placementRotation = Quaternion.FromToRotation(placementRotation * Vector3.up, -target.HitNormal) * placementRotation;

        state.CastPosition = target.HitPoint;
        state.CastRotation = placementRotation;
        state.CastAimTargetPos = target.HitPoint;
        state.CastVelocity = Vector3.zero;
        state.CastChargeLevel = 1f;
        state.isHeld = false;
        state.SpawnedCoresCounter = 0;

        SpellTriggerInfo triggerInfo = new SpellTriggerInfo(
            true,
            manager.CastController.gameObject,
            state,
            target.HitPoint,
            placementRotation,
            Vector3.zero,
            hitObject
        );

        ExecuteSpawnCoreSpell(context.ActionData.SpellID, triggerInfo);
        RemoveCastingToken(state);
    }

    public override ItemAnimation GetAnimationForPhase(int phaseID)
    {
        return placeAnimation;
    }
}