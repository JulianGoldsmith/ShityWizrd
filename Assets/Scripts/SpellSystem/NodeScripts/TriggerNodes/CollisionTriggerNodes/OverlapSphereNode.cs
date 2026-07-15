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

        List<RuntimeVFXPlan> vfxPlans = new List<RuntimeVFXPlan>();
        List<VFXTheme> discoveredThemes = GetDownstreamThemes(context);
        foreach (var theme in discoveredThemes)
        {
            vfxPlans.Add(new RuntimeVFXPlan()
            {
                VfxDictionaryId = context.ClaimVFXId(),
                Theme = theme,
                Topology = this.Topology,
                Lifecycle = this.Lifecycle
            });
        }

        int vfxId = context.ClaimVFXId();

        return new OverlapSphereTrigger()
        {
            Radius = new RuntimeFloatProperty(this.radius),
            TickDuration = tickDuration,
            StartTickMemoryIndex = startTickSlot,
            Filters = this.filterNodes.ToArray(),

            VfxPlans = vfxPlans
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

    public List<RuntimeVFXPlan> VfxPlans;

    public override void InitTick(ISpellExecutionCore core)
    {
        core.SetInt(StartTickMemoryIndex, core.Runner.Tick);
    }

    private Collider[] _overlapResults = new Collider[64];

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

    public override void TickVFX(ISpellExecutionCore core)
    {
        if (VfxPlans == null || VfxPlans.Count == 0) return;

        bool shouldBeActive = true;
        int startTick = core.GetInt(StartTickMemoryIndex);

        if (TickDuration > 0 && (core.Runner.Tick - startTick) >= TickDuration)
        {
            shouldBeActive = false;
        }

        float currentRadius = Radius.GetValue(default);
        Vector3 currentPos = core.Position;
        Quaternion currentRot = core.Rotation;

        foreach (var plan in VfxPlans)
        {
            bool currentlyExists = core.ActiveVisuals.TryGetValue(plan.VfxDictionaryId, out GameObject currentVfx);

            if (shouldBeActive && !currentlyExists)
            {
                var vfxData = VFXRegistry.GetVFX(plan.Theme, plan.Topology, plan.Lifecycle);

                if (vfxData.prefab != null)
                {
                    GameObject newVfx = GameObject.Instantiate(vfxData.prefab, currentPos, currentRot, null);

                    if (newVfx.TryGetComponent<SpellVFX>(out var vfxController))
                    {
                        vfxController.Initialize(vfxData.tint);
                        vfxController.UpdateSpatialData(currentRadius, 1.0f, currentPos, currentPos);
                    }

                    core.ActiveVisuals[plan.VfxDictionaryId] = newVfx;
                }
            }
            else if (shouldBeActive && currentlyExists)
            {
                if (currentVfx != null && currentVfx.TryGetComponent<SpellVFX>(out var vfxController))
                {
                    vfxController.UpdateSpatialData(currentRadius, 1.0f, currentPos, currentPos);
                }
            }
            else if (!shouldBeActive && currentlyExists)
            {
                if (currentVfx != null)
                {
                    if (currentVfx.TryGetComponent<SpellVFX>(out var vfxController)) vfxController.StopAndCleanup();
                    else GameObject.Destroy(currentVfx);
                }
                core.ActiveVisuals.Remove(plan.VfxDictionaryId);
            }
        }
    }

    public override void CleanupVFX(ISpellExecutionCore core)
    {
        if (VfxPlans == null) return;

        foreach (var plan in VfxPlans)
        {
            // Use the interface dictionary
            if (core.ActiveVisuals.TryGetValue(plan.VfxDictionaryId, out GameObject currentVfx))
            {
                if (currentVfx != null)
                {
                    if (currentVfx.TryGetComponent<SpellVFX>(out var vfxController)) vfxController.StopAndCleanup();
                    else GameObject.Destroy(currentVfx);
                }
                core.ActiveVisuals.Remove(plan.VfxDictionaryId);
            }
        }
    }
}