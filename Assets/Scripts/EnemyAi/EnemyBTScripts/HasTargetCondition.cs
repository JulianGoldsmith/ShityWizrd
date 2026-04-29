using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "HasTarget", story: "[Target] is [Comparison] null", category: "Conditions", id: "1708c7d03884ee3f610359886ed86d4f")]
public partial class HasTargetCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    [Comparison(comparisonType: ComparisonType.Boolean)]
    [SerializeReference] public BlackboardVariable<ConditionOperator> Comparison;

    public override bool IsTrue()
    {
        // 1. Safety check to prevent errors in the editor
        if (Target == null || Comparison == null)
        {
            Debug.LogWarning("HasTargetCondition: 'Target' or 'Comparison' is missing in the graph!");
            return false;
        }

        // 2. Evaluate the condition
        // This checks: Does the statement "Target is null" evaluate to TRUE using your chosen operator?
        return ConditionUtils.Evaluate(Target.Value == null, Comparison, true);
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
