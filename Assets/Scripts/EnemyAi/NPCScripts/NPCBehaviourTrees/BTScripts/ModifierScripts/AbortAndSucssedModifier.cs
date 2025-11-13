using System;
using Unity.Behavior;
using UnityEngine;
using Modifier = Unity.Behavior.Modifier;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "AbortAndSucssed", story: "Abort if with sucsess", category: "Flow", id: "1930276260f3da0755cb3e4d557ebc6d")]
public partial class AbortAndSucssedModifier : Modifier
{

    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

