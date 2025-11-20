using Fusion;
using UnityEngine;

public class PlayerPhysicsObject : PhysicsObject
{
    [SerializeField] HybridCharacterController hybridCharacterController;
    const float player_bonkedness_recovery_rate_per_tick = 0.05f;

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if(HasStateAuthority)
            current_bonkedness = Mathf.Min(current_bonkedness + player_bonkedness_recovery_rate_per_tick, starting_bonkedness);
    }
    protected override void OnZeroBonk()
    {
        base.OnZeroBonk();

        // can also go to a death state at a high bonk threshold.
        if(HasStateAuthority)
            hybridCharacterController.GetBonked();
    }

    protected override void OnRecoverFromBonk()
    {
        base.OnRecoverFromBonk();
        if (HasStateAuthority)
            // Recovered from bonk.
            hybridCharacterController.GetUnBonked();
    }

    public override void OnBonkednessChanged(NetworkBehaviourBuffer previous)
    {
        base.OnBonkednessChanged(previous); 
        UpdateHUD(); 
    }

    private void UpdateHUD()
    {
        if (HasInputAuthority)
        {
            HUDController.Instance.UpdateBonkBar(current_bonkedness, starting_bonkedness);
        }
    }
}
