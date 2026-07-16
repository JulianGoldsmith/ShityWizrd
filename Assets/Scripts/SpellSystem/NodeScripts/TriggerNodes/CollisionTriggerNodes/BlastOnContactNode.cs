using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BlastOnContactNode", menuName = "SpellNodes/TriggerNodes/Blast On Contact Node")]
public class BlastOnContactNode : TriggerNode
{
    [Promotable("Blast Radius", DataTypeTag.Radius)]
    public float radius = 2f;

    [Tooltip("How many times can this core explode before stopping? (Usually 1)")]
    public int maxContacts = 1;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        int hitMemorySlot = context.ClaimIntSlot();

        // Grab the downstream themes (e.g., Fire, Frost) to color our explosion
        List<VFXTheme> discoveredThemes = GetDownstreamThemes(context);

        return new BlastOnContactTrigger()
        {
            Radius = new RuntimeFloatProperty(this.radius),
            MaxContacts = maxContacts,
            HitMemorySlot = hitMemorySlot,
            Filters = this.filterNodes.ToArray(),

            // Pass taxonomy to the runtime for One-Shot logging
            Themes = discoveredThemes.ToArray(),
            Topology = this.Topology,
            Lifecycle = VFXLifecycle.Burst // Hardcoded to Burst because it's a one-shot!
        };
    }

    public override void SetUp(GameObject spellCore, SpellState state) { }
}

public class BlastOnContactTrigger : RuntimeTriggerBase
{
    public RuntimeFloatProperty Radius;
    public int MaxContacts;
    public int HitMemorySlot;
    public FilterNode[] Filters;

    public VFXTheme[] Themes;
    public VFXTopology Topology;
    public VFXLifecycle Lifecycle;

    private Collider[] _overlapResults = new Collider[64];

    public override void InitTick(ISpellExecutionCore core) { }

    public override bool Tick(ISpellExecutionCore core, float deltaTime, out List<SpellTriggerInfo> hitInfos)
    {
        hitInfos = new List<SpellTriggerInfo>();

        // 1. THE CAPABILITIES BRIDGE
        if (!core.TryGetCoreComponent<SpellCreatedCore>(out var physicalCore))
        {
            return false; // Virtual cores can't collide!
        }

        if (physicalCore.TickContacts.Count == 0) return false;

        int currentHits = core.GetInt(HitMemorySlot);
        if (MaxContacts > 0 && currentHits >= MaxContacts) return false;

        SpellState activeState = null;
        if (physicalCore.ActiveCastID.IsValid)
        {
            ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(physicalCore.ActiveCastID);
            if (activeSpell != null) activeState = activeSpell.State;
        }

        // Evaluate the radius dynamically based on the spell graph math
        SpellTriggerInfo dummyInfo = new SpellTriggerInfo(false, core.SourceObject, activeState, core.Position, core.Rotation, null);
        float currentRadius = Radius.GetValue(dummyInfo);

        PhysicsScene physicsScene = core.Runner.GetPhysicsScene();

        // 2. PROCESS THE IMPACTS
        foreach (var contact in physicalCore.TickContacts)
        {
            GameObject targetObj = contact.Target;

            // Ignore ourselves and the caster
            if (targetObj == core.SourceObject) continue;
            if (activeState != null && activeState.Caster != null && targetObj == activeState.Caster.gameObject) continue;

            int deterministicEventId = physicalCore.ActiveCastID.GetHashCode() ^ (currentHits * 73856);

            if (core.VFXManager != null)
            {
                foreach (var theme in Themes)
                {
                    core.VFXManager.ProcessHit(
                        contact.Point,
                        contact.Normal,
                        currentRadius,
                        theme,
                        Topology,
                        Lifecycle
                    );
                }
            }

            // 4. PERFORM THE BLAST (Overlap Sphere)
            int hitCount = physicsScene.OverlapSphere(
                contact.Point,
                currentRadius,
                _overlapResults,
                SpellSystemHelpers.GeneralCollisionLayerMask(),
                QueryTriggerInteraction.UseGlobal
            );

            for (int i = 0; i < hitCount; i++)
            {
                GameObject aoeTarget = SpellSystemHelpers.GetHitGameObject(_overlapResults[i]);

                if (aoeTarget == core.SourceObject) continue;
                if (activeState != null && activeState.Caster != null && aoeTarget == activeState.Caster.gameObject) continue;

                bool isValid = true;
                if (Filters != null)
                {
                    foreach (var filter in Filters)
                    {
                        if (!filter.Evaluate(aoeTarget)) { isValid = false; break; }
                    }
                }

                if (isValid)
                {
                    Collider col = _overlapResults[i];
                    Vector3 hitPos = col.ClosestPoint(contact.Point);
                    Vector3 hitNormal = (hitPos - contact.Point).normalized;
                    if (hitNormal == Vector3.zero) hitNormal = Vector3.up;

                    hitInfos.Add(new SpellTriggerInfo(
                        isCast: false,
                        source: core.SourceObject,
                        state: activeState,
                        position: hitPos,
                        rotation: Quaternion.LookRotation(hitNormal),
                        triggerVector: hitNormal,
                        hitObject: aoeTarget
                    ));
                }
            }

            Array.Clear(_overlapResults, 0, hitCount);

            currentHits++;
            core.SetInt(HitMemorySlot, currentHits);

            // If we hit our max explosion count (usually 1), stop processing contacts
            if (MaxContacts > 0 && currentHits >= MaxContacts)
            {
                break;
            }
        }

        return hitInfos.Count > 0;
    }

    // Notice how wonderfully empty these are now!
    // The visual lifecycle is entirely managed by the Orphaned Prefab and EntityVFXManager.
    public override void TickVFX(ISpellExecutionCore core) { }
    public override void CleanupVFX(ISpellExecutionCore core) { }
}