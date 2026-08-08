using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NPCStartRandomActionTimer", story: "[Self] starts [rng] timer between [min] and [max]", category: "Action", id: "54b3aca0318baf709d529c5064ebd248")]
public partial class NpcStartRandomActionTimerAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<float> Rng;
    [SerializeReference] public BlackboardVariable<float> Min;
    [SerializeReference] public BlackboardVariable<float> Max;
    protected override Status OnStart()
    {
        Rng.Value = UnityEngine.Random.Range(Min.Value, Max.Value);
        return Status.Success;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

