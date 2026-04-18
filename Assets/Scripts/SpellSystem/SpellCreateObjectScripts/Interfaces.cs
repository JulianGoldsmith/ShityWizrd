using System.Collections.Generic;

public interface IBehaviour
{
    // Runs exactly once on the first tick the core is pulled from the buffer
    void InitTick(SpellCreatedCore core);

    // Runs every tick to apply continuous math (Movement, Gravity)
    void Tick(SpellCreatedCore core, float deltaTime);

    void TickVFX(SpellCreatedCore core);
    void CleanupVFX(SpellCreatedCore core);
}

public interface ITrigger
{
    // The specific plan to execute if this trigger returns true
    TriggerExecutioPlan Plan { get; set; }

    // Runs exactly once on the first tick
    void InitTick(SpellCreatedCore core);

    // Evaluates a condition. If true, outputs the HitInfo so the Effects know what happened!
    bool Tick(SpellCreatedCore core, float deltaTime, out List<SpellTriggerInfo> hitInfos);

    void TickVFX(SpellCreatedCore core);
    void CleanupVFX(SpellCreatedCore core);
}

public interface IEffect
{
    // Runs instantly when a trigger fires. Takes the HitInfo from the trigger.
    void Execute(SpellCreatedCore core, List<SpellTriggerInfo> hitInfos);


}