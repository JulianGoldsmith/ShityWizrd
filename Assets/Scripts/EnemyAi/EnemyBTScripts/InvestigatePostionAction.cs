using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityEngine.GraphicsBuffer;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "InvestigatePostion", story: "[Self] investigates [position]", category: "Action", id: "6a012344a6af2a0f6c22bbc94643f3ad")]
public partial class InvestigatePostionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> Position;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> SearchTime;

    private EnemyMovement controller;
    private Vector3 destination;
    private float searchStartTime;
    private bool hasReachedDestination;

    protected override Status OnStart()
    {
        controller = Self.Value.GetComponent<EnemyMovement>();
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
            controller.MoveToPoint(destination, controller.walkSpeed);
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

