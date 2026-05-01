using Fusion;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BTExecuteAction", story: "[Self] executes Action ID [ActionID] at [Target]", category: "Action", id: "555ec19eb3d9af729a724266d2a75db0")]
public partial class BtExecuteAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<int> ActionID;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    protected override Status OnStart()
    {
        if (Self.Value == null || Target.Value == null) return Status.Failure;

        var manager = Self.Value.GetComponent<NPCBehaviourManager>();
        var targetNetworkObj = Target.Value.GetComponent<NetworkObject>();

        if (manager == null || targetNetworkObj == null || manager.Runner == null) return Status.Failure;

        // Auto-Delay Sync exactly like the Pathfinding nodes!
        int targetStartTick = manager.GlobalClearTick > manager.Runner.Tick
            ? manager.GlobalClearTick
            : manager.Runner.Tick;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Action_Execute,
            Priority = 5,                          // High priority to override normal looking/movement
            SetTick = manager.Runner.Tick,
            StartTick = targetStartTick,
            EndTick = targetStartTick + 999999,
            TargetID = targetNetworkObj.Id,
            IntData = ActionID.Value               // This is where we pass the Action array index (e.g. 0 for Fireball)
        };

        bool success = manager.TryAddCommand(payload);

        return success ? Status.Success : Status.Failure;
    }
}

