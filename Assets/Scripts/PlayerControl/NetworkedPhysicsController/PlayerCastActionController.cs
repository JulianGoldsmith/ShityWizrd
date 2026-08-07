using UnityEngine;
using Fusion;

[RequireComponent(typeof(NetworkedInventoryManager))]
public class PlayerCastActionController : CastActionController
{
    public NetworkedInventoryManager inventory;
    [SerializeField] private PlayerActionManager playerActionManager;
    [Networked] Quaternion lookDirection { get; set; }

    public Vector3 castPointOffset;

    private HybridCharacterController hcc;

    public override void Spawned()
    {
        base.Spawned();
        if (inventory == null) inventory = GetComponent<NetworkedInventoryManager>();
        if (playerActionManager == null) playerActionManager = GetComponent<PlayerActionManager>();
        if (hcc == null) hcc = GetComponent<HybridCharacterController>();
        //GlobalSpellBuffer.Instance.AssignSliceToPlayer(Runner.LocalPlayer);
    }

    public void SetSimulationLookDirection(Quaternion value)
    {
        lookDirection = value;
    }

    public override void EndCast()
    {
        if (playerActionManager != null)
            playerActionManager.ReleaseCurrentAction();

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
