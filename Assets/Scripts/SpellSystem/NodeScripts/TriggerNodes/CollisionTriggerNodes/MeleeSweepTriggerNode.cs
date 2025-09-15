using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "MeleeSweepTriggerNode", menuName = "SpellNodes/TriggerNodes/MeleeSweepTriggerNode")]
public class MeleeSweepTriggerNode : TriggerNode
{
    public override void SetUp(GameObject spellCore, SpellState state)
    {
        MeleeSweepST collisionChecker = spellCore.AddComponent<MeleeSweepST>();
        collisionChecker.state = state;
        collisionChecker.filterNodes = this.filterNodes;
        collisionChecker.outcomeNodes = this.outcomeNodes;
        collisionChecker.Init(state);
    }
}