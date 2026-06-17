using UnityEngine;
using System;

[CreateAssetMenu(fileName = "AccelerateNode", menuName = "SpellNodes/Behaviour/AccelerateNode")]
public class AccelerateNode : BehaviourNode
{
    [Promotable("Acceleration", DataTypeTag.Speed)]
    public float acceleration;

    public override IRuntimeNode CompileNode(SpellCompilationContext sCC)
    {

        return new AccelerateBehaviour()
        {
            Acceleration = new RuntimeFloatProperty(this.acceleration)
        };
    }

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        /*var accel = spellCore.AddComponent<AccelerateSB>();
        accel.triggerInfo = triggerInfo;
        accel.acceleration = acceleration;

        accel.OnAttach(this);*/
    }
}

public class AccelerateBehaviour : IBehaviour
{
    public RuntimeFloatProperty Acceleration;

    public void InitTick(SpellCreatedCore core)
    {
    }

    public void Tick(SpellCreatedCore core, float deltaTime)
    {
        if (core.TryGetComponent<IMovementHandler>(out var mover))
        {
            // Use your existing CurrentVelocity property
            if (mover.CurrentVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 direction = mover.CurrentVelocity.normalized;

                // Route the force directly to the handler
                mover.ApplyForce(direction * Acceleration.GetValue(default), ForceMode.Acceleration);
            }
        }
    }
    public void CleanupVFX(SpellCreatedCore core)
    {
    }
    public void TickVFX(SpellCreatedCore core)
    {
    }
}
