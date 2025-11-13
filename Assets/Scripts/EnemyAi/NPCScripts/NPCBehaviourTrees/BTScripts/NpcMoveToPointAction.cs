using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NPCMoveToPoint", story: "[Self] moves to [Point] [running] and faces target [FacesTarget]", category: "Action", id: "9ec5e4d11b7415d6052e0f80ddf6b67b")]
public partial class NpcMoveToPointAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> Point;
    [SerializeReference] public BlackboardVariable<bool> Running;
    [SerializeReference] public BlackboardVariable<bool> FacesTarget;
    private NPCMovementController controller;

    protected override Status OnStart()
    {
        if (Self.Value != null)
        {
            //Debug.Log($"Failure check  self");
            controller = Self.Value.GetComponent<NPCMovementController>();
        }
        return controller != null ? Status.Running : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        if (controller == null || Self.Value == null)
        {
            // Debug.Log($"Failure check  {( Target.Value == null? "target" : "controller" )}");
            return Status.Failure;
        }

        Vector3 targetPosition = Point.Value;

        controller.MoveToPoint(targetPosition, Running ? 2 : 1);
        //controller.RotateInMovementDirection();
        if(!FacesTarget)
            controller.RotateInMovementDirection();
        else
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

