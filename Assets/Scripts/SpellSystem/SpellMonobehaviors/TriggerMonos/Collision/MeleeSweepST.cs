using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeleeSweepST : SpellTrigger
{
    private float weaponRadius = 0.5f;
    private Transform weaponTip;
    private Transform weaponBase;
    private Vector3 previousTipPosition;
    private Vector3 previousBasePosition;
    private Item item;

    private readonly HashSet<Collider> collidersHitThisSwing = new HashSet<Collider>();

    public void Init(SpellState state)
    {
        this.state = state;
        item = state.CastItem;
        this.transform.position = item.transform.position;
        this.transform.parent = item.transform;

        weaponBase = item.weaponBase;
        weaponTip = item.weaponEnd;

        previousBasePosition = weaponBase.position;
        previousTipPosition = weaponTip.position;
    }
    void FixedUpdate()
    {
        Vector3 currentTipPosition = weaponTip.position;
        Vector3 currentBasePosition = weaponBase.position;


        Vector3 swingDirection = (currentBasePosition - previousBasePosition).normalized;
        float swingDistance = Vector3.Distance(previousBasePosition, currentBasePosition);

        if (swingDistance <= 0) return;

        RaycastHit[] hits = Physics.CapsuleCastAll(previousBasePosition, previousTipPosition, weaponRadius,
                                                   swingDirection, swingDistance);

        foreach (var hit in hits)
        {
   
            if (collidersHitThisSwing.Add(hit.collider))
            {

                HandleCollision(hit.collider.gameObject, hit.point, hit.normal);
            }
        }


        previousBasePosition = currentBasePosition;
        previousTipPosition = currentTipPosition;
    }

    void Update()
    {
        
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
            var triggerInfo = new SpellTriggerInfo(false, this.state, hitPosition, this.transform.rotation, hitRotation, target);

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
