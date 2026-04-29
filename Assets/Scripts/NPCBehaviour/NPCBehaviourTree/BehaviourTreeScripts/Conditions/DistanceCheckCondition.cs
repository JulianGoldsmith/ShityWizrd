using System;
using Unity.Behavior;
using UnityEngine;
using Unity.Properties;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "DistanceCheck", story: "[Transform] distance is [comaprison] [dist] to [target]", category: "Conditions", id: "d1d7c84896ec71a3cf7b4ced093cbbbd")]
public partial class DistanceCheckCondition : Condition
{
    [SerializeReference] public BlackboardVariable<Transform> Transform;
    [SerializeReference] public BlackboardVariable<float> Dist;
    [Comparison(comparisonType: ComparisonType.All)]
    [SerializeReference] public BlackboardVariable<ConditionOperator> Comaprison;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    public override bool IsTrue()
    {
        if (Transform.Value == null || Target.Value == null)
        {
            return false;
        }
        Vector3 sourcePos = Transform.Value.position;
        Vector3 targetPos = Target.Value.transform.position;

        if (Transform.Value.gameObject.TryGetComponent<NPCActiveRagdollController>(out var sourceRagdoll))
        {
            sourcePos = sourceRagdoll.coreRB.position;
        }
        if (Transform.Value.gameObject.TryGetComponent<HybridCharacterController>(out var hcc))
        {
            sourcePos = hcc.hipsRb.position;
        }

        if (Target.Value.gameObject.TryGetComponent<NPCActiveRagdollController>(out var targetRagdoll))
        {
            targetPos = targetRagdoll.coreRB.position;
        }
        if (Target.Value.gameObject.TryGetComponent<HybridCharacterController>(out var targethcc))
        {
            targetPos = targethcc.hipsRb.position;
        }

        float distance = Vector3.Distance(sourcePos, targetPos);
        return ConditionUtils.Evaluate(distance, Comaprison, Dist.Value);
    }
}
