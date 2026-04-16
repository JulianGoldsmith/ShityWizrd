using UnityEngine;
using System;

[CreateAssetMenu(fileName = "AccelerateNode", menuName = "SpellNodes/Behaviour/AccelerateNode")]
public class AccelerateNode : BehaviourNode
{
    [Promotable("Acceleration", DataTypeTag.Speed)]
    public float acceleration;

    public override IBehaviour CompileBehaviour(SpellCompilationContext sCC)
    {
        throw new NotImplementedException();
    }

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        var accel = spellCore.AddComponent<AccelerateSB>();
        accel.triggerInfo = triggerInfo;
        accel.acceleration = acceleration;

        accel.OnAttach(this);
    }
}
