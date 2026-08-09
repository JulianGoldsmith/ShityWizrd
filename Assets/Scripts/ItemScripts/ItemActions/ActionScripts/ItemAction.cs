using Fusion;
using System;
using UnityEngine;

public struct DerivedActionContext
{
    public NetworkPlayerActionData ActionData;

    public int CurrentTick;
    public int ActionTick;

    public int PhaseID;
    public int PhaseStartTick;
    public int TickInPhase;
    public int PhaseDurationTicks;

    public bool IsComplete;

    public bool IsActionStart => ActionTick == 0;
    public bool IsPhaseStart => TickInPhase == 0;
    public bool IsReleased => ActionData.HasReleased && CurrentTick >= ActionData.ReleaseTick;
    public float NormalizedPhaseTime => PhaseDurationTicks > 0 ? Mathf.Clamp01((float)TickInPhase / PhaseDurationTicks) : 0f;

    public int ComboIndex => ActionData.ComboIndex;

    public bool IsPhaseTick(int tick)
    {
        return TickInPhase == tick;
    }
}

public abstract class ItemAction : ScriptableObject
{

    public virtual bool IsImplemented => false;
    public virtual bool CreatesSpellState => false;

    protected SpellState EnsureSpellState(PlayerActionManager manager, EquipableItem item, in DerivedActionContext context)
    {
        if (!CreatesSpellState) return null;
        if (!context.ActionData.CastID.IsValid) return null;
        if (manager == null || manager.CastController == null || item == null) return null;
        if (SpellStateManager.instance == null) return null;

        ActiveSpell existingSpell = SpellStateManager.instance.GetActiveSpell(context.ActionData.CastID);

        if (existingSpell != null)
        {
            existingSpell.State.ComboIndex = context.ComboIndex;
            return existingSpell.State;
        }

        ItemActionChannel channel = context.ActionData.Channel;
        SpellGraphId spellID = context.ActionData.SpellID;

        if (spellID.IsNull()) return null;

        if (SpellBlueprintLibrary.Get(spellID) == null)
        {
            Debug.LogWarning($"[ItemAction] Spell {spellID.BlueprintNumber} is not hydrated.");
            return null;
        }

        CastActionController controller = manager.CastController;
        SpellState state = new SpellState(context.ActionData.CastID, controller, item, spellID, manager.Object);

        state.ComboIndex = context.ComboIndex;
        state.CastPosition = controller.transform.position;
        state.isHeld = true;

        controller.RegisterAndTrackCast(state);
        if (item.HasStateAuthority || item.HasInputAuthority) item.CurrentCastID = context.ActionData.CastID;

        return state;
    }

    protected void ExecuteSpawnCoreSpell(SpellGraphId spellID, SpellTriggerInfo triggerInfo)
    {
        if (spellID.IsNull()) return;
        RuntimeSpell runtimeSpell = SpellBlueprintLibrary.Get(spellID);

        if (runtimeSpell == null)
        {
            Debug.LogError($"[ItemAction] Spell {spellID.BlueprintNumber} could not be resolved.");
            return;
        }

        IRuntimeNode rootNode = runtimeSpell.RootNode;

        if (rootNode is RuntimeEntryPoint entryPoint)
        {
            if (entryPoint.ExpectedType != EntryPointType.SpawnCore)
            {
                Debug.LogError($"[ItemAction] Spell {spellID.BlueprintNumber} is not a SpawnCore spell.");
                return;
            }
            rootNode = entryPoint.ConnectedLogic;
        }

        if (rootNode is IRuntimeCore core)
        {
            core.ExecuteCore(triggerInfo);
            return;
        }

        Debug.LogError($"[ItemAction] Spell {spellID.BlueprintNumber} does not begin with a core.");
    }
    public virtual bool TryDeriveActionContext(in NetworkPlayerActionData actionData, int currentTick, out DerivedActionContext context)
    {
        context = default;
        return false;
    }

    public virtual void Tick(PlayerActionManager manager, EquipableItem item, in DerivedActionContext context)
    {
    }

    protected DerivedActionContext CreateDerivedContext(in NetworkPlayerActionData actionData, int currentTick, int phaseID, int phaseStartTick, int phaseDurationTicks, bool isComplete = false)
    {
        return new DerivedActionContext
        {
            ActionData = actionData,
            CurrentTick = currentTick,
            ActionTick = Mathf.Max(0, currentTick - actionData.StartTick),
            PhaseID = phaseID,
            PhaseStartTick = phaseStartTick,
            TickInPhase = Mathf.Max(0, currentTick - phaseStartTick),
            PhaseDurationTicks = phaseDurationTicks,
            IsComplete = isComplete
        };
    }

    public virtual ItemAnimation GetAnimationForPhase(int phaseID)
    {
        return default;
    }

    protected void RemoveCastingToken(SpellState state)
    {
        if (state == null) return;

        ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(state.ActiveCastID);

        if (activeSpell != null)
        {
            activeSpell.MarkInitialExecutionDone();
            activeSpell.RemoveToken();
        }
    }
}

public struct EyePosAndLookDir
{
    public Vector3 EyePosition;
    public Vector3 Forward;
    public Vector3 Up;
    public Vector3 Right;

    public EyePosAndLookDir(Vector3 eyePosition, Vector3 forward, Vector3 up)
    {
        EyePosition = eyePosition;
        Forward = forward.normalized;
        Up = up.normalized;
        Right = Vector3.Cross(Up, Forward).normalized;
    }
}


[Serializable]
public class CastMethods
{
    public ItemAction SpawnCore;
    public ItemAction Trigger;
    public ItemAction Effect;

    public ItemAction GetAction(EntryPointType entryPointType)
    {
        switch (entryPointType)
        {
            case EntryPointType.SpawnCore: return SpawnCore;
            case EntryPointType.Trigger: return Trigger;
            case EntryPointType.Effect: return Effect;
            default: return null;
        }
    }

    public bool Supports(EntryPointType entryPointType)
    {
        return GetAction(entryPointType) != null;
    }
}