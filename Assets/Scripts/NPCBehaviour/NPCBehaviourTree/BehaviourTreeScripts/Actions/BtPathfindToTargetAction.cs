using Fusion;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BT_PathfindToTarget", story: "[Self] paths to target: [Target] mode: [MovementMode]", category: "Action", id: "ec6506e0cf16c217626b6b74d28687ad")]
public partial class BtPathfindToTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<NPCMovementMode> MovementMode;
    protected override Status OnStart()
    {
        if (Self.Value == null || Target.Value == null) return Status.Failure;

        NPCBehaviourManager manager = Self.Value.GetComponent<NPCBehaviourManager>();
        NetworkObject targetNetworkObj = Target.Value.GetComponentInParent<NetworkObject>();

        if (manager == null || targetNetworkObj == null || manager.Runner == null) return Status.Failure;

        int targetStartTick = manager.GetCurrentIntentStartTick();
        int revision = manager.BeginCommandRevision();

        if (revision == 0) return Status.Failure;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Move_PathfindToID,
            Priority = 10,
            EndTick = targetStartTick + 999999,
            TargetID = targetNetworkObj.Id,
            MovementMode = MovementMode.Value
        };

        bool success = manager.TryScheduleChannelCommand(payload, targetStartTick, revision);
        return success ? Status.Success : Status.Failure;
    }
}

