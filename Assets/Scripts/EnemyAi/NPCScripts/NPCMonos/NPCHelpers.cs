using UnityEngine;

public static class NPCHelpers
{
    public static Transform GetCoreTransformFromRoot(GameObject target)
    {
        if(target.TryGetComponent<HybridCharacterController>(out HybridCharacterController cc))
        {
            return cc.hipsRb.transform;
        }
        else if (target.TryGetComponent<NPCActiveRagdollController>(out var npc))
        {
             return npc.coreRB.transform;
        }

        return target.transform;
    }
}