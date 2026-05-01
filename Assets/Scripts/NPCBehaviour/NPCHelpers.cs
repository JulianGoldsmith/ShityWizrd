using UnityEngine;
using Fusion;

public static class BehaviourHelpers
{
    public static NetworkObject GetCoreNetworkObject(NetworkObject characterObj)
    {
        if (characterObj == null) return null;

        if (characterObj.TryGetComponent<IHasPhysicalCore>(out var coreProvider))
        {
            return coreProvider.GetCoreNetworkObject();
        }

        // Fallback
        return characterObj;
    }

    public static Transform GetCoreTransform(NetworkObject characterObj)
    {
        if (characterObj == null) return null;

        if (characterObj.TryGetComponent<IHasPhysicalCore>(out var coreProvider))
        {
            return coreProvider.GetCoreTransform();
        }

        return characterObj.transform;
    }
}