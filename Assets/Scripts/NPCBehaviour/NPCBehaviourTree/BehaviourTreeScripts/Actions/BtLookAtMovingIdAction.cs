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

        var manager = Self.Value.GetComponent<NPCBehaviourManager>();
        var targetNetworkObj = Target.Value.GetComponent<NetworkObject>();

        //Debug.Log($"BT set command to look at {targetNetworkObj} + ID: {targetNetworkObj.Id}");
        if(targetNetworkObj.TryGetComponent<IHasPhysicalCore>(out var core))
        {
            targetNetworkObj = core.GetCoreNetworkObject();
            Debug.Log($"NPC BT issued command to look an object with a core - replaced core ");
        }

        Debug.Log($"NPC BT issued command to look at {targetNetworkObj.gameObject.name}");

        if (manager == null || targetNetworkObj == null || manager.Runner == null) return Status.Failure;

        int targetStartTick = manager.GlobalClearTick > manager.Runner.Tick
            ? manager.GlobalClearTick
            : manager.Runner.Tick;

        NPCCommandData payload = new NPCCommandData
        {
            CommandID = CommandType.Look_AtID,
            Priority = 10,
            SetTick = manager.Runner.Tick,         // The moment the BT decided to do this
            StartTick = targetStartTick,           // The moment the muscle should activate
            EndTick = targetStartTick + 999999,    // Run indefinitely until the next state Clear
            TargetID = targetNetworkObj.Id,        // Pass the target's NetworkID
                   // Pass the movement speed
        };
        
        bool success = manager.TryAddCommand(payload);

        return success ? Status.Success : Status.Failure;
    }

}

