using Fusion;
using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "BtLookAtMovingID", story: "[Self] looks at [Target]", category: "Action", id: "b2252ccd5a7151cffac4bd8bb3c97ea9")]
public partial class BtLookAtMovingIdAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    protected override Status OnStart()
    {
        if (Self.Value == null || Target.Value == null) return Status.Failure;

        NPCBehaviourManager manager = Self.Value.GetComponent<NPCBehaviourManager>();
        NetworkObject targetNetworkObj = Target.Value.GetComponentInParent<NetworkObject>();

        if (manager == null || targetNetworkObj == null || manager.Runner == null) return Status.Failure;

        //Debug.Log($"BT set command to look at {targetNetworkObj} + ID: {targetNetworkObj.Id}");
        if (targetNetworkObj.TryGetComponent<IHasPhysicalCore>(out IHasPhysicalCore core))
        {
            targetNetworkObj = core.GetCoreNetworkObject();
            Debug.Log($"NPC BT issued command to look an object with a core - replaced core ");
        }

        if (targetNetworkObj == null) return Status.Failure;

        Debug.Log($"NPC BT issued command to look at {targetNetworkObj.gameObject.name}");

        int targetStartTick = manager.GetCurrentIntentStartTick();
        int revision = manager.BeginCommandRevision();

        if (revision == 0) return Status.Failure;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Look_AtID,
            Priority = 10,
            EndTick = targetStartTick + 999999,
            TargetID = targetNetworkObj.Id
        };

        bool success = manager.TryScheduleChannelCommand(payload, targetStartTick, revision);

        return success ? Status.Success : Status.Failure;
    }
}

