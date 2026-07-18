using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Fusion;

public class TimerTriggerST : SpellTrigger ////////////////////////////////////////This needs re-writing /////////////////////////////////////////////////////////
{
    // we piggy-back on an associated spellcreatedphysicsobject's lifetime
    // to use a networked ticktimer. This
    // ensures the trigger occurs consistently across the network.

    float duration_in_seconds = 0;
    int max_times_triggered = 1;
    int n_times_triggered = 0;
    float initial_tick_timer_remaining_seconds = -1;
    public override void OnAttach(TriggerNode node, float _size)
    {
        base.OnAttach(node, _size);
        // turn the second duration into a tick duration.
       /* TimerTriggerNode ttn = node as TimerTriggerNode;
        duration_in_seconds = ttn.duration_in_seconds;

        if (ttn.repeated_trigger_count >= 0)
            max_times_triggered = ttn.repeated_trigger_count;
        else
            // if you set it to -1, it will do it indefinitely.
            max_times_triggered = int.MaxValue;

        n_times_triggered = 0;

        scpo = GetComponent<SpellCreatedPhysicsObject>();
        if (scpo == null)
            return;*/

        
    }

    

    public override void OnTick()
    {
        base.OnTick();

       /* if (initial_tick_timer_remaining_seconds < 0)
        {
            if (!CatchInitialTickTimerValue())
                return;
        }

        if (n_times_triggered >= max_times_triggered)
            return;

        if (duration_in_seconds <= 0 || scpo == null)
            return;

        // note that remaining lifetime ticks down.
        if(Mathf.Floor(ElapsedTime()/duration_in_seconds) > n_times_triggered)
        {
            TimerTrigger();
        }*/
    }


    private void TimerTrigger()
    {
        // timer trigger, so has no actual targets.
        // just creates cores.
        // therefore effects wouldn't do anything...
        // does it make more sense as a core?

        // what would filters do? nothing?

        n_times_triggered++;

        var triggerInfo = new SpellTriggerInfo(false, gameObject, this.state, this.transform.position, this.transform.rotation, this.transform.rotation);

        foreach (EffectNode effect in outcomeNodes.OfType<EffectNode>())
        {
            effect.Execute(triggerInfo);
        }
        foreach (CoreNode core in outcomeNodes.OfType<CoreNode>())
        {
            //core.CreateSpellCore(triggerInfo);
        }

    }
}
