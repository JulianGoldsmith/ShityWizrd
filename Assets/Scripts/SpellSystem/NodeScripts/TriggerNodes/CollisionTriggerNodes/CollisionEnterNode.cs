using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "CollisionEnterNode", menuName = "SpellNodes/TriggerNodes/Collision Enter Node")]
public class CollisionEnterNode : TriggerNode
{

    public int maxContacts = 1;

    public override ITrigger CompileTriggerCondition(SpellCompilationContext context)
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
            VfxContext = this.vfx_context,
            VfxModType = this.default_vfx_modifier_type
        };
    }

    public override void SetUp(GameObject spellCore, SpellState state)
    {
        /*EnterCollisionST collisionChecker = spellCore.AddComponent<EnterCollisionST>();
        collisionChecker.state = state;
        collisionChecker.filterNodes = this.filterNodes;
        collisionChecker.outcomeNodes = this.outcomeNodes;*/
    }
}

public class CollisionEnterTrigger : ITrigger
{
    public TriggerExecutioPlan Plan { get; set; }

    public int MaxContacts;
    public int HitMemorySlot;
    public FilterNode[] Filters;

    public int VfxDictionaryId;
    public VFXContext VfxContext;
    public ModifierType VfxModType;

    public void InitTick(SpellCreatedCore core) { }

    public bool Tick(SpellCreatedCore core, float deltaTime, out List<SpellTriggerInfo> hitInfos)
    {
        hitInfos = new List<SpellTriggerInfo>();

        if (core.TickContacts.Count == 0) return false;

        int currentHits = core.GetInt(HitMemorySlot);
        if (MaxContacts > 0 && currentHits >= MaxContacts) return false;

        SpellState activeState = null;
        PhysicsObject instigator = null;

        if (core.ActiveCastID.IsValid)
        {
            ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(core.ActiveCastID);
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
        foreach (var contact in core.TickContacts)
        {
            GameObject targetObj = contact.Target;

            if (targetObj == core.gameObject) continue;
            if (activeState != null && activeState.Caster != null && targetObj == activeState.Caster.gameObject) continue;

            if (targetObj.TryGetComponent<SpellCreatedCore>(out var targetCore))
            {
                if (targetCore.ActiveCastID.Equals(core.ActiveCastID)) continue; 
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
                    float impactSpeed = core.NetworkVelocity.magnitude;
                    if (impactSpeed < 1f) impactSpeed = 10f; // Fallback for stationary spells

                    // (Optional: Pass core mass if you add it to CoreContext later!)
                    targetPO.OnBonk(impactSpeed * 1f, instigator.Object, contact.Point);
                }

                // Package the Hit Info for the downstream Effects!
                hitInfos.Add(new SpellTriggerInfo()
                {
                    IsValid = true,
                    IsCast = false,
                    Source = core.gameObject,
                    State = activeState,
                    HasOverridePosition = true,
                    TriggerPoint = contact.Point,
                    TriggerRotation = contact.Normal.sqrMagnitude > 0 ? Quaternion.LookRotation(contact.Normal) : Quaternion.identity,
                    TriggerNormal = contact.Normal.sqrMagnitude > 0 ? Quaternion.LookRotation(contact.Normal) : Quaternion.identity,
                    TriggerVector = core.NetworkVelocity,
                    HitObject = targetObj
                });

                currentHits++;
                core.SetInt(HitMemorySlot, currentHits);
                if (MaxContacts > 0 && currentHits >= MaxContacts)
                {
                    break;
                }
            }
        }

        return hitInfos.Count > 0;
    }

    public void TickVFX(SpellCreatedCore core) { }
    public void CleanupVFX(SpellCreatedCore core) { }
}