using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "BTIsCastingCheck", story: "[Self] is [IsCasting] Casting", category: "Conditions", id: "1d9c27233002e41ff293a56e2dfecab5")]
public partial class BtIsCastingCheckCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<bool> IsCasting;

    public override bool IsTrue()
    {
        if (Self.Value == null) return false;

        var actionManager = Self.Value.GetComponent<NPCActionManager>();
        if (actionManager == null) return false;

        return actionManager.isCasting == IsCasting.Value;
    }
}
