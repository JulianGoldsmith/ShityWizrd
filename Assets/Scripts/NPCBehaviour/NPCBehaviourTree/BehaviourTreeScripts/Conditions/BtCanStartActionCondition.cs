using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "BTCanStartAction", story: "[Self] can start Action ID [ActionID]", category: "Conditions", id: "aee0ea0e6f15f05a377f8d4524869ff8")]
public partial class BtCanStartActionCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<int> ActionID;

    public override bool IsTrue()
    {
        if (Self.Value == null) return false;

        NPCActionManager actionManager = Self.Value.GetComponent<NPCActionManager>();
        if (actionManager == null) return false;

        return actionManager.CanStartAction(ActionID.Value);
    }
}
