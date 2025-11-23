using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "DistanceIsLessThan", story: "[Self] is less that [x] meter units from [target]", category: "Conditions", id: "da30ac199cc223669dd5bc0285db9183")]
public partial class DistanceIsLessThanCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<float> X;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    public override bool IsTrue()
    {
        var controller = Self.Value.GetComponent<NPCActiveRagdollController>();
        if (controller == null) return false;
        if (Target.Value == null) return false;
        float distance = Vector3.Distance(controller.coreRB.transform.position, NPCHelpers.GetCoreTransformFromRoot(Target.Value).position);
        return (distance <= X);
    }

}
