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
        [Tooltip("Pushes in the direction of the spell's trigger e.g hitbox momentum")]
        TRIGGER_DIRECTION
    }

    public enum ForceScaling
    {
        [Tooltip("Applies a static Force Multiplier value.")]
        STATIC,

        [Tooltip("Scales the Force Multiplier based on distance from the source.")]
        BY_DISTANCE,

        [Tooltip("The force is the Force Multiplier multiplied by the triggers momentum magnitude.")]
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
        //Debug.Log($"PUSHPULL node Executed");

        /*foreach (var info in triggerInfos)
        {
            GameObject target = info.HitObject;



            if (target == null) continue;

            Vector3 rawDirection = GetRawDirection(info, target);
            float magnitude = GetForceMagnitude(info, rawDirection);
            //Debug.Log($"PUSHPULL node added {magnitude} force to {target} in direction {rawDirection.normalized}");
            if (rawDirection.sqrMagnitude > 0.001f)
            {
                if (target.TryGetComponent<PhysicsObject>(out PhysicsObject PO))
                {
                    PO.ApplyForce(rawDirection.normalized * magnitude, forceMode);
                    PO.BonkFromImpulse(magnitude, null); // (Add your instigator here later)
                }
                else if (target.TryGetComponent<PhysicsSubObject>(out PhysicsSubObject PSO))
                {
                    if (PSO.rb != null)
                    {
                        PSO.rb.AddForce(rawDirection.normalized * magnitude, forceMode);
                    }

                    // But report the damage/stagger to the parent brain
                    PSO.parent_physics_object.BonkFromImpulse(magnitude, null); // NEED TO PUT INSTIGATOR HERE!!
                }
            }
        }*/
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
                // This could be like momentum vector from the hitbox
                return info.TriggerVector;
        }
        return Vector3.zero; 
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

    public override IEffect CompileEffect()
    {
        float bakedForce = GetFinalValue(nameof(pushPullForce), pushPullForce);

        return new PushPullEffect()
        {
            Direction = pushPullDirection,
            Scaling = forceScaling,
            ForceMultiplier = bakedForce,
            ForceMode = forceMode,
            DistanceCurve = force_scaling_by_distance
        };
    }
}

public class PushPullEffect : IEffect
{
    public PushPullNode.PUSH_PULL_DIRECTION Direction;
    public PushPullNode.ForceScaling Scaling;
    public float ForceMultiplier;
    public ForceMode ForceMode;
    public AnimationCurve DistanceCurve;

    public void Execute(SpellCreatedCore core, List<SpellTriggerInfo> hitInfos)
    {
        foreach (var info in hitInfos)
        {
            // Struct validation check
            if (!info.IsValid || info.HitObject == null) continue;

            GameObject target = info.HitObject;

            Vector3 rawDirection = GetRawDirection(info, target);
            float magnitude = GetForceMagnitude(info, rawDirection);

            if (rawDirection.sqrMagnitude > 0.001f)
            {
                PhysicsObject instigator = null;
                if (info.Source != null)
                {
                    instigator = info.Source.GetComponent<PhysicsObject>();

                    if (instigator == null && info.Source.TryGetComponent<PhysicsSubObject>(out var pso))
                    {
                        instigator = pso.parent_physics_object;
                    }
                }

                if (target.TryGetComponent<PhysicsObject>(out PhysicsObject PO))
                {
                    PO.ApplyForce(rawDirection.normalized * magnitude, ForceMode);
                    //PO.BonkFromImpulse(magnitude, instigator);
                }
                else if (target.TryGetComponent<PhysicsSubObject>(out PhysicsSubObject PSO))
                {
                    if (PSO.rb != null)
                    {
                        PSO.rb.AddForce(rawDirection.normalized * magnitude, ForceMode);
                    }
                    //PSO.parent_physics_object.BonkFromImpulse(magnitude, instigator);
                }
            }
        }
    }

    private Vector3 GetRawDirection(SpellTriggerInfo info, GameObject target)
    {
        switch (Direction)
        {
            case PushPullNode.PUSH_PULL_DIRECTION.PUSH_FROM_SOURCE:
                return target.transform.position - info.Source.transform.position;

            case PushPullNode.PUSH_PULL_DIRECTION.TO_SOURCE:
                return info.Source.transform.position - target.transform.position;

            case PushPullNode.PUSH_PULL_DIRECTION.SOURCE_FORWARD:
                return info.Source.transform.forward;

            case PushPullNode.PUSH_PULL_DIRECTION.TRIGGER_DIRECTION:
                return info.TriggerVector;
        }
        return Vector3.zero;
    }

    private float GetForceMagnitude(SpellTriggerInfo info, Vector3 rawDirection)
    {
        switch (Scaling)
        {
            case PushPullNode.ForceScaling.BY_TRIGGER_FORCE:
                return info.TriggerVector.magnitude * ForceMultiplier;

            case PushPullNode.ForceScaling.BY_DISTANCE:
                float distance = rawDirection.magnitude;
                float distanceScale = (DistanceCurve != null) ? DistanceCurve.Evaluate(distance) : 1.0f;
                return ForceMultiplier * distanceScale;

            case PushPullNode.ForceScaling.STATIC:
            default:
                return ForceMultiplier;
        }
    }
}