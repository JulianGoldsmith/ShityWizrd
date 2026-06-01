using Fusion;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class EnterCollisionST : SpellTrigger
{
    PhysicsObject po;
    Rigidbody rb;
    Collider col;
    private void Start()
    {
        if (this.GetComponent<Collider>() == null)
        {
            var col = this.AddComponent<SphereCollider>();
            col.isTrigger = false;
        }
        this.TryGetComponent<PhysicsObject>(out PhysicsObject p);
        po = p;
        //rb = SpellSystemHelpers.AddDefaultSpellRigidBodyToGameObject(this.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {

        HandleCollision(
            collision.gameObject,
            collision.contacts[0].point,
            collision.contacts[0].normal
        );
    }

    private void OnTriggerEnter(Collider other)
    {
        Vector3 hitPoint = other.ClosestPoint(transform.position);

        Vector3 hitNormal = (transform.position - other.transform.position).normalized;

        if (hitNormal == Vector3.zero) hitNormal = Vector3.up;

        if (other.TryGetComponent<PhysicsObject>(out var targetPO))
        {
            float impactSpeed = 10f; // Default
            if (TryGetComponent<IMovementHandler>(out var mover))
            {
                impactSpeed = mover.CurrentVelocity.magnitude;
            }
            else if (TryGetComponent<Rigidbody>(out var rb))
                impactSpeed = rb.linearVelocity.magnitude;

            targetPO.OnBonk(impactSpeed * po.physicsObjectProperties.mass, GetComponent<NetworkObject>(), hitPoint);
        }

        // 4. Continue with your existing Graph logic
        HandleCollision(
            other.gameObject,
            hitPoint,
            hitNormal
        );
    }

    private void HandleCollision(GameObject target, Vector3 hitPosition, Vector3 hitNormal)
    {
        bool isValidTarget = false;
        if (this.filterNodes.Count == 0)
        {
            isValidTarget = true;
        }
        else
        {
            foreach (FilterNode filterNode in this.filterNodes)
            {
                if (filterNode.Evaluate(target))
                {
                    isValidTarget = true;
                    break;
                }
            }
        }

        if (isValidTarget)
        {
            Quaternion hitRotation = Quaternion.LookRotation(hitNormal);
            var triggerInfo = new SpellTriggerInfo(false, gameObject, this.state, hitPosition, this.transform.rotation, hitRotation, target);

            foreach (EffectNode effect in outcomeNodes.OfType<EffectNode>())
            {
                effect.Execute(triggerInfo);
            }
            foreach (CoreNode core in outcomeNodes.OfType<CoreNode>())
            {
                //core.CreateSpellCore(triggerInfo);
            }
        }
    }
}
