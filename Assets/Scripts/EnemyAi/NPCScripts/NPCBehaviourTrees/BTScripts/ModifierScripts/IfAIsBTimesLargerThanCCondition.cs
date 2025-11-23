using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "If A is B times larger than C", story: "If [A] is [B] times larger than [C]", category: "Conditions", id: "819f0b2e72f998d19398da41d21dabef")]
public partial class IfAIsBTimesLargerThanCCondition : Condition
{
    [SerializeReference] public BlackboardVariable<float> A;
    [SerializeReference] public BlackboardVariable<float> B;
    [SerializeReference] public BlackboardVariable<float> C;

    public override bool IsTrue()
    {
        return (A != null && B != null && C != null) && (A.Value > B.Value * C.Value);
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
