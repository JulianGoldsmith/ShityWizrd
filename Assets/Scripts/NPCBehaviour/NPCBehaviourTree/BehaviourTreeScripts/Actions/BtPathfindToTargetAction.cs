using Fusion;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BT_PathfindToTarget", story: "[Self] paths to target: [Target] speed: [Speed]", category: "Action", id: "ec6506e0cf16c217626b6b74d28687ad")]
public partial class BtPathfindToTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> Speed;
    protected override Status OnStart()
    {
        if (Self.Value == null || Target.Value == null) return Status.Failure;

        var manager = Self.Value.GetComponent<NPCBehaviourManager>();
        var targetNetworkObj = Target.Value.GetComponent<NetworkObject>();

        if (manager == null || targetNetworkObj == null || manager.Runner == null) return Status.Failure;

        int targetStartTick = manager.GlobalClearTick > manager.Runner.Tick
            ? manager.GlobalClearTick
            : manager.Runner.Tick;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Move_PathfindToID,
            Priority = 10,
            SetTick = manager.Runner.Tick,         // The moment the BT decided to do this
            StartTick = targetStartTick,           // The moment the muscle should activate
            EndTick = targetStartTick + 999999,    // Run indefinitely until the next state Clear
            TargetID = targetNetworkObj.Id,        // Pass the target's NetworkID
            FloatData = Speed.Value                // Pass the movement speed
        };

        bool success = manager.TryAddCommand(payload);

        return success ? Status.Success : Status.Failure;
    }
}

