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
    public float size = 2f;

    [Tooltip("0 = Infinite Field, 1 = Instant Blast (Single Trigger), >1 = Lingering Field")]
    public int tickDuration = 1;

    public override ITrigger CompileTriggerCondition(SpellCompilationContext context)
    {
        // 1. Resolve Promotables
        float bakedSize = GetFinalValue(nameof(size), size);

        // 2. Claim our single memory slot to track when this sphere was born
        int startTickSlot = context.ClaimIntSlot();
        int vfxId = context.ClaimVFXId();

        // 3. Return the pure C# trigger. 
        // Note: The base TriggerNode will automatically attach the Plan/Effects to this!
        return new OverlapSphereTrigger()
        {
            Radius = bakedSize,
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
        /*OverlapSphereST sphereChecker = spellCore.AddComponent<OverlapSphereST>();
        sphereChecker.state = state;
        sphereChecker.filterNodes = this.filterNodes;
        sphereChecker.outcomeNodes = this.outcomeNodes;
        sphereChecker.size = size;
        sphereChecker.singleTrigger = singleTrigger;    

        OnAttach(sphereChecker, size);*/
    }
}

public class OverlapSphereTrigger : ITrigger
{
    // Required by ITrigger
    public TriggerExecutioPlan Plan { get; set; }

    public float Radius;
    public int TickDuration;
    public int StartTickMemoryIndex;

    // Baked in by the Compiler
    public FilterNode[] Filters;

    // Non-alloc buffer for the spatial query
    private List<LagCompensatedHit> _hits = new List<LagCompensatedHit>();

    public int VfxDictionaryId;
    public VFXContext VfxContext;
    public ModifierType VfxModType;

    public void InitTick(SpellCreatedCore core)
    {
        // Record the exact tick this trigger was created (Rollback friendly!)
        core.SetInt(StartTickMemoryIndex, core.Runner.Tick);
    }

    public bool Tick(SpellCreatedCore core, float deltaTime, out List<SpellTriggerInfo> hitInfos)
    {
        hitInfos = new List<SpellTriggerInfo>();
        _hits.Clear();

        // 1. Check if the Field/Blast has expired
        int startTick = core.GetInt(StartTickMemoryIndex);
        if (TickDuration > 0 && (core.Runner.Tick - startTick) >= TickDuration)
        {
            return false;
        }

        // 2. Lag-Compensated Spatial Query
        int hitCount = core.Runner.LagCompensation.OverlapSphere(
            core.transform.position,
            Radius,
            core.Object.InputAuthority,
            _hits,
            SpellSystemHelpers.GeneralCollisionLayerMask(),
            HitOptions.IncludePhysX
        );

        // 3. Fetch the networked State
        SpellState activeState = null;
        if (core.ActiveCastID.IsValid)
        {
            ActiveSpell activeSpell = SpellStateManager.instance.GetActiveSpell(core.ActiveCastID);
            if (activeSpell != null) activeState = activeSpell.State;
        }

        // 4. Process Hits
        for (int i = 0; i < hitCount; i++)
        {
            GameObject targetObj = _hits[i].GameObject;

            // Ignore hitting ourselves
            if (targetObj == core.gameObject) continue;

            // Evaluate Filters
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
                Vector3 hitPos = _hits[i].Point;
                Vector3 hitNormal = _hits[i].Normal;
                Quaternion hitRot = hitNormal.sqrMagnitude > 0 ? Quaternion.LookRotation(hitNormal) : Quaternion.identity;

                hitInfos.Add(new SpellTriggerInfo(
                    isCast: false,
                    source: core.gameObject,
                    state: activeState,
                    position: hitPos,
                    rotation: hitRot,
                    triggerVector: hitNormal,
                    hitObject: targetObj
                ));
            }
        }

        // If we hit anything, return true so the Core executes the Effects!
        return hitInfos.Count > 0;
    }

    public void TickVFX(SpellCreatedCore core)
    {
        if(!core.HasStateAuthority && !core.HasInputAuthority)
        {
           // Debug.Log("VFX are running render on a proxy");
        }

        if (VfxContext == null) return;

        bool shouldBeActive = false;

        if (core.IsActiveInBuffer)
        {
            int startTick = core.GetInt(StartTickMemoryIndex);

            if (TickDuration == 0 || (core.Runner.Tick - startTick) < TickDuration)
            {
                shouldBeActive = true;
            }
        }

        bool currentlyExists = core.ActiveVisuals.TryGetValue(VfxDictionaryId, out GameObject currentVfx);

        if (shouldBeActive && !currentlyExists)
        {
            GameObject newVfx = SpellSystemHelpers.CreateVFX(VfxContext, VfxModType, core.transform, Radius, true);
            if (newVfx != null) core.ActiveVisuals[VfxDictionaryId] = newVfx;
        }
        else if (!shouldBeActive && currentlyExists)
        {
            if (currentVfx != null) GameObject.Destroy(currentVfx);
            core.ActiveVisuals.Remove(VfxDictionaryId);
        }
    }

    public void CleanupVFX(SpellCreatedCore core)
    {
        if (core.ActiveVisuals.TryGetValue(VfxDictionaryId, out GameObject currentVfx))
        {
            if (currentVfx != null) GameObject.Destroy(currentVfx);
            core.ActiveVisuals.Remove(VfxDictionaryId);
        }
    }
}