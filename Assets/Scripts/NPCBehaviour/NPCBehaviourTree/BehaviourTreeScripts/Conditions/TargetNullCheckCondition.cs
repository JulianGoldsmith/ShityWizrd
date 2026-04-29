using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "TargetNullCheck", story: "[Target] is [IsEqual] Null", category: "Conditions", id: "85cc4ffe1a8a7615ce63377d071d09fd")]
public partial class TargetNullCheckCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [Comparison(comparisonType: ComparisonType.Boolean)]
    [SerializeReference] public BlackboardVariable<ConditionOperator> IsEqual;

    public override bool IsTrue()
    {
        return ConditionUtils.Evaluate(Target.Value == null, IsEqual, true);
    }
}
