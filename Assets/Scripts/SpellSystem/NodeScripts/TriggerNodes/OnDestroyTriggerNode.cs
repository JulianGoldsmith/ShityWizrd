using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "OnDestroyTriggerNode", menuName = "SpellNodes/TriggerNodes/OnDestroyTriggerNode")]
public class OnDestroyTriggerNode : TriggerNode
{
    public bool on_expire = false; // at end of lifetime
    public bool on_break = true; // specifically when broken (destroyed)

    public override ITrigger CompileTriggerCondition(SpellCompilationContext context)
    {
        throw new System.NotImplementedException();
    }

    public override void SetUp(GameObject spellCore, SpellState state)
    {
        OnDestroyTriggerST odtst = spellCore.AddComponent<OnDestroyTriggerST>();
        odtst.state = state;
        odtst.filterNodes = this.filterNodes;
        odtst.outcomeNodes = this.outcomeNodes;
        
        float size = 1;

        OnAttach(odtst, size);
    }
}
