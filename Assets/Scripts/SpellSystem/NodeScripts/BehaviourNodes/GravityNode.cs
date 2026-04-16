using UnityEngine;

[CreateAssetMenu(fileName = "GravityNode", menuName = "SpellNodes/Behaviour/GravityNode")]
public class GravityNode : BehaviourNode
{
    [Promotable("Gravity Added", DataTypeTag.Generic)]
    public sbyte gravityAdded = 25;

    public override IBehaviour CompileBehaviour(SpellCompilationContext context)
    {
        throw new System.NotImplementedException();
    }

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        var gravity = spellCore.AddComponent<GravitySB>();
        gravity.Init(triggerInfo, gravityAdded);
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
            int newGravity = Mathf.Clamp(po.SpellEffectState.Gravity + gravityAdded, -125, 125);

            var state = po.SpellEffectState;
            state.Gravity = (sbyte)newGravity;
            po.SpellEffectState = state;

            po.UpdateDerivedPhysics();
        }

    }
}

