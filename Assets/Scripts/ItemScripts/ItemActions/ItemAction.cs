using Fusion;
using System;
using UnityEngine;

public abstract class ItemAction : ScriptableObject
{
    [NonSerialized] public int ComboIndex;
    [NonSerialized] public EquipableItem Item;

    public virtual void InitializeRuntimeForItem(EquipableItem item, int comboIndex)
    {
        Item = item;
        ComboIndex = comboIndex;
        // Let derived classes init their own animations
        float dt = (Item != null && Item.Runner != null)
            ? Item.Runner.DeltaTime
            : Time.fixedDeltaTime;

        InitializeAnimationTickCache(dt);
    }

    protected virtual void InitializeAnimationTickCache(float dt)
    {
        // default: do nothing
    }

    public abstract void OnPress(int comboIndex,bool isAlreadyReleased);

    public abstract void OnRelease(int comboIndex);

    public virtual void Tick(int comboIndex,float deltaTime){ }

    public virtual ItemAnimation GetAnimationForPhase(int phaseIndex)
    {
        return null;
    }

    protected virtual void CreateAndRegisterSpellState(int comboIndex)
    {
        if (Item == null || Item.primaryActionSpell == null) return;

        SpellGraph graph = Item.primaryActionSpell;
        //graph.CompileSpell();

        var controller = Item.activeCaster;
        var netObj = controller.GetComponent<NetworkObject>();

        ActiveCastID newCastID = controller.GenerateNewCastID();
        SpellState newCast = new SpellState(newCastID,controller, Item, graph, null, netObj);
        newCast.CastPosition = newCast.Controller.transform.position;
        newCast.ComboIndex = comboIndex;
        newCast.isHeld = true;

        /*ActiveSpell newActiveSpell = new ActiveSpell(newCastID, graph, newCast);

        newActiveSpell.AddToken();

        SpellStateManager.instance.RegisterNewCast(newCastID, newActiveSpell);*/
        controller.RegisterAndTrackCast(newCast, graph);

        Item.CurrentCastID = newCastID;

    }

    protected virtual void RemoveCastingToken(SpellState state)
    {
        if (state == null) return;

        ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(state.ActiveCastID);
        if (activeSpell != null)
        {
            activeSpell.MarkInitialExecutionDone();
            activeSpell.RemoveToken();
        }
    }

    protected virtual void RemoveSpellState()
    {
        Item.ClearSpellState();
    }

    protected void ExecuteHydratedSpell(SpellTriggerInfo triggerInfo)
    {
        if (Item == null) return;

        IRuntimeNode rootNode = SpellStateManager.instance.GetHydratedSpell(Item.PrimarySpellID);

        if (rootNode != null)
        {
            if (rootNode is RuntimeEntryPoint entryPoint)
            {
                entryPoint.Execute(triggerInfo);
            }
            else if (rootNode is IRuntimeCore core)
            {
                core.ExecuteCore(triggerInfo);
            }
        }
        else
        {
            Debug.LogError($"[ItemAction] Failed to execute! Spell {Item.PrimarySpellID.BlueprintNumber} is not hydrated in RAM.");
        }
    }
}