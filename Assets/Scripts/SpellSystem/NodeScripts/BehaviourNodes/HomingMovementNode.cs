using UnityEngine;
using System;

[CreateAssetMenu(fileName = "HomingMovementNode", menuName = "SpellNodes/Behaviour/HomingMovementNode")]
public class HomingMovementNode : BehaviourNode
{
    [Promotable("Turn Rate Speed", DataTypeTag.Speed)]
    public float turn_rate = 1.0f;
    public bool maintain_target = true; // keep same target until out of range.
    public float search_range = 5.0f;
    public float min_speed = 1.0f;

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        var homingMovement = spellCore.AddComponent<HomingMovementSB>();
        homingMovement.triggerInfo = triggerInfo;
        homingMovement.turn_rate = turn_rate;
        homingMovement.maintain_target = maintain_target;
        homingMovement.search_range = search_range;
        homingMovement.min_speed = min_speed;

        homingMovement.OnAttach(this);
    }
}
