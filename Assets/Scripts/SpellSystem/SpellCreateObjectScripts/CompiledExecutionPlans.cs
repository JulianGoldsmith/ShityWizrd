using System.Collections.Generic;

public class TriggerExecutioPlan
{
    // The list of one-shot effects (Damage, Spawn Child Core) to run when the trigger fires
    public List<IEffect> EffectsToRun = new List<IEffect>();

    // Does the core destroy itself after this trigger fires? (e.g., Fireball hitting a wall)
    public bool DestroysCore;
}

public class CoreExecutionPlan
{
    // The continuous math and checks the Core needs to run every tick
    public List<IBehaviour> Behaviours = new List<IBehaviour>();
    public List<ITrigger> Triggers = new List<ITrigger>();
}