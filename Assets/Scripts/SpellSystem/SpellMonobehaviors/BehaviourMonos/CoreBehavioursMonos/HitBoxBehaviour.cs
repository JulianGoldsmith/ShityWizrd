using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// HitBox logic. This sits on a hitbox, detects collisions/ overlaps, collects a list of hits, enables/ disables the hitbox and resets each "swing"
/// </summary>

public class HitBoxBehaviour : MonoBehaviour
{

    private CastActionController _caster;
    private SpellState _activeSpellState;

    public List<GameObject> objectsHitThisActivation = new List<GameObject>();

    public Collider hitBoxCollider;

    public Rigidbody _drivingRigidbody;

    public void Initialize(CastActionController caster, SpellState state)
    {
        _caster = caster;
        _activeSpellState = state;
    }

    public void ResetHitBox()
    {
        objectsHitThisActivation.Clear();
    }
    public void EnableHitBox()
    {
        Debug.Log("Activating hit box from the HITBOX");

        hitBoxCollider.enabled = true;
    }
    public void DisableHitBox()
    {
        hitBoxCollider.enabled = false;
    }


    public void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.gameObject, collision.GetContact(0).point);
    }
    public void OnTriggerEnter(Collider other)  
    {
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        HandleHit(other.gameObject, hitPoint);
    }

    private void HandleHit(GameObject hitObject, Vector3 hitPoint)
    {
        if (_caster == null || _activeSpellState == null)
            return;

        if (objectsHitThisActivation.Contains(hitObject))
            return;

        Vector3 swingMomentum = Vector3.zero;
        if (_drivingRigidbody != null)
        {
            swingMomentum = _drivingRigidbody.GetPointVelocity(hitPoint);
        }

        objectsHitThisActivation.Add(hitObject);

        // Report the hit back to the spell node
        if (_activeSpellState.OriginalCasterNode is HitBoxCastNode node)
        {
            // Pass the state, caster, and *what* we hit
            node.HandleTrigger(_activeSpellState, _caster, hitObject, hitPoint, swingMomentum);
        }
    }
}
