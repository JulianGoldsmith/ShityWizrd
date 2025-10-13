using System.Collections.Generic;
using UnityEngine;
public abstract class SpellTrigger : MonoBehaviour
{
    public SpellState state;

    public List<FilterNode> filterNodes;

    public List<SpellNode> outcomeNodes;
    public virtual void OnAttach(TriggerNode node, float _size) 
    {
        // override the vfx modifier type based on spell, at some point.
        GameObject vfx = GameController.Instance?.vfxDatabase?.GetVFX(node.vfx_context, node.default_vfx_modifier_type);
        if (vfx != null)
        {
            GameObject spawned_vfx = Instantiate(vfx, transform);
            // rescaling, since it's a child object.
            Vector3 new_scale = vfx.transform.localScale;
            new_scale.x *= _size / spawned_vfx.transform.parent.localScale.x;
            new_scale.y *= _size / spawned_vfx.transform.parent.localScale.y;
            new_scale.z *= _size / spawned_vfx.transform.parent.localScale.z;
            spawned_vfx.transform.localScale = new_scale;

            // then here pass on values as necessary.
        }
    }
    public virtual void OnTick() { }
}
