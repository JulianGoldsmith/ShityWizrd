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

    }
}

public class AccelerateBehaviour : IBehaviour
{
    public RuntimeFloatProperty Acceleration;

    public void InitTick(ISpellExecutionCore core) { }

    public void Tick(ISpellExecutionCore core, float deltaTime)
    {
        if (core.TryGetCoreComponent<IMovementHandler>(out var mover))
        {
            if (mover.CurrentVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 direction = mover.CurrentVelocity.normalized;

                mover.ApplyForce(direction * Acceleration.GetValue(default), ForceMode.Acceleration);
            }
        }
    }

    public void CleanupVFX(ISpellExecutionCore core) { }

    public void TickVFX(ISpellExecutionCore core) { }
}
