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
        GlobalSpellBuffer.Instance.AssignSliceToPlayer(Runner.LocalPlayer);
    }

    public override void FixedUpdateNetwork()
    {
        // Notice we do NOT call base.FixedUpdateNetwork() anymore, because the Base is just a ledger!

        if (GetInput(out NetworkInputData data))
        {
            if (data.buttons.WasPressed(prior_buttons, EInputButton.LEFT_CLICK))
                OnInputEvent(true);

            if (data.buttons.WasReleased(prior_buttons, EInputButton.LEFT_CLICK))
                OnInputEvent(false);

            prior_buttons = data.buttons;
            lookDirection = data.lookRotation;
        }
    }

    private void OnInputEvent(bool isPress)
    {
        if (inventory == null || inventory.activeItem == null) return;
        if (!inventory.activeItem.TryGetComponent<EquipableItem>(out var item)) return;

        var actions = item.primaryActions;
        if (actions == null || actions.Count == 0 || actions[0] == null) return;

        // Hardcoded to index 0 for now. Combos will be managed by ItemActions in the future!
        ItemAction action = actions[0];

        if (isPress) action.OnPress(0, false);
        else action.OnRelease(0);
    }

    public override void EndCast()
    {
        OnInputEvent(false); // Force a release
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
        return GetComponent<HybridCharacterController>().GetEyePosAndLookDir();
    }

    public override Vector3 GetSpellCastPoint()
    {
        return hcc.hipsRb.transform.position + (hcc.lookRot * castPointOffset);
    }

    public override Vector3 GetSpellCastDir()
    {
        return GetComponent<HybridCharacterController>().GetEyePosAndLookDir().Forward;
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