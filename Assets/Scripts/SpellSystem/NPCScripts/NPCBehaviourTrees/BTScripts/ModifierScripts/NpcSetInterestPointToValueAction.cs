using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NPCSetInterestPointToValue", story: "[Self] Sets current interest threat to [Value] and position [Position]", category: "Action", id: "95538205e12614cfb97cb21a51869867")]
public partial class NpcSetInterestPointToValueAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<float> Value;
    [SerializeReference] public BlackboardVariable<Vector3> Position;

    protected override Status OnStart()
    {
        if (Self.Value.TryGetComponent<NPCAggroController>(out NPCAggroController NPCAC)) {
            InterestPoint interestPoint = new InterestPoint
            {
                Position = Position.Value,
                Threat = Value.Value
            };

            NPCAC.CurrentInterestPoint = interestPoint;
        }
        else
        {
            return Status.Failure;
        }

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

