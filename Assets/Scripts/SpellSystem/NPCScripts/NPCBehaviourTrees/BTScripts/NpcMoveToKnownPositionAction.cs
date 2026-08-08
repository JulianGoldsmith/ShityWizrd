using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NPCMoveToKnownPosition", story: "[Self] Moves To [Position] for [MaxSearchTime]", category: "Action", id: "f6d438729b6278d5a957211d31479cf2")]
public partial class NpcMoveToKnownPositionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> Position;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<float> MaxSearchTime;
    [SerializeReference] public BlackboardVariable<float> ReachedTargetDistanceThreshold;

    private NPCMovementController controller;
    private Vector3 destination;
    private float searchEndTime;
    private bool hasReachedDestination;

    protected override Status OnStart()
    {
        controller = Self.Value.GetComponent<NPCMovementController>();
        destination = Position.Value;
        hasReachedDestination = false;

        if (destination != Vector3.zero)
        {
            //Debug.Log("Destination was 0 for NPC");
            searchEndTime = Time.time + MaxSearchTime;
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
            if (!controller.agent.pathPending && controller.agent.remainingDistance <= ReachedTargetDistanceThreshold)
            {
                hasReachedDestination = true;
                return Status.Success;
            }
        }

        if(Time.time > searchEndTime)
        {
            //Debug.Log("SearchTime over for NPC");
            return Status.Failure;
        }

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

