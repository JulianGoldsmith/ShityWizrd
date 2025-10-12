using System.Collections.Generic;
using UnityEngine;
public abstract class SpellTrigger : MonoBehaviour
{
    public SpellState state;

    public List<FilterNode> filterNodes;

    public List<SpellNode> outcomeNodes;
    public virtual void OnAttach(TriggerNode node) 
    {
        // override the vfx modifier type based on spell, at some point.
        GameObject vfx = GameController.Instance?.vfxDatabase?.GetVFX(node.vfx_context, node.default_vfx_modifier_type);
        if (vfx != null)
        {
            Instantiate(vfx, transform);
            // then here pass on values as necessary.
        }
    }
    public virtual void OnTick() { }
}
