using UnityEngine;

[CreateAssetMenu(fileName = "AddMomentumBehaviourNode", menuName = "SpellNodes/Behaviour/AddMomentumBehaviourNode")]
public class AddMomentumBehaviourNode : BehaviourNode
{
    [Promotable("Force Multiplier", DataTypeTag.Force)]
    public float forceMultiplier = 1f;
    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        return new AddMomentumBehaviour()
        {
            ForceMultiplier = new RuntimeFloatProperty(this.forceMultiplier),
        };
    }

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        //var momentumMono = spellCore.AddComponent<AddMomentumSBMono>();
        //momentumMono.Init(triggerInfo);
    }
}

public class AddMomentumBehaviour : IBehaviour
{
    public RuntimeFloatProperty ForceMultiplier;

    // 1. SIGNATURE UPDATE
    public void InitTick(ISpellExecutionCore core)
    {
        float charge = core.Context.CastChargeLevel;
        Vector3 direction = core.Context.TriggerVector.normalized;

        // 2. CAPABILITIES BRIDGE
        if (core.TryGetCoreComponent<PhysicsObject>(out var po))
        {
            float calcMass = Mathf.Max(0.01f, po.physicsObjectProperties.mass);

            po.ApplyForce((charge * direction * ForceMultiplier.GetValue(default)) / Mathf.Sqrt(calcMass), ForceMode.VelocityChange);
        }
    }

    // 1. SIGNATURE UPDATE
    public void Tick(ISpellExecutionCore core, float deltaTime) { }

    // 1. SIGNATURE UPDATE
    public void CleanupVFX(ISpellExecutionCore core) { }

    // 1. SIGNATURE UPDATE
    public void TickVFX(ISpellExecutionCore core) { }
}


public class AddMomentumSBMono : SpellBehaviour
{
    public Vector3 velocity;
    public float mass;
    Rigidbody rb;

    public void Init(SpellTriggerInfo _triggerInfo)
    {
        if (!triggerInfo.IsValid)
            return;

        triggerInfo = _triggerInfo;
        velocity = _triggerInfo.TriggerVector * _triggerInfo.State.CastChargeLevel;

        if (TryGetComponent<PhysicsObject>(out var po))
        {
            float calcMass = Mathf.Max(0.01f, po.physicsObjectProperties.mass);

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