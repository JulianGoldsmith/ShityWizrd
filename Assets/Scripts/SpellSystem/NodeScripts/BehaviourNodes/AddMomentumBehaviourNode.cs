using UnityEngine;

[CreateAssetMenu(fileName = "AddMomentumBehaviourNode", menuName = "SpellNodes/Behaviour/AddMomentumBehaviourNode")]
public class AddMomentumBehaviourNode : BehaviourNode
{
    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        ApplyPromotableValues();
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
        triggerInfo = _triggerInfo;
        velocity = _triggerInfo.TriggerVector;

        if (rb == null) rb = GetComponent<Rigidbody>();
        mass = rb != null ? rb.mass : 1f;

        if (rb != null)
        {
            rb.AddForce(velocity, ForceMode.VelocityChange); //ignores mass (ie adds momentum)
        }
    }
}