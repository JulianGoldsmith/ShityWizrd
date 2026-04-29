using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Clear_Commands", story: "[NPC] clears Commands at tick: [Tick]", category: "Action", id: "6c8e56ed3d6aaad2cab1988f7a43468c")]
public partial class ClearCommands : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> NPC;
    [SerializeReference] public BlackboardVariable<int> Tick;
    protected override Status OnStart()
    {
        if (NPC.Value == null) return Status.Failure;
       // Debug.Log(NPC.Value.name.ToString());
        var manager = NPC.Value.GetComponent<NPCBehaviourManager>();
        if (manager == null) return Status.Failure;

        manager.GlobalClearTick = manager.Runner.Tick;

        return Status.Success;
    }
}

