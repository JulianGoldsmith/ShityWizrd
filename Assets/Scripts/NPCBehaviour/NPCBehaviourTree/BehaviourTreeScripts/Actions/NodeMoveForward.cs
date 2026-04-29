using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Node_MoveForward", story: "[NPC] to move direction: [Direction] speed: [Speed] tick [Tick]", category: "Action", id: "83bdf2a977f1540fe3d9babbba43cbe9")]
public partial class NodeMoveForward : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> NPC;
    [SerializeReference] public BlackboardVariable<Vector3> Direction;
    [SerializeReference] public BlackboardVariable<float> Speed;
    [SerializeReference] public BlackboardVariable<int> Tick;
    protected override Status OnStart()
    {
        if (NPC.Value == null) return Status.Failure;

        var manager = NPC.Value.GetComponent<NPCBehaviourManager>();
        if (manager == null) return Status.Failure;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Move_Forward,
            Priority = 10,
            StartTick = manager.Runner.Tick + Tick,
            EndTick = manager.Runner.Tick + 99999, 
            VectorData = Direction.Value.normalized,
            FloatData = Speed.Value
        };

        bool success = manager.TryAddCommand(payload);
        return success ? Status.Success : Status.Failure;
    }
}

