using UnityEngine;
using UnityEngine.Events;

public class CollisionRelay : MonoBehaviour
{
    // Generic relay collider, used to pass collisions triggers
    // up to a parent gameobject.
    // Primarily used by halo colliders around a PhysicsObject.

    public UnityEvent<Collider> OnTriggerEnter_Action;
    public UnityEvent<Collider> OnTriggerExit_Action;
    public UnityEvent<Collider> OnTriggerStay_Action;


    private void OnTriggerEnter(Collider other)
    {
        OnTriggerEnter_Action?.Invoke(other);
    }
    private void OnTriggerExit(Collider other)
    {
        OnTriggerExit_Action?.Invoke(other);
    }
    private void OnTriggerStay(Collider other)
    {
        OnTriggerStay_Action?.Invoke(other);
    }
}