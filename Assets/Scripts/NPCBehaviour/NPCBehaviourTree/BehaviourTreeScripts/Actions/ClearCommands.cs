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

        NPCBehaviourManager manager = NPC.Value.GetComponent<NPCBehaviourManager>();
        if (manager == null || manager.Runner == null) return Status.Failure;

        int targetStartTick = manager.Runner.Tick + Mathf.Max(0, Tick.Value);
        int revision = manager.BeginCommandRevision();

        if (revision == 0) return Status.Failure;

        bool success = manager.TryScheduleAllChannelClears(targetStartTick, revision);
        if (!success) return Status.Failure;

        if (manager.actionManager != null) manager.actionManager.TryCancelPendingAction();

        manager.SetCurrentIntentStartTick(targetStartTick);
        return Status.Success;
    }
}

