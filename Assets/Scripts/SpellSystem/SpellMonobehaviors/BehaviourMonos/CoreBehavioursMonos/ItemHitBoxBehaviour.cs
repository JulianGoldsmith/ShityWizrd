using UnityEngine;


/// <summary>
/// depreciated
/// </summary>
public class ItemHitBoxBehaviour : SpellBehaviour
{
    GameObject hitBox;
    Vector3 localPos;
    Quaternion localRot;

    public void Init(SpellTriggerInfo _triggerInfo)
    {
        triggerInfo = _triggerInfo;

        
    }

    void Start()
    {
        
    }

    void FixedUpdate()
    {

    }

    void ActivateHitBox()
    {
        //Activate the hitbox collider
    }
    void DeActivateHitBox()
    {
        //Destroy the cloned gameObject
    }
}
