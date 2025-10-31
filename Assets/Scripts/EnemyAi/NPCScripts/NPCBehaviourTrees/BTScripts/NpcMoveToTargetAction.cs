using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NPCMoveToTarget", story: "[Self] Moves To [Target]", category: "Action", id: "5789e974e4f15bdd5de04ed89ce6bd58")]
public partial class NpcMoveToTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    private NPCMovementController controller;

    protected override Status OnStart()
    {
        if (Self.Value != null)
        {
            Debug.Log($"Failure check  self");
            controller = Self.Value.GetComponent<NPCMovementController>();
        }
        return controller != null ? Status.Running : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        if (controller == null || Target.Value == null)
        {
            Debug.Log($"Failure check  {( Target.Value == null? "target" : "controller" )}");
            return Status.Failure;
        }

        Vector3 targetPosition = Target.Value.transform.position;

        controller.MoveToPoint(targetPosition, 1);
        //controller.RotateInMovementDirection();
        controller.RotateTowardsPoint(targetPosition);

        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (controller != null)
        {
            controller.StopMovement();
        }
    }
}

