using Fusion;
using UnityEngine;

//holds information on the event that triggered the core of the spell 
//For example projectile hits floor and creates explosion this is passed to create the explosion in the right place
public class SpellTriggerInfo
{
    public SpellState State { get; private set; }
    public GameObject Source {  get; private set; } //Gives the source that created the trigger, e.g. the spell/physicsobject that called the trigger event.
    public bool IsCast {  get; private set; } //Tells us if what triggered this was a CastNode from player or AI
    public bool HasOverridePosition { get; private set; } //If we set the position and rotation 
    public Vector3 TriggerPoint { get; private set; } //position of trigger ie - collision point or rayhit point
    public Quaternion TriggerRotation { get; private set; } //rotation of trigger ie - collision rotation or rayhit rotation
    public Quaternion TriggerNormal { get; private set; }
    public GameObject HitObject { get; private set; } //object hit that triggered this particular trigger
    public Vector3 TriggerVector { get; private set; } //represents things like impactVector or throwVector. 
    

    public SpellTriggerInfo(PlayerRef playerref, GameObject source, bool isCast, SpellState state, Vector3 position, Quaternion rotation, Quaternion normal, Vector3 tiggerVector, GameObject hitObject = null)
    {
        IsCast = isCast;
        Source = source;
        State = state;
        HasOverridePosition = true;
        TriggerPoint = position;
        TriggerRotation = rotation;
        TriggerNormal = normal;
        HitObject = hitObject;
        TriggerVector = tiggerVector;
    }

    public SpellTriggerInfo(bool isCast, GameObject source, SpellState state, Vector3 position, Quaternion rotation, Vector3 tiggerVector, GameObject hitObject = null)
    {
        IsCast = isCast;
        Source = Source;
        State = state;
        HasOverridePosition = true;
        TriggerPoint = position;
        TriggerRotation = rotation;

        HitObject = hitObject;
        TriggerVector = tiggerVector;
    }

    public SpellTriggerInfo(bool isCast, GameObject source, SpellState state, Vector3 position, Quaternion rotation, Quaternion normal,  GameObject hitObject = null)
    {
        IsCast = isCast;
        Source = source;
        State = state;
        HasOverridePosition = true;
        TriggerPoint = position;
        TriggerRotation = rotation;
        TriggerNormal = normal;
        HitObject = hitObject;

    }

    public SpellTriggerInfo(bool isCast, GameObject source, SpellState state, Vector3 position, Quaternion rotation, GameObject hitObject = null)
    {
        IsCast = isCast;
        Source = source;
        State = state; 
        HasOverridePosition = true;
        TriggerPoint = position;
        TriggerRotation = rotation;
        TriggerNormal = Quaternion.identity;
        HitObject = hitObject;
    }

    public SpellTriggerInfo(bool isCast, GameObject source, SpellState state, GameObject hitObject = null)
    {
        IsCast = isCast;
        Source = source;
        State = state; 
        HasOverridePosition = false;
        TriggerPoint = Vector3.zero;
        TriggerRotation = Quaternion.identity;
        TriggerNormal = Quaternion.identity; 
        HitObject = hitObject;
    }

    public SpellTriggerInfo(bool isCast, GameObject source, SpellState state, Vector3 triggerVector, GameObject hitObject = null)
    {
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
