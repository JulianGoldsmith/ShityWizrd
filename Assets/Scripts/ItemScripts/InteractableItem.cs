using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

public abstract class InteractableItem : NetworkBehaviour
{
    public NetworkRigidbody3D networkedRB;

    public abstract void PickUpItem(NetworkObject playerObject);

    public abstract void DropItem(NetworkObject playerObject);
}
