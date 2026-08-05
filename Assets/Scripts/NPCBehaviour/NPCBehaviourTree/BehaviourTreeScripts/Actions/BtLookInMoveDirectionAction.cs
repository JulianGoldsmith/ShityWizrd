using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BtLookInMoveDirection", story: "[Self] looks in move direction", category: "Action", id: "da7128eebe1c118d97ccde00cff98e2f")]
public partial class BtLookInMoveDirectionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    protected override Status OnStart()
    {
        if (Self.Value == null) return Status.Failure;

        NPCBehaviourManager manager = Self.Value.GetComponent<NPCBehaviourManager>();
        if (manager == null || manager.Runner == null) return Status.Failure;

        int targetStartTick = manager.GetCurrentIntentStartTick();
        int revision = manager.BeginCommandRevision();

        if (revision == 0) return Status.Failure;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Look_InMoveDirection,
            Priority = 10,
            EndTick = targetStartTick + 999999
        };

        bool success = manager.TryScheduleChannelCommand(payload, targetStartTick, revision);
        return success ? Status.Success : Status.Failure;
    }
}

