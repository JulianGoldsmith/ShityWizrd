using System;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;
using Action = Unity.Behavior.Action;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NPCSetHomePoint", story: "[Self] sets [HomeLocation]", category: "Action", id: "e4ca69feb6900cabb5d95394db09661e")]
public partial class NpcSetHomePointAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> HomeLocation;

    protected override Status OnStart()
    {
        NPCActiveRagdollController core = Self.Value.GetComponent<NPCActiveRagdollController>();
        if (core == null) return Status.Failure;

        HomeLocation.Value = core.coreRB.transform.position;
        return Status.Success;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

