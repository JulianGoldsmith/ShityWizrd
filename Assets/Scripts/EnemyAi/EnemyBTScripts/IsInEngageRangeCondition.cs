using System;
using Unity.Behavior;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "IsInEngageRange", story: "[Self] is in engage range of [Target]", category: "Conditions", id: "90c140fe0911414ff0fe9c295f0311f5")]
public partial class IsInEngageRangeCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    public override bool IsTrue()
    {
        var combat = Self.Value.GetComponent<EnemyCombat>();
        if (combat == null) return false;

        float distance = Vector3.Distance(Self.Value.transform.position, Target.Value.transform.position);
        return (distance <= combat.engageDistance);
    }

}
