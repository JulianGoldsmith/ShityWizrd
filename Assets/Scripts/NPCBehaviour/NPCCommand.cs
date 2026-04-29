using UnityEngine;

public abstract class NPCCommand : ScriptableObject
{
    public CommandType Type;

    public virtual void PreTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {

    }

    public abstract void ActiveTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle);
}

