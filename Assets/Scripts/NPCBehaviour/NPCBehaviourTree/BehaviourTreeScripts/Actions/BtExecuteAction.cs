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

        NPCBehaviourManager manager = Self.Value.GetComponent<NPCBehaviourManager>();
        NPCActionManager actionManager = Self.Value.GetComponent<NPCActionManager>();
        NetworkObject targetNetworkObj = Target.Value.GetComponentInParent<NetworkObject>();

        if (manager == null || actionManager == null || targetNetworkObj == null || manager.Runner == null) return Status.Failure;

        int targetStartTick = manager.GetCurrentIntentStartTick();

        bool success = actionManager.TryScheduleAction(ActionID.Value, targetNetworkObj.Id, targetStartTick);

        return success ? Status.Success : Status.Failure;
    }
}

