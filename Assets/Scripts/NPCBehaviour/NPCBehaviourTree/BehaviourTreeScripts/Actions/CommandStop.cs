using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Command_Stop", story: "[NPC] StopCommand at tick: [Tick]", category: "Action", id: "617574d9613a31c4de7aa5193af6e77c")]
public partial class CommandStop : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> NPC;
    [SerializeReference] public BlackboardVariable<int> Tick;
    protected override Status OnStart()
    {
        if (NPC.Value == null) return Status.Failure;

        var manager = NPC.Value.GetComponent<NPCBehaviourManager>();
        if (manager == null) return Status.Failure;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Move_Stop,
            Priority = 50, 
            StartTick = manager.Runner.Tick + Tick,
            EndTick = manager.Runner.Tick + 99999
        };

        bool success = manager.TryAddCommand(payload);
        return success ? Status.Success : Status.Failure;
    }
}

