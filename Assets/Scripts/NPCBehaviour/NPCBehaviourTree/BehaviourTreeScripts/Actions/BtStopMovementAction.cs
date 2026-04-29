using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BtStopMovement", story: "[Self] stops moving", category: "Action", id: "0a91b00f3a44573340861530aef29db8")]
public partial class BtStopMovementAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    protected override Status OnStart()
    {
        if (Self.Value == null) return Status.Failure;

        var manager = Self.Value.GetComponent<NPCBehaviourManager>();

        if (manager == null || manager.Runner == null) return Status.Failure;

        int targetStartTick = manager.GlobalClearTick > manager.Runner.Tick
            ? manager.GlobalClearTick
            : manager.Runner.Tick;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Move_Stop, 
            Priority = 20,                   
            SetTick = manager.Runner.Tick,
            StartTick = targetStartTick,
            EndTick = targetStartTick + 999999     
        };

        bool success = manager.TryAddCommand(payload);

        return success ? Status.Success : Status.Failure;
    }
}

