using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public abstract class InteractableItem : NetworkBehaviour
{
    public NetworkRigidbody3D networkedRB;

    public virtual void PickUpItem(NetworkObject playerObject) { }

    public virtual void DropItem(NetworkObject playerObject, bool hasInputAuthority, bool hasStateAuthority) { }

    public virtual void ForceReleaseForDisconnect(NetworkObject playerObject)
    {
        if (Object == null || !Object.IsValid || !Object.HasStateAuthority)
            return;

        DropItem(playerObject, false, true);
        Object.RemoveInputAuthority();
    }
}
