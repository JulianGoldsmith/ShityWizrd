using Fusion;
using UnityEngine;

public class NPCPhysicsObject : PhysicsObject
{
    [SerializeField] CharacterBonkController bonkController;
    const float character_bonkedness_recovery_rate_per_tick = 0.05f;

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

       
    }
    
}