using System;
using System.Linq;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "CastItemAction", story: "[Self] casts item spell", category: "Action", id: "8333af0c16c22a59231098a65d11078d")]
public partial class CastItemAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<bool> IsCasting;

    private EnemyMovement movementController;
    private EnemyCastActionController castController;
    private EnemyCombat combatController;

    private float targetChargeTime;
    private AttackPhase currentPhase; 
    private enum AttackPhase { Charging, Releasing }

    protected override Status OnStart()
    {
        movementController = Self.Value.GetComponent<EnemyMovement>();
        castController = Self.Value.GetComponent<EnemyCastActionController>();
        combatController = Self.Value.GetComponent<EnemyCombat>();

        if (movementController == null || castController == null || combatController == null)
        {
            return Status.Failure;
        }

        movementController.StopMovement();
        castController.StartCast(false);

        //var held = castController.activeCasts?.FirstOrDefault(c => c.isHeld);

        targetChargeTime = Time.time +  0.1f; // for now, this will need changing to properly use the charge time
        currentPhase = AttackPhase.Charging;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Target.Value == null) return Status.Failure;

        movementController.RotateTowardsPoint(Target.Value.transform.position);


        if (currentPhase == AttackPhase.Charging)
        {
            if (Time.time >= targetChargeTime)
            {
                castController.EndCast();
                currentPhase = AttackPhase.Releasing;
            }
        }
        else if (currentPhase == AttackPhase.Releasing)
        {

            if (!IsCasting.Value)
            {
                return Status.Success;
            }
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {

        bool isAnyCastHeld = false;
        foreach (var cast in castController.activeCasts)
        {
            if (cast.isHeld)
            {
                isAnyCastHeld = true;
                break; 
            }
        }
        if (isAnyCastHeld)
        {
            castController.EndCast();
        }
    }
}

