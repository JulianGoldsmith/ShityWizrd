using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "BtCommandQueuedCheck", story: "[Self] has [Command] Queued [IsQueued]", category: "Conditions", id: "774c3a956f12811ac984b1db11586e34")]
public partial class BtCommandQueuedCheckCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<CommandType> Command;
    [SerializeReference] public BlackboardVariable<bool> IsQueued;
    public override bool IsTrue()
    {
        if (Self.Value == null) return false;

        var behaviourManager = Self.Value.GetComponent<NPCBehaviourManager>();
        if (behaviourManager == null) return false;

        bool actualQueueState = behaviourManager.IsCommandQueuedAndWaiting(Command.Value);

        return actualQueueState == IsQueued.Value;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
