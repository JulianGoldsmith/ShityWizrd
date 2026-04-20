using Fusion;
using UnityEngine;

//holds information on the event that triggered the core of the spell 
//For example projectile hits floor and creates explosion this is passed to create the explosion in the right place
public struct SpellTriggerInfo
{
    public bool IsValid;

    public SpellState State;
    public GameObject Source; 
    public bool IsCast; 
    public bool HasOverridePosition; 
    public Vector3 TriggerPoint; 
    public Quaternion TriggerRotation; 
    public Quaternion TriggerNormal;
    public GameObject HitObject; 
    public Vector3 TriggerVector;


    public SpellTriggerInfo(PlayerRef playerref, GameObject source, bool isCast, SpellState state, Vector3 position, Quaternion rotation, Quaternion normal, Vector3 triggerVector, GameObject hitObject = null)
    {
        IsValid = true;
        IsCast = isCast;
        Source = source;
        State = state;
        HasOverridePosition = true;
        TriggerPoint = position;
        TriggerRotation = rotation;
        TriggerNormal = normal;
        HitObject = hitObject;
        TriggerVector = triggerVector;
    }

    public SpellTriggerInfo(bool isCast, GameObject source, SpellState state, Vector3 position, Quaternion rotation, Vector3 triggerVector, GameObject hitObject = null)
    {
        IsValid = true;
        IsCast = isCast;
        Source = source;
        State = state;
        HasOverridePosition = true;
        TriggerPoint = position;
        TriggerRotation = rotation;
        TriggerNormal = Quaternion.identity;
        HitObject = hitObject;
        TriggerVector = triggerVector;
    }

    public SpellTriggerInfo(bool isCast, GameObject source, SpellState state, Vector3 position, Quaternion rotation, Quaternion normal, GameObject hitObject = null)
    {
        IsValid = true;
        IsCast = isCast;
        Source = source;
        State = state;
        HasOverridePosition = true;
        TriggerPoint = position;
        TriggerRotation = rotation;
        TriggerNormal = normal;
        HitObject = hitObject;
        TriggerVector = Vector3.zero;
    }

    public SpellTriggerInfo(bool isCast, GameObject source, SpellState state, Vector3 position, Quaternion rotation, GameObject hitObject = null)
    {
        IsValid = true;
        IsCast = isCast;
        Source = source;
        State = state;
        HasOverridePosition = true;
        TriggerPoint = position;
        TriggerRotation = rotation;
        TriggerNormal = Quaternion.identity;
        HitObject = hitObject;
        TriggerVector = Vector3.zero;
    }

    public SpellTriggerInfo(bool isCast, GameObject source, SpellState state, GameObject hitObject = null)
    {
        IsValid = true;
        IsCast = isCast;
        Source = source;
        State = state;
        HasOverridePosition = false;
        TriggerPoint = Vector3.zero;
        TriggerRotation = Quaternion.identity;
        TriggerNormal = Quaternion.identity;
        HitObject = hitObject;
        TriggerVector = Vector3.zero;
    }

    public SpellTriggerInfo(bool isCast, GameObject source, SpellState state, Vector3 triggerVector, GameObject hitObject = null)
    {
        IsValid = true;
        IsCast = isCast;
        Source = source;
        State = state;
        HasOverridePosition = false;
        TriggerPoint = Vector3.zero;
        TriggerRotation = Quaternion.identity;
        TriggerNormal = Quaternion.identity;
        HitObject = hitObject;
        TriggerVector = triggerVector;
    }

}
