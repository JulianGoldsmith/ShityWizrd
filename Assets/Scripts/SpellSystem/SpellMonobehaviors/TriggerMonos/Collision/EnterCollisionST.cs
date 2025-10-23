using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class EnterCollisionST : SpellTrigger
{
    Rigidbody rb;
    Collider col;
    private void Start()
    {
        if (this.GetComponent<Collider>() == null)
        {
            var col = this.AddComponent<SphereCollider>();
            col.isTrigger = false;
        }
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
        HandleCollision(
            other.gameObject,
            transform.position,
            Vector3.up // need to fix this to some how find the normal at this point.
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
                core.CreateSpellCore(triggerInfo);
            }
        }
    }
}
