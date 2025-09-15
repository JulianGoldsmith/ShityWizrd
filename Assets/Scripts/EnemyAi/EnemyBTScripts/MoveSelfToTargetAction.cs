using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "MoveSelfToTarget", story: "Move [Self] to [Target]", category: "Action", id: "03bb1a35d49ee911b24cecb9bb65f124")]
public partial class MoveSelfToTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    private EnemyMovement controller;

    protected override Status OnStart()
    {
        if (Self.Value != null)
        {
            controller = Self.Value.GetComponent<EnemyMovement>();
        }
        return controller != null ? Status.Running : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        if (controller == null || Target.Value == null)
        {
            return Status.Failure;
        }

        Vector3 targetPosition = Target.Value.transform.position;

        controller.MoveToPoint(targetPosition, controller.runSpeed);
        controller.RotateInMovementDirection();

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

