using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "UpdateRushTimerAction", story: "[Self] Manages [IsRushing] timer", category: "Action", id: "4915e60be0ba759e19d1753558dd43c8")]
public partial class UpdateRushTimerAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<bool> IsRushing;

    private EnemyCombat combatController;
    private float rushTimer = 0f;

    protected override Status OnStart()
    {
        combatController = Self.Value.GetComponent<EnemyCombat>();
        if (combatController == null) return Status.Failure;

        ResetTimer();
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (IsRushing.Value)
        {
            return Status.Running;
        }

        rushTimer -= Time.deltaTime;
        
        if (rushTimer <= 0)
        {

            IsRushing.Value = true;
            Debug.Log($"Rushing triggered {IsRushing.Value}");
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {

    }

    private void ResetTimer()
    {
       // Debug.Log("Reset Rush Timer");
        rushTimer = UnityEngine.Random.Range(combatController.rushdownCooldownMin, combatController.rushdownCooldownMax);
    }
}

