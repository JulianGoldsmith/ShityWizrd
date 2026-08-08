using Fusion;
using UnityEngine;

[System.Serializable]
public struct NetworkPlayerActionData : INetworkStruct
{
    public NetworkBool Valid;
    public int Revision;

    public NetworkId ItemID;
    public ItemActionChannel Channel;
    public int ActionID;
    public int ComboIndex;

    public SpellGraphId SpellID;
    public EntryPointType EntryPointType;

    public int StartTick;
    public int ReleaseTick;

    public ActiveCastID CastID;
    public NetworkInteractionTarget InteractionTarget;

    public bool IsValid => Valid && ItemID.IsValid && Channel != ItemActionChannel.None && ActionID >= 0;
    public bool HasReleased => ReleaseTick >= 0;
}

[RequireComponent(typeof(NetworkedInventoryManager))]
[RequireComponent(typeof(PlayerCastActionController))]
public class PlayerActionManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkedInventoryManager inventory;
    [SerializeField] private PlayerCastActionController castController;

    [Header("Networked Action State")]
    [Networked] public NetworkPlayerActionData CurrentAction { get; set; }
    [Networked] private int ActionRevisionCounter { get; set; }

    [Networked] private NetworkButtons PriorButtons { get; set; }

    public bool HasActiveAction => CurrentAction.IsValid;
    public NetworkedInventoryManager Inventory => inventory;
    public PlayerCastActionController CastController => castController;

    public override void Spawned()
    {
        if (inventory == null) inventory = GetComponent<NetworkedInventoryManager>();
        if (castController == null) castController = GetComponent<PlayerCastActionController>();
    }

    #region INPUT  + FIXED UPDATE 
    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData input))
        {
            if (castController != null) castController.SetSimulationLookDirection(input.lookRotation);

            if (input.buttons.WasReleased(PriorButtons, EInputButton.LEFT_CLICK))
                HandleInputEvent(ItemActionChannel.Primary, false, default);

            if (input.buttons.WasReleased(PriorButtons, EInputButton.FEED))
                HandleInputEvent(ItemActionChannel.Feed, false, default);

            if (input.buttons.WasPressed(PriorButtons, EInputButton.LEFT_CLICK))
                HandleInputEvent(ItemActionChannel.Primary, true, input.interactionTarget);

            if (input.buttons.WasPressed(PriorButtons, EInputButton.FEED))
                HandleInputEvent(ItemActionChannel.Feed, true, input.interactionTarget);

            PriorButtons = input.buttons;
        }

        TickCurrentAction();
    }


    private void HandleInputEvent(ItemActionChannel channel, bool isPress, NetworkInteractionTarget interactionTarget)
    {
        if (isPress)
        {
            if (inventory == null || inventory.activeItem == null) return;
            if (!inventory.activeItem.TryGetComponent(out EquipableItem item)) return;

            TryStartAction(item.Object, channel, 0, interactionTarget);
            return;
        }

        TryReleaseAction(channel);
    }

    public void ReleaseCurrentAction()
    {
        if (!CurrentAction.IsValid) return;
        TryReleaseAction(CurrentAction.Channel);
    }

    #endregion


    public bool TryStartAction(NetworkObject itemObject, ItemActionChannel channel, int comboIndex, NetworkInteractionTarget interactionTarget)
    {
        if (!CanAuthorActionState()) return false;
        if (HasActiveAction) return false;
        if (itemObject == null || !itemObject.IsValid) return false;
        if (channel == ItemActionChannel.None) return false;
        if (!itemObject.TryGetComponent(out EquipableItem item)) return false;
        if (item.HoldingPlayer != Object) return false;

        ItemAction action;
        SpellGraphId spellID = default;
        EntryPointType entryPointType = default;
        int actionID;

        if (channel == ItemActionChannel.Feed)
        {
            actionID = 0;
            action = item.GetAction(channel, actionID);
        }
        else
        {
            if (!item.TryResolveCast(channel, out entryPointType, out action, out spellID)) return false;
            actionID = (int)entryPointType;
        }

        if (action == null || !action.IsImplemented) return false;

        ActiveCastID castID = default;

        if (action.CreatesSpellState)
        {
            if (castController == null || spellID.IsNull()) return false;
            castID = castController.GenerateNewCastID();
        }

        ActionRevisionCounter = ActionRevisionCounter == int.MaxValue ? 1 : ActionRevisionCounter + 1;

        CurrentAction = new NetworkPlayerActionData
        {
            Valid = true,
            Revision = ActionRevisionCounter,
            ItemID = itemObject.Id,
            Channel = channel,
            ActionID = actionID,
            ComboIndex = comboIndex,
            SpellID = spellID,
            EntryPointType = entryPointType,
            StartTick = Runner.Tick,
            ReleaseTick = -1,
            CastID = castID,
            InteractionTarget = interactionTarget
        };

        return true;
    }

    public bool TryReleaseAction(ItemActionChannel channel)
    {
        if (!CanAuthorActionState()) return false;

        NetworkPlayerActionData action = CurrentAction;

        if (!action.IsValid) return false;
        if (action.Channel != channel) return false;
        if (action.HasReleased) return false;

        action.ReleaseTick = Runner.Tick;
        CurrentAction = action;
        return true;
    }

    public bool TryClearAction(int expectedRevision)
    {
        if (!CanAuthorActionState()) return false;
        if (!CurrentAction.IsValid) return false;
        if (CurrentAction.Revision != expectedRevision) return false;

        CurrentAction = default;
        return true;
    }

    public bool TryResolveCurrentItem(out EquipableItem item)
    {
        item = null;

        if (!CurrentAction.IsValid) return false;
        if (!Runner.TryFindObject(CurrentAction.ItemID, out NetworkObject itemObject)) return false;

        return itemObject.TryGetComponent(out item);
    }

    private bool CanAuthorActionState()
    {
        return HasStateAuthority || HasInputAuthority;
    }

    private void TickCurrentAction()
    {
        NetworkPlayerActionData actionData = CurrentAction;
        if (!actionData.IsValid) return;

        if (!TryResolveCurrentItem(out EquipableItem item))
        {
            if (CanAuthorActionState()) TryClearAction(actionData.Revision);
            return;
        }

        if (item.HoldingPlayer != Object)
        {
            if (CanAuthorActionState()) TryClearAction(actionData.Revision);
            return;
        }

        if (!TryGetActionContextForItem(item, Runner.Tick, out ItemAction action, out DerivedActionContext context))
        {
            if (CanAuthorActionState()) TryClearAction(actionData.Revision);
            return;
        }

        action.Tick(this, item, context);

        if (context.IsComplete) CompleteCurrentAction(item, actionData.Revision);
    }

    public bool TryGetActionContextForItem(EquipableItem item, int currentTick, out ItemAction action, out DerivedActionContext context)
    {
        action = null;
        context = default;

        NetworkPlayerActionData actionData = CurrentAction;

        if (!actionData.IsValid) return false;
        if (item == null || item.Object == null) return false;
        if (item.Object.Id != actionData.ItemID) return false;

        action = item.GetAction(actionData.Channel, actionData.ActionID);

        if (action == null || !action.IsImplemented) return false;
        return action.TryDeriveActionContext(actionData, currentTick, out context);
    }

    private void CompleteCurrentAction(EquipableItem item, int expectedRevision)
    {
        if (castController != null) castController.isCasting = false;
        if (!CanAuthorActionState()) return;

        item.ClearSpellState();
        TryClearAction(expectedRevision);
    }

 
}