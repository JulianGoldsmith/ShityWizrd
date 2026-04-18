using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using Fusion;

public class OverlapSphereST : SpellTrigger
{
    // defines the maximum number colliders that can 
    // be hit.
    // since this is called every tick, we use non-alloc
    // to reduce garbage collection and frame spikes.
    const int MAX_TARGETS = 16;

    public float size = 1.0f;
    Collider[] non_alloc_colliders = new Collider[MAX_TARGETS];
    List<Collider> colliders_already_applied_To = new List<Collider>();

    public bool singleTrigger = false;
        
    public override void OnTick()
    {
        base.OnTick();
        // only runs on host...

        // should add a layer mask
        // LayerMask mask
        // currently just hits all colliders and skips if not relevant,
        // which uses up non_alloc capacity.
        int hit = Physics.OverlapSphereNonAlloc(
            transform.position,
            size, // this maybe should've been half size, since its radius not width.
            non_alloc_colliders,
            SpellSystemHelpers.GeneralCollisionLayerMask());

        //non_alloc_colliders = Physics.OverlapSphere(
        //    transform.position,
        //    size, // this maybe should've been half size, since its radius not width.
        //    SpellSystemHelpers.GeneralCollisionLayerMask());

        //int hit = non_alloc_colliders.Length;

        if (hit <= 0)
            return;

        for (int i = 0; i < hit; ++i)
        {
            
            bool shouldHanldeCollision = singleTrigger? !colliders_already_applied_To.Contains(non_alloc_colliders[i]) : true;
            if (shouldHanldeCollision)
            {
                colliders_already_applied_To.Add(non_alloc_colliders[i]);
                HandleOverlap(
                    non_alloc_colliders[i].gameObject,
                    transform.position,
                    (transform.position - non_alloc_colliders[i].transform.position).normalized
                );
            }
            
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blueViolet;
        Gizmos.DrawWireSphere(transform.position, size);
    }

    private void HandleOverlap(GameObject target, Vector3 hitPosition, Vector3 hitNormal)
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
            Quaternion hitRotation;
            if (hitNormal.magnitude == 0)
                hitRotation = Quaternion.identity;
            else
                hitRotation = Quaternion.LookRotation(hitNormal);
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

