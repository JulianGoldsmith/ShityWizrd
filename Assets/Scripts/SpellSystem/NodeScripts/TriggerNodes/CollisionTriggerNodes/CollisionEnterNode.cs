using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "CollisionEnterNode", menuName = "SpellNodes/TriggerNodes/Collision Enter Node")]
public class CollisionEnterNode : TriggerNode
{

    public int maxContacts = 1;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        int vfxId = context.ClaimVFXId();

        int bakedMaxContacts = maxContacts;

        int hitMemorySlot = context.ClaimIntSlot();

        return new CollisionEnterTrigger()
        {
            MaxContacts = bakedMaxContacts,
            HitMemorySlot = hitMemorySlot,
            Filters = this.filterNodes.ToArray(),

            VfxDictionaryId = vfxId,
            //VfxContext = this.vfx_context,
            //VfxModType = this.default_vfx_modifier_type
        };
    }

    public override void SetUp(GameObject spellCore, SpellState state)
    {

    }
}

public class CollisionEnterTrigger : RuntimeTriggerBase
{
    // (Note: We deleted 'TriggerExecutionPlan' here because 'RuntimeTriggerBase' 
    // now natively holds the 'Outcomes' list for us!)

    public int MaxContacts;
    public int HitMemorySlot;
    public FilterNode[] Filters;

    public int VfxDictionaryId;
    public ModifierType VfxModType;

    // 1. THE SIGNATURE UPDATE
    public override void InitTick(ISpellExecutionCore core) { }

    // 1. THE SIGNATURE UPDATE
    public override bool Tick(ISpellExecutionCore core, float deltaTime, out List<SpellTriggerInfo> hitInfos)
    {
        hitInfos = new List<SpellTriggerInfo>();

        // 2. THE CAPABILITIES BRIDGE!
        // We ask the interface: "Do you have a physical SpellCreatedCore component?"
        // If this is a Virtual Core (Ghost Beam), it gracefully returns false and skips the logic!
        if (!core.TryGetCoreComponent<SpellCreatedCore>(out var physicalCore))
        {
            return false;
        }

        // Now we can safely use the physicalCore to read its Unity collisions!
        if (physicalCore.TickContacts.Count == 0) return false;

        int currentHits = core.GetInt(HitMemorySlot);
        if (MaxContacts > 0 && currentHits >= MaxContacts) return false;

        SpellState activeState = null;
        PhysicsObject instigator = null;

        if (physicalCore.ActiveCastID.IsValid)
        {
            ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(physicalCore.ActiveCastID);
            if (activeSpell != null)
            {
                activeState = activeSpell.State;
                if (activeState.Caster != null)
                {
                    instigator = activeState.Caster.GetComponent<PhysicsObject>();
                }
            }
        }

        // 3. Process the hits!
        foreach (var contact in physicalCore.TickContacts)
        {
            GameObject targetObj = contact.Target;

            Debug.Log($"TargetObject {targetObj} and source object = {core.SourceObject}");

            // 3. THE ANCHOR BRIDGE
            // Replaced 'core.gameObject' with 'core.SourceObject'
            if (targetObj == core.SourceObject) continue;
            if (activeState != null && activeState.Caster != null && targetObj == activeState.Caster.gameObject) continue;

            if (targetObj.TryGetComponent<SpellCreatedCore>(out var targetCore))
            {
                if (targetCore.ActiveCastID.Equals(physicalCore.ActiveCastID)) continue;
            }

            bool isValid = true;
            if (Filters != null)
            {
                foreach (var filter in Filters)
                {
                    if (!filter.Evaluate(targetObj)) { isValid = false; break; }
                }
            }

            if (isValid)
            {
                if (targetObj.TryGetComponent<PhysicsObject>(out var targetPO))
                {
                    // Use the physical core for network velocity!
                    float impactSpeed = physicalCore.NetworkVelocity.magnitude;
                    if (impactSpeed < 1f) impactSpeed = 10f; // Fallback for stationary spells

                    targetPO.OnBonk(impactSpeed * 1f, instigator != null ? instigator.Object : null, contact.Point);
                }

                // Package the Hit Info for the downstream Effects!
                hitInfos.Add(new SpellTriggerInfo()
                {
                    IsValid = true,
                    IsCast = false,
                    Source = core.SourceObject, // The Anchor Bridge!
                    State = activeState,
                    HasOverridePosition = true,
                    TriggerPoint = contact.Point,
                    TriggerRotation = contact.Normal.sqrMagnitude > 0 ? Quaternion.LookRotation(contact.Normal) : Quaternion.identity,
                    TriggerNormal = contact.Normal.sqrMagnitude > 0 ? Quaternion.LookRotation(contact.Normal) : Quaternion.identity,
                    TriggerVector = physicalCore.NetworkVelocity, // Use the physical core for velocity
                    HitObject = targetObj
                });

                currentHits++;
                core.SetInt(HitMemorySlot, currentHits); // We can still safely write to the memory sketchpad!

                if (MaxContacts > 0 && currentHits >= MaxContacts)
                {
                    break;
                }
            }
        }

        return hitInfos.Count > 0;
    }

    // 1. THE SIGNATURE UPDATE
    public override void TickVFX(ISpellExecutionCore core) { }

    // 1. THE SIGNATURE UPDATE
    public override void CleanupVFX(ISpellExecutionCore core) { }
}