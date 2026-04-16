using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "CollisionEnterNode", menuName = "SpellNodes/TriggerNodes/Collision Enter Node")]
public class CollisionEnterNode : TriggerNode
{
    public override ITrigger CompileTriggerCondition(SpellCompilationContext context)
    {
        throw new System.NotImplementedException();
    }

    public override void SetUp(GameObject spellCore, SpellState state)
    {
        EnterCollisionST collisionChecker = spellCore.AddComponent<EnterCollisionST>();
        collisionChecker.state = state;
        collisionChecker.filterNodes = this.filterNodes;
        collisionChecker.outcomeNodes = this.outcomeNodes;
    }
}
