using UnityEngine;
using Fusion;
public class PhysicsSubObject : NetworkBehaviour
{
    // A component of a physics object, allowing
    // multiple rigid bodies to trigger collisions
    // for a physicsobject (e.g. hands, arms, legs).
    [SerializeField] PhysicsObject parent_physics_object;
    void OnCollisionEnter(Collision collision)
    {
        if (collision.impulse.magnitude > 0.01)
            Debug.Log($"{name} was hit for {collision.impulse.magnitude} impulse");
        parent_physics_object.OnCollisionEnter(collision);
    }
}
