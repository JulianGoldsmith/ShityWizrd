using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BtPathfindToPoint", story: "[Self] paths to point: [Point] mode: [MovementMode]", category: "Action", id: "61d85a0cdd268e7b16b2513ce74916e4")]
public partial class BtPathfindToPointAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> Point;
    [SerializeReference] public BlackboardVariable<NPCMovementMode> MovementMode;
    protected override Status OnStart()
    {
        if (Self.Value == null) return Status.Failure;

        var manager = Self.Value.GetComponent<NPCBehaviourManager>();

        if (manager == null || manager.Runner == null) return Status.Failure;

        int targetStartTick = manager.GetCurrentIntentStartTick();
        int revision = manager.BeginCommandRevision();

        if (revision == 0) return Status.Failure;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Move_PathfindToPoint,
            Priority = 10,
            EndTick = targetStartTick + 999999,
            VectorData = Point.Value,
            MovementMode = MovementMode.Value
        };

        bool success = manager.TryScheduleChannelCommand(payload, targetStartTick, revision);

        return success ? Status.Success : Status.Failure;
    }
}

