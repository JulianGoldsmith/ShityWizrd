using UnityEngine;

public class AccelerateSB : SpellBehaviour
{
    public float acceleration = 0.1f;

    public override void OnTick()
    {
        if (scpo == null || scpo.rb.linearVelocity.magnitude <= 0)
            return;

        // simply acceleration along velocity.
        scpo.ApplyForce(scpo.rb.linearVelocity.normalized * acceleration, ForceMode.Acceleration);
    }
}
