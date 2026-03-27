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

        graph.CompileSpell();

        var netObj = Item.activeCaster.GetComponent<NetworkObject>();
        SpellState newCast = new SpellState(Item.activeCaster, Item, graph, null, netObj);

        newCast.CastPosition = newCast.Controller.transform.position;

        newCast.ComboIndex = comboIndex;
        newCast.isHeld = true;
        Item.activeCast = newCast;

    }
    protected virtual void RemoveSpellState()
    {
        Item.activeCast = null;
    }


}