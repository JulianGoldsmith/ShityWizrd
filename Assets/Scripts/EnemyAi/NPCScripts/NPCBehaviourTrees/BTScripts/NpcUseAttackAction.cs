using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NPCUseAttack", story: "[Self] uses attack [NPCActionID]", category: "Action", id: "e5bd0ee028609251c52bbe0f690101cd")]
public partial class NpcUseAttackAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<int> NPCActionID;


    private NPCActionController actionController;
    private NPCAction actionToUse;
    private float durationToHold, timeToRelease;

    protected override Status OnStart()
    {
        actionController = Self.Value.GetComponent<NPCActionController>();

        if(actionController == null) return Status.Failure;

        actionToUse = actionController.actions[NPCActionID];

        if(actionToUse == null) return Status.Failure;

        //Debug.Log($"NPC called to perform aciton called and action is spell");
        actionController.StartCast(NPCActionID);

        durationToHold = actionToUse.holdDuration;

        timeToRelease = Time.time + durationToHold;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if(timeToRelease <= Time.time)
        {

            return Status.Success;
        }
        //actionController.UpdateActiveCasts();
        return Status.Running;
    }

    protected override void OnEnd()
    {
        actionController.EndCast();
    }
}

