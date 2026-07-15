using System.Collections.Generic;
using UnityEngine;

// --- 1. THE UMBRELLA (For the Hydrator) ---
// This allows the Hydrator to hold everything in one temporary array while wiring
public interface IRuntimeNode { }


// --- 2. THE CORE (The Container) ---
// We add this because Cores need to accept the Triggers and Behaviours
public interface ISpellExecutionCore
{
    ActiveCastID ActiveCastID { get; }
    CoreContext Context { get; set; }

    UnityEngine.GameObject SourceObject { get; }
    UnityEngine.Vector3 Position { get; }
    UnityEngine.Quaternion Rotation { get; }
    Fusion.NetworkRunner Runner { get; }
    Fusion.PlayerRef InputAuthority { get; }
    Fusion.NetworkId CoreNetworkId { get; }
    Dictionary<int, GameObject> ActiveVisuals { get; }
    bool TryGetCoreComponent<T>(out T component) where T : class;

    // Memory Sketchpads
    bool GetBool(int bitIndex);
    void SetBool(int bitIndex, bool value);
    int GetInt(int index);
    void SetInt(int index, int value);
    float GetFloat(int index);
    void SetFloat(int index, float value);
    UnityEngine.Vector3 GetVector(int index);
    void SetVector(int index, UnityEngine.Vector3 value);
}


// --- 3. THE CORE (The Container) ---
public interface IRuntimeCore : IRuntimeNode
{
    void AddTrigger(ITrigger trigger);
    void AddBehaviour(IBehaviour behaviour);
    void ExecuteCore(SpellTriggerInfo info);
}


// --- 4. THE BEHAVIOURS (Passive Modifiers) ---
public interface IBehaviour : IRuntimeNode
{
    // UPDATED: Now accepts the abstraction!
    void InitTick(ISpellExecutionCore core);
    void Tick(ISpellExecutionCore core, float deltaTime);
    void TickVFX(ISpellExecutionCore core);
    void CleanupVFX(ISpellExecutionCore core);
}


// --- 5. THE TRIGGERS (Event Listeners) ---
public interface ITrigger : IRuntimeNode
{
    void AddOutcome(IRuntimeNode outcome);

    // UPDATED: Now accepts the abstraction!
    void InitTick(ISpellExecutionCore core);
    bool Tick(ISpellExecutionCore core, float deltaTime, out System.Collections.Generic.List<SpellTriggerInfo> hitInfos);
    void TickVFX(ISpellExecutionCore core);
    void CleanupVFX(ISpellExecutionCore core);
}


// --- 5. THE EFFECTS (Instant Actions) ---
public interface IEffect : IRuntimeNode
{
    // Runs instantly when a trigger fires. Takes the HitInfo from the trigger.
    void Execute(ISpellExecutionCore core, List<SpellTriggerInfo> hitInfos);
}

public abstract class RuntimeCoreBase : IRuntimeCore
{
    // The shared lists! No more duplication.
    public List<IBehaviour> Behaviours { get; protected set; } = new List<IBehaviour>();
    public List<ITrigger> Triggers { get; protected set; } = new List<ITrigger>();

    // The shared plumbing!
    public void AddBehaviour(IBehaviour behaviour) => Behaviours.Add(behaviour);
    public void AddTrigger(ITrigger trigger) => Triggers.Add(trigger);

    // The unique logic each core must still define
    public abstract void ExecuteCore(SpellTriggerInfo info);
}

public abstract class RuntimeTriggerBase : ITrigger
{
    // The shared list of downstream effects/cores
    public List<IRuntimeNode> Outcomes { get; protected set; } = new List<IRuntimeNode>();

    // The shared plumbing!
    public void AddOutcome(IRuntimeNode outcome) => Outcomes.Add(outcome);

    // The unique logic each trigger must still define
    public abstract void InitTick(ISpellExecutionCore core);
    public abstract bool Tick(ISpellExecutionCore core, float deltaTime, out List<SpellTriggerInfo> hitInfos);
    public abstract void TickVFX(ISpellExecutionCore core);
    public abstract void CleanupVFX(ISpellExecutionCore core);

    // (Note: You can likely delete TriggerExecutionPlan entirely now, 
    // as the 'Outcomes' list perfectly replaces it!)
}

public class RuntimeEntryPoint : IRuntimeNode
{
    public EntryPointType ExpectedType;
    public IRuntimeNode ConnectedLogic; // The Core, Trigger, or Effect plugged into it

    // The Hydrator uses this to snap the next node into the Entry Point
    public void SetConnection(IRuntimeNode target)
    {
        ConnectedLogic = target;
    }

    // The Spark!
    public void Execute(SpellTriggerInfo info)
    {
        if (ConnectedLogic == null) return;

        // Route the execution based on what is plugged in
        if (ConnectedLogic is IRuntimeCore core) core.ExecuteCore(info);
        else if (ConnectedLogic is ITrigger trigger) trigger.InitTick(null); // (We will refine virtual core ticking later)
        else if (ConnectedLogic is IEffect effect) effect.Execute(null, new List<SpellTriggerInfo> { info });
    }
}

public interface IRuntimeValueNode : IRuntimeNode { }

public interface IRuntimeValueNode<T> : IRuntimeValueNode
{
    // Evaluates instantly during combat!
    ValueModifier<T> GetModifier(SpellTriggerInfo info);
}

// Allows the Hydrator to blindly inject nodes into ANY variable type
public interface IRuntimeDataProperty
{
    void AddValueNode(IRuntimeValueNode node);
}

// THE WRAPPER: Replaces standard 'float' for promotable variables!
public class RuntimeFloatProperty : IRuntimeDataProperty
{
    public float BaseValue;
    public List<IRuntimeValueNode<float>> Modifiers;

    public RuntimeFloatProperty(float baseValue) => BaseValue = baseValue;

    public void AddValueNode(IRuntimeValueNode node)
    {
        if (node is IRuntimeValueNode<float> floatMod)
        {
            if (Modifiers == null) Modifiers = new List<IRuntimeValueNode<float>>();
            Modifiers.Add(floatMod);
        }
    }

    public float GetValue(SpellTriggerInfo info)
    {
        if (Modifiers == null) return BaseValue; // No wires? Just return the base instantly!

        float finalValue = BaseValue;
        float multiplyAgg = 1f;

        // Apply all dynamic math plugged in by the player!
        foreach (var mod in Modifiers)
        {
            var valMod = mod.GetModifier(info);
            if (valMod.Type == ValueModifierType.Set) finalValue = valMod.Value;
            else if (valMod.Type == ValueModifierType.Add) finalValue += valMod.Value;
            else if (valMod.Type == ValueModifierType.Multiply) multiplyAgg *= valMod.Value;
        }
        return finalValue * multiplyAgg;
    }
}

public struct RuntimeVFXPlan
{
    public int VfxDictionaryId;
    public VFXTheme Theme;
    public VFXTopology Topology;
    public VFXLifecycle Lifecycle;
}