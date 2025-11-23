using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NPCPickRandomPointInRadius", story: "[Self] sets [Point] to random point within [Radius] of [Center]", category: "Action", id: "279c4e109376854fc3edb44195740cfc")]
public partial class NpcPickRandomPointInRadiusAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> Point;
    [SerializeReference] public BlackboardVariable<float> Radius;
    [SerializeReference] public BlackboardVariable<Vector3> Center;

    private NPCMovementController controller;
    private NavMeshAgent agent;
    private const int maxPointFindingAttepts = 50;
    private const float samplePositionMaxDistance = 2.0f;

    protected override Status OnStart()
    {
        if(Self.Value == null || Point.Value ==null || Center.Value == null ) return Status.Failure;

        controller = Self.Value.GetComponent<NPCMovementController>();
        agent = controller.agent;

        if (controller == null || agent == null) return Status.Failure;

        float radius = Radius.Value;
        Vector3 center = Center.Value;

        for (int i = 0; i < maxPointFindingAttepts; i++)
        {
            Vector3 randomPointInSphere = center + (UnityEngine.Random.insideUnitSphere * radius);

            NavMeshHit hit;

            if (NavMesh.SamplePosition(randomPointInSphere, out hit, samplePositionMaxDistance, agent.areaMask))
            {
                Point.Value = hit.position;
                return Status.Success; // Return success immediately
            }
        }

        Debug.LogWarning($"NpcPickRandomPointInRadiusAction: Failed to find random NavMesh point after {maxPointFindingAttepts} attempts.");
        return Status.Failure;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

