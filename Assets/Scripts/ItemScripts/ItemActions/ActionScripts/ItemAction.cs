using Fusion;
using System;
using UnityEngine;

public abstract class ItemAction : ScriptableObject
{
    [NonSerialized] public int ComboIndex;
    [NonSerialized] public EquipableItem Item;
    [NonSerialized] public ItemActionChannel Channel;
    public virtual void InitializeRuntimeForItem(EquipableItem item, int comboIndex, ItemActionChannel channel)
    {
        Item = item;
        ComboIndex = comboIndex;
        Channel = channel;

        float dt = Item != null && Item.Runner != null ? Item.Runner.DeltaTime : Time.fixedDeltaTime;
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
        if (Item == null || Item.activeCaster == null || Channel == ItemActionChannel.Feed)
            return;

        SpellGraphId spellId = Channel == ItemActionChannel.Secondary ? Item.SecondarySpellID : Item.PrimarySpellID;
        SpellGraph legacyGraph = Channel == ItemActionChannel.Secondary ? Item.secondaryActionSpell : Item.primaryActionSpell;

        if (spellId.IsNull())
            return;

        if (SpellStateManager.instance.GetHydratedSpell(spellId) == null)
        {
            Debug.LogWarning($"[ItemAction] Spell {spellId.BlueprintNumber} is not hydrated.");
            return;
        }

        CastActionController controller = Item.activeCaster;
        NetworkObject casterObject = controller.GetComponent<NetworkObject>();
        ActiveCastID newCastID = controller.GenerateNewCastID();

        SpellState newCast = new SpellState(newCastID, controller, Item, spellId, casterObject);
        newCast.CastPosition = controller.transform.position;
        newCast.ComboIndex = comboIndex;
        newCast.isHeld = true;

        controller.RegisterAndTrackCast(newCast, legacyGraph);
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
        if (Item == null || Channel == ItemActionChannel.Feed)
            return;

        SpellGraphId spellId = Channel == ItemActionChannel.Secondary ? Item.SecondarySpellID : Item.PrimarySpellID;
        IRuntimeNode rootNode = SpellStateManager.instance.GetHydratedSpell(spellId);

        if (rootNode is RuntimeEntryPoint entryPoint)
            entryPoint.Execute(triggerInfo);
        else if (rootNode is IRuntimeCore core)
            core.ExecuteCore(triggerInfo);
        else
            Debug.LogError($"[ItemAction] Failed to execute spell {spellId.BlueprintNumber}.");
    }
}