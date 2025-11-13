using Fusion;
using UnityEngine;

public class NPCPhysicsObject : PhysicsObject
{
    [SerializeField] NPCCoreController controller;
    const float character_bonkedness_recovery_rate_per_tick = 0.05f;

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (HasStateAuthority)
            current_bonkedness = Mathf.Min(current_bonkedness + character_bonkedness_recovery_rate_per_tick, starting_bonkedness);
    }
    protected override void OnZeroBonk()
    {
        base.OnZeroBonk();

        // can also go to a death state at a high bonk threshold.
        controller.GetBonked();
    }

    protected override void OnRecoverFromBonk()
    {
        base.OnRecoverFromBonk();

        // Recovered from bonk.
        controller.GetUnBonked();
    }

    protected override void OnBonk(float bonk_ammount, NetworkObject bonk_instigator = null, Vector3? pos = null)
    {
        base.OnBonk(bonk_ammount, bonk_instigator);
        if(this.TryGetComponent<NPCAggroController>(out NPCAggroController npcac))
        {
            Debug.Log($"Reporting Bonk to aggro controller bonker - {bonk_instigator} ammount - {bonk_ammount}");
            npcac.ReportBonk(bonk_instigator, bonk_ammount, pos);

            
        }
    }
}