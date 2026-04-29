using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "TargetIsNull", story: "[Target] Is [IsEqual] Null", category: "Conditions", id: "823fc00c66b12977a9b26eaef404eeb8")]
public partial class TargetIsNullCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [Comparison(comparisonType: ComparisonType.Boolean)]
    [SerializeReference] public BlackboardVariable<ConditionOperator> IsEqual;

    public override bool IsTrue()
    {
        return ConditionUtils.Evaluate(Target.Value == null, IsEqual, true);

    }

  
}
