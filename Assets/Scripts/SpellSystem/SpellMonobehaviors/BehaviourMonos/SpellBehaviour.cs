using UnityEngine;

public abstract class SpellBehaviour : MonoBehaviour ////////////////////////// This is depreciated, no longer using mono's attatched
{
    public SpellTriggerInfo triggerInfo;
    public virtual void OnAttach(BehaviourNode node, float _size = 1)
    {
        
    }
    public virtual void OnTick() { }
}
