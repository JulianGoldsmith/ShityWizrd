using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "RushTowardTargetAction", story: "[Self] Rushes towards [Target]", category: "Action", id: "1c31babde5c43fcda572eec48ed8d00b")]
public partial class RushTowardTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<bool> IsRushing;

    private EnemyMovement movementController;
    private EnemyCombat combatController;

    protected override Status OnStart()
    {
        movementController = Self.Value.GetComponent<EnemyMovement>();
        combatController = Self.Value.GetComponent<EnemyCombat>();

        if (movementController == null || combatController == null)
        {
            return Status.Failure;
        }

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Target.Value == null)
        {
            return Status.Failure;
        }

        float distanceToTarget = Vector3.Distance(Self.Value.transform.position, Target.Value.transform.position);

        if (distanceToTarget <= combatController.attackRange)
        {
            IsRushing.Value = false;
            movementController.StopMovement();
            Debug.Log("Rushed");
            return Status.Success;
        }

        movementController.MoveToPoint(Target.Value.transform.position, movementController.runSpeed);
        movementController.RotateTowardsPoint(Target.Value.transform.position);

        return Status.Running;
    }

    protected override void OnEnd()
    {
        if (movementController != null)
        {
            movementController.StopMovement();
        }
    }
}

