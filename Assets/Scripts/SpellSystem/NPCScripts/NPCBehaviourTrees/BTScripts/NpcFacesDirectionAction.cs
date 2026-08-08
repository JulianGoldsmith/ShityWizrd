using System;
using System.Drawing;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NPCFacesDirection", story: "[Self] Faces [Direction]", category: "Action", id: "dff7d4d5252601a8caca740c4fbb02c0")]
public partial class NpcFacesDirectionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> Direction;
    [SerializeField] public float acceptanceAngle = 10f;

    private NPCMovementController movementController;
    private NPCActiveRagdollController ragdollController;

    protected override Status OnStart()
    {
        if (Self.Value != null)
        {
            movementController = Self.Value.GetComponent<NPCMovementController>();
            ragdollController = movementController.controller;
        }
        return movementController != null ? Status.Running : Status.Failure;
    }

    protected override Status OnUpdate()
    {
        if (movementController == null || Self.Value == null)
        {
            return Status.Failure;
        }
        Vector3 targetDir = Direction.Value;
        targetDir.y = 0;
        movementController.RotateInDirection(targetDir);

        Vector3 currentDir = ragdollController.coreRB.transform.forward;
        currentDir.y = 0;
        currentDir.Normalize();

        float angle = Vector3.Angle(currentDir, targetDir);

        if (angle < acceptanceAngle)
        {
            return Status.Success;
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
    
    }
}

