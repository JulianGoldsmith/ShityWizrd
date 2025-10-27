using UnityEngine;

public class HomingMovementSB : SpellBehaviour
{
    public float turn_rate;
    public bool maintain_target;
    public float search_range = 5.0f;
    public float min_speed;

    public override void OnAttach(BehaviourNode node, float _size = 1)
    {
        base.OnAttach(node, _size);

        current_target = null;
    }


    const int MAX_TARGETS = 1;
    Collider[] non_alloc_colliders = new Collider[MAX_TARGETS];
    Collider current_target = null;
    public override void OnTick()
    {
        base.OnTick();

        // might be better to instead find all possible targets and
        // choose the closest.
        // that's not how this currently works, technically.
        Debug.Log($"homing tick");
        if (CurrentTargetOutOfRange())
        {
            // do this so I don't home to myself:
            int layer = transform.gameObject.layer;
            transform.gameObject.layer = 0;
            int hit = Physics.OverlapSphereNonAlloc(
                transform.position,
                search_range,
                non_alloc_colliders,
                SpellSystemHelpers.GeneralCollisionLayerMask());
            transform.gameObject.layer = layer;

            if (hit <= 0)
            {
                current_target = null;
                return;
            }

            current_target = non_alloc_colliders[0];
        }

        if (current_target == null)
            return;

        HomeTowards(current_target.gameObject);
    }

    bool CurrentTargetOutOfRange()
    {
        return current_target == null || (current_target.transform.position - transform.position).magnitude > search_range;
    }

    private void HomeTowards(GameObject target)
    {
        // rotate to look towards and angle velocity towards target.
        if (scpo == null || scpo.rb == null)
            return;

        Vector3 currentvel = scpo.rb.linearVelocity;
        Vector3 targetvel = (target.transform.position - transform.position).normalized * Mathf.Max(min_speed, currentvel.magnitude);
        if (currentvel.magnitude <= 0 && min_speed <= 0)
            return;
        targetvel = Vector3.RotateTowards(currentvel, targetvel, turn_rate * Mathf.Deg2Rad, float.MaxValue);
        
        scpo.ApplyForce(targetvel - currentvel, ForceMode.VelocityChange);
    }

}
