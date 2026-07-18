using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Fusion;

public class OnDestroyTriggerST : SpellTrigger /////////////////////////////////////This needs rewriting /////////////////////////////////////////
{
    // we piggy-back on an associated spellcreatedphysicsobject
    // methods to know when it either expired or was destroyed.
    

    bool on_expire;
    bool on_break;
    public override void OnAttach(TriggerNode node, float _size)
    {
        base.OnAttach(node, _size);
       
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
