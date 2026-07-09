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

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {

        int assignedBoolBit = context.ClaimBoolBit();

        return new TimerTrigger()
        {
            DurationInSeconds = new RuntimeFloatProperty(this.duration_in_seconds),
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

public class TimerTrigger : RuntimeTriggerBase
{
    public RuntimeFloatProperty DurationInSeconds;
    public int MaxTriggerCount;
    public int HasFiredBitIndex;

    // 1. SIGNATURE UPDATE
    public override void InitTick(ISpellExecutionCore core) { }

    // 1. SIGNATURE UPDATE
    public override bool Tick(ISpellExecutionCore core, float deltaTime, out List<SpellTriggerInfo> triggerInfo)
    {
        triggerInfo = new List<SpellTriggerInfo>();

        if (core.GetBool(HasFiredBitIndex) == false && core.Context.AliveTime >= DurationInSeconds.GetValue(default))
        {
            core.SetBool(HasFiredBitIndex, true);

            // 2. CAPABILITIES BRIDGE (Velocity Fallback)
            // If it's a fireball, use its actual flying speed. 
            // If it's a virtual beam, use the aim vector stored in its Context!
            Vector3 triggerVel = Vector3.zero;
            if (core.TryGetCoreComponent<SpellCreatedCore>(out var physicalCore))
            {
                triggerVel = physicalCore.NetworkVelocity;
            }
            else
            {
                triggerVel = core.Context.TriggerVector;
            }

            SpellTriggerInfo hitInfo = new SpellTriggerInfo(
                isCast: false,
                source: core.SourceObject, // ANCHOR BRIDGE
                state: SpellStateManager.instance.GetActiveSpell(core.ActiveCastID).State,
                position: core.Position,   // SPATIAL UPDATE
                rotation: core.Rotation,   // SPATIAL UPDATE
                triggerVector: triggerVel, // BRIDGED VELOCITY
                hitObject: core.SourceObject // ANCHOR BRIDGE
            );

            triggerInfo.Add(hitInfo);

            return true;
        }

        return false;
    }

    // 1. SIGNATURE UPDATE
    public override void TickVFX(ISpellExecutionCore core) { }

    // 1. SIGNATURE UPDATE
    public override void CleanupVFX(ISpellExecutionCore core) { }
}