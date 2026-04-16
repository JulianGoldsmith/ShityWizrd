using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "OverlapSphereNode", menuName = "SpellNodes/TriggerNodes/OverlapSphereNode")]
public class OverlapSphereNode : TriggerNode
{
    [Promotable("Size", DataTypeTag.Radius)]
    public float size;
    [Tooltip("If this should only trigger once per overlaped object, rather than every tick")]
    public bool singleTrigger = false;

    public override ITrigger CompileTriggerCondition(SpellCompilationContext context)
    {
        throw new System.NotImplementedException();
    }

    public override void SetUp(GameObject spellCore, SpellState state)
    {
        OverlapSphereST sphereChecker = spellCore.AddComponent<OverlapSphereST>();
        sphereChecker.state = state;
        sphereChecker.filterNodes = this.filterNodes;
        sphereChecker.outcomeNodes = this.outcomeNodes;
        sphereChecker.size = size;
        sphereChecker.singleTrigger = singleTrigger;    

        OnAttach(sphereChecker, size);
    }
}
