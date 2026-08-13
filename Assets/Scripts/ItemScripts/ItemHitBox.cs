using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ItemHitBox : MonoBehaviour
{
    [SerializeField] private Collider hitBoxCollider;
    [SerializeField] private Rigidbody drivingRigidbody;

    private MeleeExecutionCore _executionCore;

    private void Awake()
    {
        if (hitBoxCollider == null) hitBoxCollider = GetComponent<Collider>();
        if (drivingRigidbody == null) drivingRigidbody = GetComponentInParent<Rigidbody>();
        hitBoxCollider.enabled = false;
    }

    private void OnDisable()
    {
        _executionCore = null;
        if (hitBoxCollider != null) hitBoxCollider.enabled = false;
    }

    public void SetActive(MeleeExecutionCore executionCore, bool active)
    {
        _executionCore = active ? executionCore : null;
        if (hitBoxCollider != null) hitBoxCollider.enabled = active;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0) return;
        ContactPoint contact = collision.GetContact(0);
        AccumulateHit(collision.collider, contact.point, contact.normal);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.contactCount == 0) return;
        ContactPoint contact = collision.GetContact(0);
        AccumulateHit(collision.collider, contact.point, contact.normal);
    }

    private void OnTriggerEnter(Collider other)
    {
        AccumulateTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        AccumulateTrigger(other);
    }

    private void AccumulateTrigger(Collider other)
    {
        Vector3 point = other.ClosestPoint(transform.position);
        Vector3 normal = (transform.position - point).normalized;
        if (normal.sqrMagnitude < 0.0001f) normal = transform.forward;
        AccumulateHit(other, point, normal);
    }

    private void AccumulateHit(Collider other, Vector3 point, Vector3 normal)
    {
        if (_executionCore == null || other == null) return;

        GameObject hitObject = SpellSystemHelpers.GetHitGameObject(other);
        if (hitObject == null) return;

        NetworkObject targetObject = null;
        PhysicsSubObject subObject = hitObject.GetComponent<PhysicsSubObject>();

        if (subObject != null && subObject.parent_physics_object != null) targetObject = subObject.parent_physics_object.Object;
        if (targetObject == null) targetObject = hitObject.GetComponent<NetworkObject>();
        if (targetObject == null) targetObject = hitObject.GetComponentInParent<NetworkObject>();
        if (targetObject == null || !targetObject.Id.IsValid) return;

        Vector3 velocity = drivingRigidbody != null ? drivingRigidbody.GetPointVelocity(point) : transform.forward;
        _executionCore.AccumulateHit(targetObject.Id, hitObject, point, normal, velocity);
    }
}
