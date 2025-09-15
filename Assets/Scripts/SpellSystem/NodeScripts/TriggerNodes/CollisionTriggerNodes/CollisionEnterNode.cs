using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "CollisionEnterNode", menuName = "SpellNodes/TriggerNodes/Collision Enter Node")]
public class CollisionEnterNode : TriggerNode
{
     
    public override void SetUp(GameObject spellCore, SpellState state)
    {
        EnterCollisionST collisionChecker = spellCore.AddComponent<EnterCollisionST>();
        collisionChecker.state = state;
        collisionChecker.filterNodes = this.filterNodes;
        collisionChecker.outcomeNodes = this.outcomeNodes;
    }
}
