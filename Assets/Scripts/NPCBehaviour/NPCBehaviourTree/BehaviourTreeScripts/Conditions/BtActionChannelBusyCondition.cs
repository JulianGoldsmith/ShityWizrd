using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "BTActionChannelBusy", story: "[Self] action channel busy is [ExpectedBusy]", category: "Conditions", id: "60e2c8569607e81e570f784e93a86569")]
public partial class BtActionChannelBusyCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<bool> ExpectedBusy;

    public override bool IsTrue()
    {
        if (Self.Value == null) return false;

        NPCActionManager actionManager = Self.Value.GetComponent<NPCActionManager>();
        if (actionManager == null) return false;

        return actionManager.IsActionChannelBusy == ExpectedBusy.Value;
    }
}
