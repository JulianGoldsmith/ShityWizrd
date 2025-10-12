using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "OverlapSphereNode", menuName = "SpellNodes/TriggerNodes/OverlapSphereNode")]
public class OverlapSphereNode : TriggerNode
{
    [Promotable("Size", DataTypeTag.Radius)]
    public float size;
    public override void SetUp(GameObject spellCore, SpellState state)
    {
        OverlapSphereST sphereChecker = spellCore.AddComponent<OverlapSphereST>();
        sphereChecker.state = state;
        sphereChecker.filterNodes = this.filterNodes;
        sphereChecker.outcomeNodes = this.outcomeNodes;
        sphereChecker.size = size;
    }
}
