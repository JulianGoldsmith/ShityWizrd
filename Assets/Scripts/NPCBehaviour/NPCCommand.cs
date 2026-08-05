using UnityEngine;

public abstract class NPCCommand : ScriptableObject
{
    public abstract CommandType Type { get; }
    public abstract NPCCommandChannel Channel { get; }

    public virtual void PreTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {

    }

    public abstract void ActiveTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle);
}

