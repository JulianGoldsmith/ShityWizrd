using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Stagger", story: "Agent is Staggered and waits for x seconds", category: "Action", id: "55baf3e5bc00f67876d1901dc26bd5ca")]
public partial class StaggerAction : Action
{


    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [SerializeReference] public BlackboardVariable<bool> IsStaggered;

    [SerializeReference] public BlackboardVariable<string> staggerAnimTriggerName = new ("StaggerTrigger");

    [SerializeReference] public BlackboardVariable<float> StaggerDuration = new(1.5f);

    private Animator animator;
    private float startTime;

    protected override Status OnStart()
    {
        animator = Self.Value.GetComponent<Animator>();
        if (animator == null || string.IsNullOrEmpty(staggerAnimTriggerName.Value))
        {
            return Status.Failure;
        }

        animator.SetTrigger(staggerAnimTriggerName.Value);
        startTime = Time.time;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (Time.time - startTime >= StaggerDuration.Value)
        {
            IsStaggered.Value = false;

            return Status.Success;
        }

        return Status.Running;
    }
}