using UnityEngine;

[CreateAssetMenu(fileName = "AddMomentumBehaviourNode", menuName = "SpellNodes/Behaviour/AddMomentumBehaviourNode")]
public class AddMomentumBehaviourNode : BehaviourNode
{
    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        var momentumMono = spellCore.AddComponent<AddMomentumSBMono>();
        momentumMono.Init(triggerInfo);
    }
}

public class AddMomentumSBMono : SpellBehaviour
{
    public Vector3 velocity;
    public float mass;
    Rigidbody rb;

    public void Init(SpellTriggerInfo _triggerInfo)
    {
        if (_triggerInfo == null)
            return;

        triggerInfo = _triggerInfo;
        velocity = _triggerInfo.TriggerVector * _triggerInfo.State.CastChargeLevel;

        if (TryGetComponent<PhysicsObject>(out var po))
        {
            float calcMass = Mathf.Max(0.01f, po.currentProperties.mass);

            // Apply universal force!
            po.ApplyForce(velocity / Mathf.Sqrt(calcMass), ForceMode.VelocityChange);
        }

        /*if (rb == null) rb = GetComponent<Rigidbody>();
        mass = rb != null ? rb.mass : 1f;

        if (rb != null)
        {


            rb.AddForce(velocity/Mathf.Sqrt(mass), ForceMode.VelocityChange); //ignores mass (ie adds momentum)
            // just applies velocity, not momentum since mass is ignored.
            // This means that you can fling any-weight object at the same initial velocity.
            // However, including mass in the denominator makes flings extremely weak
            // if you include a size rune, since the object gets super heavy.
            // It feels like a heavier object should be more difficult to fling with
            // this rune, but currently not the case.
            // Could try dividing by mass to get back to a momentum;
            // or dividing by sqrt(mass) or similar to make mass not as impactful.
        }*/
    }
}