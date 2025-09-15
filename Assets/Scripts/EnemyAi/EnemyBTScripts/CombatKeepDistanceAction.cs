using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "CombatKeepDistanceAction", story: "[Self] keeps combat distance from [Target]", category: "Action", id: "ee3fa682cd2b26c655d8b668a2fc1705")]
public partial class CombatKeepDistanceAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    public float validPositionSearchRadius = 2f;

    private EnemyMovement movementController;
    private EnemyCombat combatController;

    protected override Status OnStart()
    {
        movementController = Self.Value.GetComponent<EnemyMovement>();
        combatController = Self.Value.GetComponent<EnemyCombat>();

        return (movementController != null && combatController != null) ? Status.Running : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        if (Target.Value == null)
        {
            return Status.Failure;
        }

        Vector3 selfPosition = Self.Value.transform.position;
        Vector3 targetPosition = Target.Value.transform.position;
        float currentDistance = Vector3.Distance(selfPosition, targetPosition);

        Vector3 destination = selfPosition;

        if (currentDistance < combatController.idealMinDistance)
        {
            Vector3 directionAwayFromTarget = (selfPosition - targetPosition).normalized;
            destination = selfPosition + directionAwayFromTarget * 2f;
        }
        else if (currentDistance > combatController.idealMaxDistance)
        {
            destination = targetPosition;
        }
        else
        {
            movementController.StopMovement();
            movementController.RotateTowardsPoint(targetPosition);
            return Status.Failure;
        }

 
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, validPositionSearchRadius, NavMesh.AllAreas))
        {
            movementController.MoveToPoint(hit.position, movementController.walkSpeed);
            movementController.RotateTowardsPoint(targetPosition);
        }

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

