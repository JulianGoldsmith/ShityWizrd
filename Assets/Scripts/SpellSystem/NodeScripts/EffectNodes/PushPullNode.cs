using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "PushPull", menuName = "SpellNodes/Effect/PushPull Effect")]
public class PushPullNode : EffectNode
{
    public enum PUSH_PULL_DIRECTION
    {
        TO_SOURCE = 0
    }
    public PUSH_PULL_DIRECTION pushPullDirection = PUSH_PULL_DIRECTION.TO_SOURCE;
    [Promotable("PushPullForce", DataTypeTag.Force)]
    public float pushPullForce = 10f;
    public ForceMode forceMode = ForceMode.Acceleration;

    // TODO: scaling based on size of effect?
    // i.e. proportioned out across the size of the aura?
    public AnimationCurve force_scaling_by_distance;

    public override void Execute(List<SpellTriggerInfo> triggerInfos)
    {
        foreach (var info in triggerInfos)
        {
            GameObject target = info.HitObject;

            if (target == null) continue;

            if (target.TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.AddForce(CalcForce(info.Source, target), forceMode);
            }
        }
    }

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

