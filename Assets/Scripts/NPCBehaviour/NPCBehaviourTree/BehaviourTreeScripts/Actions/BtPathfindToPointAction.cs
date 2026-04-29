using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BtPathfindToPoint", story: "[Self] paths to point: [Point] speed [Speed]", category: "Action", id: "61d85a0cdd268e7b16b2513ce74916e4")]
public partial class BtPathfindToPointAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> Point;
    [SerializeReference] public BlackboardVariable<float> Speed;
    protected override Status OnStart()
    {
        if (Self.Value == null) return Status.Failure;

        var manager = Self.Value.GetComponent<NPCBehaviourManager>();

        if (manager == null || manager.Runner == null) return Status.Failure;

        // Auto-Delay Sync
        int targetStartTick = manager.GlobalClearTick > manager.Runner.Tick
            ? manager.GlobalClearTick
            : manager.Runner.Tick;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Move_PathfindToPoint, // Use the existing Point enum
            Priority = 10,
            SetTick = manager.Runner.Tick,
            StartTick = targetStartTick,
            EndTick = targetStartTick + 999999,
            VectorData = Point.Value,        // Push the Vector3 here!
            FloatData = Speed.Value
        };

        bool success = manager.TryAddCommand(payload);

        return success ? Status.Success : Status.Failure;
    }
}

