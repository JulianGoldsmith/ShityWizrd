using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "TimerTriggerNode", menuName = "SpellNodes/TriggerNodes/Timer Trigger Node")]
public class TimerTriggerNode : TriggerNode
{
    [Promotable("Duration", DataTypeTag.Duration)]
    public float duration_in_seconds = 1;
    // number times it can trigger. if >1, then it will wait for the duration between triggers.
    public int repeated_trigger_count = 1; 
    public override void SetUp(GameObject spellCore, SpellState state)
    {
        TimerTriggerST timerst = spellCore.AddComponent<TimerTriggerST>();
        timerst.state = state;
        timerst.filterNodes = this.filterNodes;
        timerst.outcomeNodes = this.outcomeNodes;
        
        float size = 1;

        OnAttach(timerst, size);
    }
}
