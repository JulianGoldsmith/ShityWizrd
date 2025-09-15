using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.AI;
using static UnityEngine.GraphicsBuffer;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "CombatRepositionAction", story: "[Self] positions for combat with [Target]", category: "Action", id: "5d86949529fa901d98d562aa4655cfd6")]
public partial class CombatRepositionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    public float validPositionSearchRadius = 3f;

    private EnemyMovement movementController;
    private EnemyCombat combatController;

    private bool isMovingToPosition;
    private Vector3 currentDestination;

    private float nextRepositionTime;
    public float strafeDirection;
    public float strafeDistance; 

    protected override Status OnStart()
    {
        movementController = Self.Value.GetComponent<EnemyMovement>();
        combatController = Self.Value.GetComponent<EnemyCombat>();
        if (movementController == null || combatController == null)
        {
            Debug.Log("Need to attach movementController and combatController to use CombatRepositionAction");
            return Status.Failure;
        }
        isMovingToPosition = false;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Target.Value == null)
        {
            return Status.Failure;
        }

        if (Time.time > nextRepositionTime)
        {
            PickNewRelativeGoal(); 
        }

        bool wasSuccessful = movementController.ExecuteCombatOrbit(Target.Value, strafeDistance, strafeDirection);

        if (!wasSuccessful)
        {
            strafeDirection *= -1;
        }

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

    private void PickNewRelativeGoal()
    {

        strafeDistance = UnityEngine.Random.Range(combatController.idealMinDistance, combatController.idealMaxDistance);

        strafeDirection = (UnityEngine.Random.value > 0.5f) ? 1f: -1f;

        float interval = UnityEngine.Random.Range(combatController.minRepositionTime, combatController.maxRepositionTime);
        nextRepositionTime = Time.time + interval;
    }
}

