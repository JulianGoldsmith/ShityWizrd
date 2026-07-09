using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "OverlapSphereNode", menuName = "SpellNodes/TriggerNodes/OverlapSphereNode")]
public class OverlapSphereNode : TriggerNode
{
    [Promotable("Size", DataTypeTag.Radius)]
    public float radius = 2f;

    [Tooltip("0 = Infinite Field, 1 = Instant Blast (Single Trigger), >1 = Lingering Field")]
    public int tickDuration = 1;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        // 2. Claim our single memory slot to track when this sphere was born
        int startTickSlot = context.ClaimIntSlot();
        int vfxId = context.ClaimVFXId();

        // 3. Return the pure C# trigger. 
        // Note: The base TriggerNode will automatically attach the Plan/Effects to this!
        return new OverlapSphereTrigger()
        {
            Radius = new RuntimeFloatProperty(this.radius),
            TickDuration = tickDuration,
            StartTickMemoryIndex = startTickSlot,
            Filters = this.filterNodes.ToArray(), // Hand the filters directly to the stateless object

            VfxDictionaryId = vfxId,
            VfxContext = this.vfx_context,
            VfxModType = this.default_vfx_modifier_type
        };
    }

    public override void SetUp(GameObject spellCore, SpellState state)
    {

    }
}

public class OverlapSphereTrigger : RuntimeTriggerBase
{
    public RuntimeFloatProperty Radius;
    public int TickDuration;
    public int StartTickMemoryIndex;

    public FilterNode[] Filters;

    public int VfxDictionaryId;
    public VFXContext VfxContext;
    public ModifierType VfxModType;

    // 1. SIGNATURE UPDATE
    public override void InitTick(ISpellExecutionCore core)
    {
        core.SetInt(StartTickMemoryIndex, core.Runner.Tick);
    }

    private Collider[] _overlapResults = new Collider[64];

    // 1. SIGNATURE UPDATE
    public override bool Tick(ISpellExecutionCore core, float deltaTime, out List<SpellTriggerInfo> hitInfos)
    {
        hitInfos = new List<SpellTriggerInfo>();

        int startTick = core.GetInt(StartTickMemoryIndex);
        if (TickDuration > 0 && (core.Runner.Tick - startTick) >= TickDuration)
        {
            return false;
        }

        PhysicsScene physicsScene = core.Runner.GetPhysicsScene();

        // 2. SPATIAL UPDATE (Use core.Position)
        int hitCount = physicsScene.OverlapSphere(
            core.Position,
            Radius.GetValue(default),
            _overlapResults,
            SpellSystemHelpers.GeneralCollisionLayerMask(),
            QueryTriggerInteraction.UseGlobal
        );

        // 3. USE THE INTERFACE FOR CAST ID
        SpellState activeState = null;
        if (core.ActiveCastID.IsValid)
        {
            ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(core.ActiveCastID);
            if (activeSpell != null) activeState = activeSpell.State;
        }

        for (int i = 0; i < hitCount; i++)
        {
            GameObject targetObj = _overlapResults[i].gameObject;

            // 4. ANCHOR BRIDGE (Ignore ourselves)
            if (targetObj == core.SourceObject) continue;

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
                Collider col = _overlapResults[i];
                Vector3 hitPos = col.ClosestPoint(core.Position); // SPATIAL UPDATE
                Vector3 hitNormal = (hitPos - core.Position).normalized;

                Quaternion hitRot = hitNormal.sqrMagnitude > 0 ? Quaternion.LookRotation(hitNormal) : Quaternion.identity;

                hitInfos.Add(new SpellTriggerInfo(
                    isCast: false,
                    source: core.SourceObject, // ANCHOR BRIDGE
                    state: activeState,
                    position: hitPos,
                    rotation: hitRot,
                    triggerVector: hitNormal,
                    hitObject: targetObj
                ));
            }
        }

        Array.Clear(_overlapResults, 0, hitCount);

        return hitInfos.Count > 0;
    }

    // 1. SIGNATURE UPDATE
    public override void TickVFX(ISpellExecutionCore core)
    {
        if (VfxContext == null) return;

        // 5. CAPABILITIES BRIDGE (Only physical cores use ActiveVisuals dict)
        if (core.TryGetCoreComponent<SpellCreatedCore>(out var physicalCore))
        {
            bool shouldBeActive = false;

            if (physicalCore.IsActiveInBuffer)
            {
                int startTick = core.GetInt(StartTickMemoryIndex);

                if (TickDuration == 0 || (core.Runner.Tick - startTick) < TickDuration)
                {
                    shouldBeActive = true;
                }
            }

            bool currentlyExists = physicalCore.ActiveVisuals.TryGetValue(VfxDictionaryId, out GameObject currentVfx);

            if (shouldBeActive && !currentlyExists)
            {
                GameObject newVfx = SpellSystemHelpers.CreateVFX(VfxContext, VfxModType, physicalCore.transform, Radius.GetValue(default), true);
                if (newVfx != null) physicalCore.ActiveVisuals[VfxDictionaryId] = newVfx;
            }
            else if (!shouldBeActive && currentlyExists)
            {
                if (currentVfx != null) GameObject.Destroy(currentVfx);
                physicalCore.ActiveVisuals.Remove(VfxDictionaryId);
            }
        }
    }

    // 1. SIGNATURE UPDATE
    public override void CleanupVFX(ISpellExecutionCore core)
    {
        if (core.TryGetCoreComponent<SpellCreatedCore>(out var physicalCore))
        {
            if (physicalCore.ActiveVisuals.TryGetValue(VfxDictionaryId, out GameObject currentVfx))
            {
                if (currentVfx != null) GameObject.Destroy(currentVfx);
                physicalCore.ActiveVisuals.Remove(VfxDictionaryId);
            }
        }
    }
}