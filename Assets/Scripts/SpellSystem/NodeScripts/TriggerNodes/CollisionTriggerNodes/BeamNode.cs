using Fusion;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "BeamNode", menuName = "SpellNodes/TriggerNodes/BeamNode")]
public class BeamNode : TriggerNode
{
    [Promotable("Radius", DataTypeTag.Radius)]
    public float radius = 0.5f;

    [Promotable("Max Range", DataTypeTag.Generic)]
    public float maxRange = 20f;

    [Promotable("Pierce Number", DataTypeTag.Generic)]
    public float pierceNumber = 0f;

    [Tooltip("0 = Infinite, 1 = Instant Blast (Single Trigger), >1 = Lingering Beam")]
    public int tickDuration = 0;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        int startTickSlot = context.ClaimIntSlot();
        int endpointVectorSlot = context.ClaimVectorSlot();

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

        return new BeamTrigger()
        {
            Radius = new RuntimeFloatProperty(this.radius),
            MaxRange = new RuntimeFloatProperty(this.maxRange),
            PierceNumber = new RuntimeFloatProperty(this.pierceNumber),
            TickDuration = tickDuration,

            StartTickMemoryIndex = startTickSlot,
            EndpointMemoryIndex = endpointVectorSlot,

            Filters = this.filterNodes.ToArray(),
            VfxPlans = vfxPlans
        };
    }

    public override void SetUp(GameObject spellCore, SpellState state)
    {
    }
}

public class BeamTrigger : RuntimeTriggerBase
{
    public RuntimeFloatProperty Radius;
    public RuntimeFloatProperty MaxRange;
    public RuntimeFloatProperty PierceNumber;

    public int TickDuration;
    public int StartTickMemoryIndex;
    public int EndpointMemoryIndex;

    public FilterNode[] Filters;
    public List<RuntimeVFXPlan> VfxPlans;

    private RaycastHit[] _raycastHits = new RaycastHit[64];

    public override void InitTick(ISpellExecutionCore core)
    {
        core.SetInt(StartTickMemoryIndex, core.Runner.Tick);

        SpellTriggerInfo dummyInfo = new SpellTriggerInfo(false, core.SourceObject, null, core.Position, core.Rotation, null);
        float range = MaxRange.GetValue(dummyInfo);
        core.SetVector(EndpointMemoryIndex, core.Position + (core.Rotation * Vector3.forward * range));
    }

    public override bool Tick(ISpellExecutionCore core, float deltaTime, out List<SpellTriggerInfo> hitInfos)
    {
        hitInfos = new List<SpellTriggerInfo>();

        int startTick = core.GetInt(StartTickMemoryIndex);
        if (TickDuration > 0 && (core.Runner.Tick - startTick) >= TickDuration)
        {
            return false;
        }

        SpellState activeState = null;
        if (core.ActiveCastID.IsValid)
        {
            ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(core.ActiveCastID);
            if (activeSpell != null) activeState = activeSpell.State;
        }

        SpellTriggerInfo evaluationInfo = new SpellTriggerInfo(
            isCast: false, source: core.SourceObject, state: activeState,
            position: core.Position, rotation: core.Rotation, hitObject: null
        );

        float currentRadius = Radius.GetValue(evaluationInfo);
        float currentMaxRange = MaxRange.GetValue(evaluationInfo);
        int currentPierces = Mathf.FloorToInt(PierceNumber.GetValue(evaluationInfo));

        Vector3 origin = core.Position;
        Vector3 direction = core.Rotation * Vector3.forward;

        PhysicsScene physicsScene = core.Runner.GetPhysicsScene();
        int hitCount = physicsScene.SphereCast(
            origin, currentRadius, direction, _raycastHits, currentMaxRange,
            SpellSystemHelpers.GeneralCollisionLayerMask(), QueryTriggerInteraction.UseGlobal
        );

        var sortedHits = _raycastHits.Take(hitCount).OrderBy(h => h.distance).ToArray();

        Vector3 finalEndpoint = origin + (direction * currentMaxRange);

        for (int i = 0; i < sortedHits.Length; i++)
        {
            GameObject targetObj = sortedHits[i].collider.gameObject;
            Debug.Log($"Beam hit : {targetObj.name}");
            if (targetObj == core.SourceObject) continue;

            bool isValid = true;
            if (Filters != null)
            {
                foreach (var filter in Filters)
                {
                    if (!filter.Evaluate(targetObj)) { isValid = false; break; }
                }
            }

            if (!isValid)
            {
                finalEndpoint = sortedHits[i].point;

                if (finalEndpoint == Vector3.zero) finalEndpoint = origin + (direction * sortedHits[i].distance);

                break;
            }
            else
            {
                Vector3 hitPos = sortedHits[i].point;
                if (hitPos == Vector3.zero) hitPos = origin + (direction * sortedHits[i].distance);

                hitInfos.Add(new SpellTriggerInfo(
                    isCast: false,
                    source: core.SourceObject,
                    state: activeState,
                    position: hitPos,
                    rotation: Quaternion.LookRotation(-direction),
                    triggerVector: direction,
                    hitObject: targetObj
                ));

                if (currentPierces <= 0)
                {
                    // Out of pierces. Stop here.
                    finalEndpoint = hitPos;
                    break;
                }
                else
                {
                    // Pierce and continue!
                    currentPierces--;
                }
            }
        }

        // 5. Save the endpoint to the Network Sketchpad so VFX can read it during Render()
        core.SetVector(EndpointMemoryIndex, finalEndpoint);

        // Prevent ghost data on next tick
        Array.Clear(_raycastHits, 0, _raycastHits.Length);

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

        // Safely pull the dynamic coordinates from the network array
        Vector3 currentOrigin = core.Position;
        Vector3 currentTarget = core.GetVector(EndpointMemoryIndex);

        // Context for dynamic scaling visually
        SpellState activeState = null;
        if (core.ActiveCastID.IsValid)
        {
            ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(core.ActiveCastID);
            if (activeSpell != null) activeState = activeSpell.State;
        }

        SpellTriggerInfo evaluationInfo = new SpellTriggerInfo(false, core.SourceObject, activeState, currentOrigin, core.Rotation, null);
        float currentRadius = Radius.GetValue(evaluationInfo);

        foreach (var plan in VfxPlans)
        {
            bool currentlyExists = core.ActiveVisuals.TryGetValue(plan.VfxDictionaryId, out GameObject currentVfx);

            if (shouldBeActive && !currentlyExists)
            {
                var vfxData = VFXRegistry.GetVFX(plan.Theme, plan.Topology, plan.Lifecycle);

                if (vfxData.prefab != null)
                {
                    // Unparented instantiation for true World Space decoupling
                    GameObject newVfx = GameObject.Instantiate(vfxData.prefab, currentOrigin, Quaternion.identity, null);

                    if (newVfx.TryGetComponent<SpellVFX>(out var vfxController))
                    {
                        vfxController.Initialize(vfxData.tint);
                        vfxController.UpdateSpatialData(currentRadius, 1.0f, currentOrigin, currentTarget);
                    }

                    core.ActiveVisuals[plan.VfxDictionaryId] = newVfx;
                }
            }
            else if (shouldBeActive && currentlyExists)
            {
                if (currentVfx != null && currentVfx.TryGetComponent<SpellVFX>(out var vfxController))
                {
                    // Dynamically sweep the beam across the world
                    vfxController.UpdateSpatialData(currentRadius, 1.0f, currentOrigin, currentTarget);
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