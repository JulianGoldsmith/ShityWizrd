using UnityEngine;

public struct DerivedNPCActionContext
{
    public NetworkNPCActionData ActionData;

    public int CurrentTick;
    public int ActionTick;
    public int DurationTicks;

    public bool IsComplete;

    public bool IsActionStart => ActionTick == 0;

    public bool IsActionTick(int tick)
    {
        return ActionTick == tick;
    }
}

public abstract class NPCAction : ScriptableObject
{
    public virtual bool IsImplemented => false;
    public virtual bool CreatesSpellState => false;

    public virtual bool TryDeriveActionContext(in NetworkNPCActionData actionData, int currentTick, out DerivedNPCActionContext context)
    {
        context = default;
        return false;
    }

    public virtual void Tick(NPCActionManager manager, in DerivedNPCActionContext context)
    {
    }

    protected DerivedNPCActionContext CreateDerivedContext(in NetworkNPCActionData actionData, int currentTick, int durationTicks)
    {
        return new DerivedNPCActionContext
        {
            ActionData = actionData,
            CurrentTick = currentTick,
            ActionTick = Mathf.Max(0, currentTick - actionData.startTick),
            DurationTicks = durationTicks,
            IsComplete = currentTick >= actionData.startTick + durationTicks
        };
    }
}