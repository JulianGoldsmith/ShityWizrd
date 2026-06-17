using UnityEngine;

[CreateAssetMenu(fileName = "GravityNode", menuName = "SpellNodes/Behaviour/GravityNode")]
public class GravityNode : BehaviourNode
{
    [Promotable("Gravity Added", DataTypeTag.Generic)]
    public sbyte gravityAdded = 25;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
       // float finalGravity = GetFinalValue(nameof(gravityAdded), gravityAdded);

        sbyte bakedGravity = (sbyte)Mathf.Clamp(gravityAdded, -125, 125);

        return new GravityBehaviour()
        {
            GravityAdded = bakedGravity
        };
    }

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        /*var gravity = spellCore.AddComponent<GravitySB>();
        gravity.Init(triggerInfo, gravityAdded);*/
    }
}

public class GravityBehaviour : IBehaviour
{
    public sbyte GravityAdded;
    public void InitTick(SpellCreatedCore core)
    {
        if (core.TryGetComponent<PhysicsObject>(out var po))
        {
            // Convert your old -125 to 125 system into a simple float multiplier
            float gravityModifier = GravityAdded / 100f;

            // Modify the base property on the new Ledger directly
            po.physicsObjectProperties.Base_gravity_multiplier += gravityModifier;
        }
    }

    public void Tick(SpellCreatedCore core, float deltaTime)
    {

    }

    public void CleanupVFX(SpellCreatedCore core)
    {
    }
    public void TickVFX(SpellCreatedCore core)
    {
    }
}

public class GravitySB : SpellBehaviour
{
    public sbyte gravityAdded;
    public float terminalSpeed = 80f;
    private PhysicsObject po;

    public void Init(SpellTriggerInfo _triggerInfo, sbyte _gravityAdded)
    {
        triggerInfo = _triggerInfo;
        gravityAdded = _gravityAdded;

        po = GetComponent<PhysicsObject>();

        if (po != null)
        {
            float gravityModifier = gravityAdded / 100f;
            po.physicsObjectProperties.Base_gravity_multiplier += gravityModifier;
        }
    }
}

