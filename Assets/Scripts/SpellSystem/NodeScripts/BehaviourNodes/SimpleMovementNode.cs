using UnityEngine;
using System;

[CreateAssetMenu(fileName = "SimpleMovement", menuName = "SpellNodes/Behaviour/Simple Movement Node")]
public class SimpleMovementNode : BehaviourNode
{
    [Promotable("Motion Speed", DataTypeTag.Speed)]
    public float speed;

    public override IBehaviour CompileBehaviour(SpellCompilationContext context)
    {
        throw new NotImplementedException();
    }

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        var simpleMovement = spellCore.AddComponent<SimpleMovementSB>();
        simpleMovement.triggerInfo = triggerInfo;
        simpleMovement.speed = speed;
    }
}
