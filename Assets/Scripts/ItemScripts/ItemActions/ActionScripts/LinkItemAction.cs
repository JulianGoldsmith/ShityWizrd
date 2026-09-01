using Fusion;
using UnityEngine;

[CreateAssetMenu(fileName = "LinkItemAction", menuName = "Items/Actions/Link")]
public class LinkItemAction : ItemAction
{
    private enum Phase
    {
        SelectEndpoint
    }

    public override bool IsImplemented => true;

    [Header("Animation")]
    public ItemAnimation selectAnimation;

    public override bool TryDeriveActionContext(in NetworkPlayerActionData actionData, int currentTick, out DerivedActionContext context)
    {
        context = default;

        if (!actionData.IsValid) return false;
        if (currentTick < actionData.StartTick) return false;

        bool isComplete = currentTick > actionData.StartTick;
        context = CreateDerivedContext(actionData, currentTick, (int)Phase.SelectEndpoint, actionData.StartTick, 1, isComplete);
        return true;
    }

    public override void Tick(PlayerActionManager manager, EquipableItem item, in DerivedActionContext context)
    {
        if (context.IsComplete || !context.IsActionStart) return;
        if (!manager.HasStateAuthority || !manager.Runner.IsForward) return;
        if (!context.ActionData.InteractionTarget.IsValid) return;

        NetworkInteractionTarget target = context.ActionData.InteractionTarget;
        CasterLinkController linkController = manager.GetComponent<CasterLinkController>();

        LinkEndpoint endpoint;
        string targetName;

        if (target.Type == InteractionTargetType.WorldPoint)
        {
            endpoint = new LinkEndpoint
            {
                Kind = LinkEndpointKind.WorldPoint,
                ObjectId = default,
                Anchor = target.HitPoint
            };

            targetName = "world";
        }
        else
        {
            if (!manager.Runner.TryFindObject(target.ObjectId, out NetworkObject targetObject)) return;
            if (!targetObject.TryGetComponent(out Rigidbody targetBody)) return;

            endpoint = new LinkEndpoint
            {
                Kind = LinkEndpointKind.NetworkBody,
                ObjectId = targetObject.Id,
                Anchor = targetBody.transform.InverseTransformPoint(target.HitPoint)
            };

            targetName = targetObject.name;
        }

        if (!linkController.HasPendingEndpoint)
        {
            linkController.PendingEndpoint = endpoint;
            linkController.HasPendingEndpoint = true;

            Debug.Log($"[LinkItemAction] Selected endpoint A: {targetName}.");
            return;
        }

        RuntimeSpell runtimeSpell = SpellBlueprintLibrary.Get(context.ActionData.SpellID);
        RuntimeLink runtimeLink = (RuntimeLink)runtimeSpell.RootNode;
        RuntimeTetherLaw tetherLaw = (RuntimeTetherLaw)runtimeLink.Law;

        int slot = linkController.CreateLink(
            context.ActionData.CastID,
            linkController.PendingEndpoint,
            endpoint,
            tetherLaw.MaximumLength,
            tetherLaw.BreakForce,
            tetherLaw.Compliance,
            tetherLaw.Damping,
            runtimeLink.Duration,
            runtimeLink.SpellLoad);

        if (slot == -1) return;

        linkController.PendingEndpoint = default;
        linkController.HasPendingEndpoint = false;

        Debug.Log($"[LinkItemAction] Created tether in slot {slot}.");
    }

    public override ItemAnimation GetAnimationForPhase(int phaseID)
    {
        return selectAnimation;
    }
}