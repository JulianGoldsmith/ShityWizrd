using UnityEngine;
using Fusion;

[RequireComponent(typeof(NetworkedInventoryManager))]
public class PlayerCastActionController : CastActionController
{
    public NetworkedInventoryManager inventory;

    [Networked] NetworkButtons prior_buttons { get; set; }
    [Networked] Quaternion lookDirection { get; set; }

    public Vector3 castPointOffset;

    private HybridCharacterController hcc;

    public override void Spawned()
    {
        base.Spawned();
        if (inventory == null) inventory = GetComponent<NetworkedInventoryManager>();
        if (hcc == null) hcc = GetComponent<HybridCharacterController>();
        //GlobalSpellBuffer.Instance.AssignSliceToPlayer(Runner.LocalPlayer);
    }

    public override void FixedUpdateNetwork()
    {
        // Notice we do NOT call base.FixedUpdateNetwork() anymore, because the Base is just a ledger!

        if (GetInput(out NetworkInputData data))
        {
            if (data.buttons.WasReleased(prior_buttons, EInputButton.LEFT_CLICK))
                OnInputEvent(ItemActionChannel.Primary, false, default);

            if (data.buttons.WasReleased(prior_buttons, EInputButton.RIGHT_CLICK))
                OnInputEvent(ItemActionChannel.Feed, false, default);

            if (data.buttons.WasPressed(prior_buttons, EInputButton.LEFT_CLICK))
                OnInputEvent(ItemActionChannel.Primary, true, data.interactionTarget);

            if (data.buttons.WasPressed(prior_buttons, EInputButton.RIGHT_CLICK))
                OnInputEvent(ItemActionChannel.Feed, true, data.interactionTarget);

            prior_buttons = data.buttons;
            lookDirection = data.lookRotation;
        }
    }

    private void OnInputEvent(ItemActionChannel channel, bool isPress, NetworkInteractionTarget target)
    {
        if (inventory == null || inventory.activeItem == null)
            return;

        if (!inventory.activeItem.TryGetComponent(out EquipableItem item))
            return;

        if (isPress)
            item.TryPressAction(channel, 0, target);
        else
            item.TryReleaseAction(channel);
    }

    public override void EndCast()
    {
        if (inventory != null && inventory.activeItem != null && inventory.activeItem.TryGetComponent(out EquipableItem item))
        {
            ItemActionChannel channel = item.ItemActionData.channel;

            if (channel != ItemActionChannel.None)
                item.TryReleaseAction(channel);
        }

        isCasting = false;
    }

    // ==========================================
    // THE SPATIAL CONTRACT (Fulfilling the Base)
    // ==========================================

    public override Vector3 GetAimTarget()
    {
        RaycastHit hit;
        HybridCharacterController hcc = GetComponent<HybridCharacterController>();

        Vector3 viewpoint = hcc.hipsRb.position + hcc.camController.localEyeOffset + hcc.camController.GetEyePosBasedOnPitch(lookDirection);
        Vector3 fallback = lookDirection * Vector3.forward * 100f + viewpoint;

        return Physics.Raycast(viewpoint, lookDirection * Vector3.forward, out hit, 100f, SpellSystemHelpers.GeneralCollisionLayerMask())? hit.point: fallback;
    }

    public override EyePosAndLookDir GetEyePosAndLookDir()
    {
        return hcc.GetEyePosAndLookDirSim();
    }

    public override Vector3 GetSpellCastPoint()
    {
        return hcc.hipsRb.transform.position + (hcc.lookRot * castPointOffset);
    }

    public override Vector3 GetSpellCastDir()
    {
        return lookDirection * Vector3.forward;
    }

    // ==========================================
    // THE HARDWARE CONTRACT
    // ==========================================

    public override void ActivateHitbox(int hitBoxID, SpellState state)
    {
        // Left blank intentionally! Player hitboxes are managed by EquipableItem natively.
    }

    public override void DeactivateHitbox(int hitBoxID)
    {
        // Left blank intentionally!
    }
}