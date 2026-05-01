using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BtLookAtStaticPoint", story: "[Self] looks at static [Point] or [Target]", category: "Action", id: "344450f59d7c0bc53e68d5dfdf69290d")]
public partial class BtLookAtStaticPointAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> Point;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    protected override Status OnStart()
    {
        if (Self.Value == null) return Status.Failure;

        var manager = Self.Value.GetComponent<NPCBehaviourManager>();
        if (manager == null || manager.Runner == null) return Status.Failure;

        int targetStartTick = manager.GlobalClearTick > manager.Runner.Tick
            ? manager.GlobalClearTick
            : manager.Runner.Tick;

        Vector3 targ = Target.Value != null ? Target.Value.transform.position : Point.Value;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Look_AtPoint,
            Priority = 10,
            SetTick = manager.Runner.Tick,
            StartTick = targetStartTick,
            EndTick = targetStartTick + 999999, // Run forever until aborted/cleared
            VectorData = targ
        };

        bool success = manager.TryAddCommand(payload);

        return success ? Status.Success : Status.Failure;
    }
}

