using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Fusion;

public class OnDestroyTriggerST : SpellTrigger
{
    // we piggy-back on an associated spellcreatedphysicsobject
    // methods to know when it either expired or was destroyed.

    SpellCreatedPhysicsObject scpo;

    bool on_expire;
    bool on_break;
    public override void OnAttach(TriggerNode node, float _size)
    {
        base.OnAttach(node, _size);
        // turn the second duration into a tick duration.
        OnDestroyTriggerNode odtn = node as OnDestroyTriggerNode;

        on_expire = odtn.on_expire;
        on_break = odtn.on_break;

        scpo = GetComponent<SpellCreatedPhysicsObject>();
        if (scpo == null)
            return;

        // subscribe to events on the object

        if (on_expire)
            scpo.OnLifetimeExpired_event.AddListener(Trigger);

        if (on_break)
            scpo.OnZeroBonk_event.AddListener(Trigger);
    }

    private void Trigger()
    {
        var triggerInfo = new SpellTriggerInfo(false, gameObject, this.state, this.transform.position, this.transform.rotation, this.transform.rotation);

        foreach (EffectNode effect in outcomeNodes.OfType<EffectNode>())
        {
            effect.Execute(triggerInfo);
        }
        foreach (CoreNode core in outcomeNodes.OfType<CoreNode>())
        {
            Debug.Log($"spawning a core because of destroy trigger {core.InstanceGuid}");
            //core.CreateSpellCore(triggerInfo);
        }

    }
}
