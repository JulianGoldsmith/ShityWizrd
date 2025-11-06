using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NPCMoveToKnownPosition", story: "[Self] Moves To [TargetLastSeenPosition]", category: "Action", id: "f6d438729b6278d5a957211d31479cf2")]
public partial class NpcMoveToKnownPositionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> TargetLastSeenPosition;
    [SerializeReference] public BlackboardVariable<Vector3> Position;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> SearchTime;

    private NPCMovementController controller;
    private Vector3 destination;
    private float searchStartTime;
    private bool hasReachedDestination;

    protected override Status OnStart()
    {
        controller = Self.Value.GetComponent<NPCMovementController>();
        destination = Position.Value;
        hasReachedDestination = false;

        if (destination != Vector3.zero)
        {

            return Status.Running;
        }
        return Status.Failure;
    }

    protected override Status OnUpdate()
    {
        if (Target.Value != null)
        {
            return Status.Failure;
        }

        if (!hasReachedDestination)
        {
            controller.MoveToPoint(destination, 1);
            controller.RotateInMovementDirection();
            if (!controller.agent.pathPending && controller.agent.remainingDistance <= controller.agent.stoppingDistance)
            {
                hasReachedDestination = true;
                searchStartTime = Time.time;
            }
        }
        else
        {
            if (Time.time - searchStartTime >= SearchTime)
            {
                return Status.Success;
            }
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (CurrentStatus == Status.Success)
        {
            if (Position.Value != Vector3.zero)
            {
                Position.Value = Vector3.zero;
            }
        }
        if (controller != null)
        {
            controller.StopMovement();
        }
    }
}

