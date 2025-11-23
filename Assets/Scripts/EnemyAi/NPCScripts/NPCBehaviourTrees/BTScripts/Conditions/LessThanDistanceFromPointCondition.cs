using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "LessThanDistanceFromPoint", story: "[Self] is less than [Distance] metre units from [Point]", category: "Conditions", id: "1a93473fa31d49f2287b724e02109d26")]
public partial class LessThanDistanceFromPointCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<float> Distance;
    [SerializeReference] public BlackboardVariable<Vector3> Point;

    public override bool IsTrue()
    {
        var controller = Self.Value.GetComponent<NPCActiveRagdollController>();
        if (controller == null) return false;
        if (Point.Value == null) return false;
        float distance = Vector3.Distance(controller.coreRB.transform.position, Point.Value);
        return (distance <= Distance);
    }
}
