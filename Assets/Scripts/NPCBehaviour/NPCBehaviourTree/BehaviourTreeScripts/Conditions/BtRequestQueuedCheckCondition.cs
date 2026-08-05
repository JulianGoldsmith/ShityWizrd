using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "BtRequestQueuedCheck", story: "[Self] has [Command] queued [IsQueued]", category: "Conditions", id: "774c3a956f12811ac984b1db11586e34")]
public partial class BtRequestQueuedCheckCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<CommandType> Command;
    [SerializeReference] public BlackboardVariable<bool> IsQueued;

    public override bool IsTrue()
    {
        if (Self.Value == null) return false;

        NPCBehaviourManager behaviourManager = Self.Value.GetComponent<NPCBehaviourManager>();
        if (behaviourManager == null) return false;

        bool actualQueueState = behaviourManager.IsRequestQueuedAndWaiting(Command.Value);
        return actualQueueState == IsQueued.Value;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
