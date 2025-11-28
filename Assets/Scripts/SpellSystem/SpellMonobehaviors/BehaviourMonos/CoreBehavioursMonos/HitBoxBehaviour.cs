using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// HitBox logic. This sits on a hitbox, detects collisions/ overlaps, collects a list of hits, enables/ disables the hitbox and resets each "swing"
/// </summary>

public class HitBoxBehaviour : MonoBehaviour
{

    private CastActionController _npcCaster;
    private SpellState _activeSpellState;
    private EquipableItem _itemOwner;

    public List<GameObject> objectsHitThisActivation = new List<GameObject>();

    public Collider hitBoxCollider;

    public Rigidbody _drivingRigidbody;

    public void InitializeNull()
    {
        _npcCaster = null;
        _itemOwner = null;
        _activeSpellState = null;
        ResetHitBox();
    }

    public void Initialize(CastActionController caster, SpellState state)
    {
        _npcCaster = caster;
        _itemOwner = null; 
        _activeSpellState = state;
        ResetHitBox();
    }

    public void Initialize(EquipableItem item, SpellState state)
    {
        _itemOwner = item;
        _npcCaster = null;
        _activeSpellState = state;
        ResetHitBox();
    }

    public void ResetHitBox()
    {
        objectsHitThisActivation.Clear();
    }
    public void EnableHitBox()
    {
        Debug.Log("Activating hit box from the HITBOX");

        ResetHitBox();
        if (hitBoxCollider != null)
        {
            hitBoxCollider.enabled = true;
            Debug.Log($"Hitbox Enabled on {gameObject.name}");
        }
    }
    public void DisableHitBox()
    {
        if (hitBoxCollider != null)
        {
            hitBoxCollider.enabled = false;
        }
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
        if(_activeSpellState == null) return;
        if (_npcCaster == null && _itemOwner == null) return;

        if (objectsHitThisActivation.Contains(hitObject))
            return;

        if (_itemOwner != null && _itemOwner.HoldingPlayer != null && hitObject == _itemOwner.HoldingPlayer.gameObject) return;
        if (_npcCaster != null && hitObject == _npcCaster.gameObject) return;


        Vector3 swingMomentum = Vector3.zero;
        if (_drivingRigidbody != null)
        {
            swingMomentum = _drivingRigidbody.GetPointVelocity(hitPoint);
        }
        else
        {
            // Fallback momentum based on forward direction if no RB
            swingMomentum = transform.forward;
        }

        // 5. Register Hit
        objectsHitThisActivation.Add(hitObject);

        // 6. Delegate to the correct owner
        if (_itemOwner != null)
        {
            // NEW PATH: Tell the Item
            _itemOwner.OnMeleeHit(hitObject, _activeSpellState, hitPoint, swingMomentum);
        }
        else if (_npcCaster != null)
        {
            // OLD PATH: Tell the NPC Controller
            _npcCaster.OnItemHit(_activeSpellState, hitObject, hitPoint, swingMomentum);
        }
    }
}
