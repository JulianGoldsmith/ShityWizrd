using UnityEngine;

public abstract class SpellBehaviour : MonoBehaviour
{
    public SpellTriggerInfo triggerInfo;
    protected SpellCreatedPhysicsObject scpo;
    public virtual void OnAttach(BehaviourNode node, float _size = 1)
    {
        scpo = GetComponent<SpellCreatedPhysicsObject>();

        // override the vfx modifier type based on spell, at some point.
        
    }
    public virtual void OnTick() { }
}
