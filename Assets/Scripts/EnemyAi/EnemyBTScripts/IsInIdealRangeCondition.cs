using System;
using Unity.Behavior;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "IsOutOfIdealRangeCondition", story: "[Self] is out of ideal range of [Target]", category: "Conditions", id: "1ea4758cdb5bc2228c2e71366b13b1f5")]
public partial class IsInIdealRangeCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    public override bool IsTrue()
    {
        if(Target.Value == null) return false;
        var combat = Self.Value.GetComponent<EnemyCombat>();
        if (combat == null) return false;

        float distance = Vector3.Distance(Self.Value.transform.position, Target.Value.transform.position);
        return !(distance >= combat.idealMinDistance && distance <= combat.idealMaxDistance);
    }

}
