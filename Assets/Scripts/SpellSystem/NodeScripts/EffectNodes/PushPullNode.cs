using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

[CreateAssetMenu(fileName = "PushPull", menuName = "SpellNodes/Effect/PushPull Effect")]
public class PushPullNode : EffectNode
{
    public enum PUSH_PULL_DIRECTION
    {
        TO_SOURCE = 0,
        PUSH_FROM_SOURCE,
        SOURCE_FORWARD,
        [Tooltip("Pushes in the direction of the spell's trigger e.g, hitbox momentum")]
        TRIGGER_DIRECTION
    }

    public enum ForceScaling
    {
        [Tooltip("Applies a simple, static Force Multiplier value.")]
        STATIC,

        [Tooltip("Scales the Force Multiplier based on distance from the source.")]
        BY_DISTANCE,

        [Tooltip("The force is the Force Multiplier multiplied by the trigger's momentum magnitude.")]
        BY_TRIGGER_FORCE
    }

    public PUSH_PULL_DIRECTION pushPullDirection = PUSH_PULL_DIRECTION.TO_SOURCE;
    public ForceScaling forceScaling = ForceScaling.STATIC;



    [Promotable("PushPullForce", DataTypeTag.Force)]
    public float pushPullForce = 10f;
    public ForceMode forceMode = ForceMode.Acceleration;

    // TODO: scaling based on size of effect?
    // i.e. proportioned out across the size of the aura?
    public AnimationCurve force_scaling_by_distance;

    public override void Execute(List<SpellTriggerInfo> triggerInfos)
    {
        Debug.Log($"PUSHPULL node Executed");

        foreach (var info in triggerInfos)
        {
            GameObject target = info.HitObject;

            if (target == null || !target.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                continue;
            }

            Vector3 rawDirection = GetRawDirection(info, target);
            float magnitude = GetForceMagnitude(info, rawDirection);
            Debug.Log($"PUSHPULL node added {magnitude} force to {target} in direction {rawDirection.normalized}");
            if (rawDirection.sqrMagnitude > 0.001f)
            {
                rb.AddForce(rawDirection.normalized * magnitude, forceMode);

            }
            if(target.TryGetComponent<PhysicsObject>(out PhysicsObject PO))
            {
                PO.BonkFromImpulse(magnitude, null); //need to put causeative object here!
            }
            else if(target.TryGetComponent<PhysicsSubObject>(out PhysicsSubObject PSO))
            {
                PSO.parent_physics_object.BonkFromImpulse(magnitude, null);
            }
        }
    }

    private Vector3 GetRawDirection(SpellTriggerInfo info, GameObject target)
    {
        switch (pushPullDirection)
        {
            case PUSH_PULL_DIRECTION.PUSH_FROM_SOURCE:
                return target.transform.position - info.Source.transform.position;

            case PUSH_PULL_DIRECTION.TO_SOURCE:
                return info.Source.transform.position - target.transform.position;

            case PUSH_PULL_DIRECTION.SOURCE_FORWARD:
                return info.Source.transform.forward;

            case PUSH_PULL_DIRECTION.TRIGGER_DIRECTION:
                // This is the momentum vector from the hitbox
                return info.TriggerVector;
        }
        return Vector3.zero; // Fallback
    }


    private float GetForceMagnitude(SpellTriggerInfo info, Vector3 rawDirection)
    {
        switch (forceScaling)
        {
            case ForceScaling.BY_TRIGGER_FORCE:
                return info.TriggerVector.magnitude * pushPullForce;

            case ForceScaling.BY_DISTANCE:
                float distance = rawDirection.magnitude;
                float distanceScale = (force_scaling_by_distance != null) ? force_scaling_by_distance.Evaluate(distance) : 1.0f;
                return pushPullForce * distanceScale;

            case ForceScaling.STATIC:
            default:
                return pushPullForce;
        }
    }



    // I didnt want to remove this but not used in this implementation?
    Vector3 CalcForce(GameObject source, GameObject target)
    {
        return pushPullForce * ForceDir(source, target);
    }
    Vector3 ForceDir(GameObject source, GameObject target)
    {
        Vector3 dir;
        switch (pushPullDirection)
        {
            case PUSH_PULL_DIRECTION.TO_SOURCE:
                dir = source.transform.position - target.transform.position;
                break;
            default:
                dir = source.transform.forward;
                break;
        }
        float scaling = 1.0f;
        if (force_scaling_by_distance != null)
            scaling = force_scaling_by_distance.Evaluate(dir.magnitude);
        return dir.normalized * scaling;
    }
}

