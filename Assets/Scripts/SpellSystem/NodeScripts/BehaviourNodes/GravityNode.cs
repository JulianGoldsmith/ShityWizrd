using UnityEngine;

[CreateAssetMenu(fileName = "GravityNode", menuName = "SpellNodes/Behaviour/GravityNode")]
public class GravityNode : BehaviourNode
{
    [Promotable("Gravity", DataTypeTag.Force)]
    public float gravityAcceleration = 98.1f;
    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        ApplyPromotableValues();
        var gravity = spellCore.AddComponent<GravitySB>();
        gravity.Init(triggerInfo, gravityAcceleration);
    }
}

public class GravitySB : SpellBehaviour
{
    public float gravityAcceleration;
    public float terminalSpeed = 80f;
    private Rigidbody rb;


    public void Init(SpellTriggerInfo _triggerInfo, float _gravityAcceleration)
    {
        triggerInfo = _triggerInfo;
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = SpellSystemHelpers.AddDefaultSpellRigidBodyToGameObject(this.gameObject);
        }
        gravityAcceleration = _gravityAcceleration;
    }

    void FixedUpdate()
    {
        rb.AddForce(Vector3.down * gravityAcceleration, ForceMode.Acceleration);

        if (terminalSpeed > 0f)
        {
            var v = rb.linearVelocity;
            if (v.y < -terminalSpeed)
            {
                v.y = -terminalSpeed;
                rb.linearVelocity = v;
            }
        }
    }
}

