using System.Collections.Generic;
using UnityEngine;
public abstract class SpellTrigger : MonoBehaviour
{
    public SpellState state;

    public List<FilterNode> filterNodes;

    public List<SpellNode> outcomeNodes;
    public virtual void OnAttach(TriggerNode node, float _size = 1) 
    {
        // override the vfx modifier type based on spell, at some point.
        GameObject vfx = SpellSystemHelpers.CreateVFX(
            node.vfx_context, 
            node.default_vfx_modifier_type, 
            transform, 
            _size, 
            true
        );
    }
    public virtual void OnTick() { }
}
