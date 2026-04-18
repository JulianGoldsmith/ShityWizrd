using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "TimerTriggerNode", menuName = "SpellNodes/TriggerNodes/Timer Trigger Node")]
public class TimerTriggerNode : TriggerNode
{
    [Promotable("Duration", DataTypeTag.Duration)]
    public float duration_in_seconds = 1;
    // number times it can trigger. if >1, then it will wait for the duration between triggers.
    public int repeated_trigger_count = 1;

    public override ITrigger CompileTriggerCondition(SpellCompilationContext context)
    {
        float bakedDuration = GetFinalValue(nameof(duration_in_seconds), duration_in_seconds);

        int assignedBoolBit = context.ClaimBoolBit();

        return new TimerTrigger()
        {
            DurationInSeconds = bakedDuration,
            MaxTriggerCount = repeated_trigger_count,
            HasFiredBitIndex = assignedBoolBit
        };
    }


    public override void SetUp(GameObject spellCore, SpellState state)
    {
       /* TimerTriggerST timerst = spellCore.AddComponent<TimerTriggerST>();
        timerst.state = state;
        timerst.filterNodes = this.filterNodes;
        timerst.outcomeNodes = this.outcomeNodes;
        
        float size = 1;

        OnAttach(timerst, size);*/
    }
}

public class TimerTrigger : ITrigger
{

    public float DurationInSeconds;
    public int MaxTriggerCount;
    public int HasFiredBitIndex;

    public TriggerExecutioPlan Plan { get; set; }

    
    public void InitTick(SpellCreatedCore core)
    {

    }

    public bool Tick(SpellCreatedCore core, float deltaTime, out List<SpellTriggerInfo> triggerInfo)
    {
        triggerInfo =  new List<SpellTriggerInfo>();
        SpellTriggerInfo hitInfo = null;
        
        if (core.GetBool(HasFiredBitIndex) == false && core.Context.AliveTime >= DurationInSeconds)
        {
      
            core.SetBool(HasFiredBitIndex, true);

            hitInfo = new SpellTriggerInfo(isCast: false,
                    source: core.gameObject,
                    state: SpellStateManager.instance.GetActiveSpell(core.ActiveCastID).State,
                    position: core.transform.position,
                    rotation: core.transform.rotation,
                    tiggerVector: core.NetworkVelocity,
                    hitObject: core.gameObject);

            triggerInfo.Add(hitInfo);

            return true;
        }

        return false;
    }

    public void TickVFX(SpellCreatedCore core)
    {
    }
    public void CleanupVFX(SpellCreatedCore core)
    {
    }

}