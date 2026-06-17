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
        /*OverlapSphereST sphereChecker = spellCore.AddComponent<OverlapSphereST>();
        sphereChecker.state = state;
        sphereChecker.filterNodes = this.filterNodes;
        sphereChecker.outcomeNodes = this.outcomeNodes;
        sphereChecker.size = size;
        sphereChecker.singleTrigger = singleTrigger;    

        OnAttach(sphereChecker, size);*/
    }
}

public class OverlapSphereTrigger : RuntimeTriggerBase
{
    // Required by ITrigger
    public TriggerExecutioPlan Plan { get; set; }

    public RuntimeFloatProperty Radius;
    public int TickDuration;
    public int StartTickMemoryIndex;

    // Baked in by the Compiler
    public FilterNode[] Filters;

    public int VfxDictionaryId;
    public VFXContext VfxContext;
    public ModifierType VfxModType;

    public override void InitTick(SpellCreatedCore core)
    {
        // Record the exact tick this trigger was created (Rollback friendly!)
        core.SetInt(StartTickMemoryIndex, core.Runner.Tick);
    }

    private Collider[] _overlapResults = new Collider[64];

    public override bool Tick(SpellCreatedCore core, float deltaTime, out List<SpellTriggerInfo> hitInfos)
    {
        hitInfos = new List<SpellTriggerInfo>();

        // 1. Check if the Field/Blast has expired
        int startTick = core.GetInt(StartTickMemoryIndex);
        if (TickDuration > 0 && (core.Runner.Tick - startTick) >= TickDuration)
        {
            return false;
        }

        // 2. Standard Physics Spatial Query via the Runner's Physics Scene
        // This ensures it works perfectly in Host Mode, Multi-Peer, and local prediction.
        PhysicsScene physicsScene = core.Runner.GetPhysicsScene();

        int hitCount = physicsScene.OverlapSphere(
            core.transform.position,
            Radius.GetValue(default),
            _overlapResults,
            SpellSystemHelpers.GeneralCollisionLayerMask(),
            QueryTriggerInteraction.UseGlobal
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
            GameObject targetObj = _overlapResults[i].gameObject;

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
    
                Collider col = _overlapResults[i];
                Vector3 hitPos = col.ClosestPoint(core.transform.position);
                Vector3 hitNormal = (hitPos - core.transform.position).normalized;

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

        // Clear the array references to avoid memory leaks of destroyed objects
        Array.Clear(_overlapResults, 0, hitCount);

        return hitInfos.Count > 0;
    }

    public override void TickVFX(SpellCreatedCore core)
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
            GameObject newVfx = SpellSystemHelpers.CreateVFX(VfxContext, VfxModType, core.transform, Radius.GetValue(default), true);
            if (newVfx != null) core.ActiveVisuals[VfxDictionaryId] = newVfx;
        }
        else if (!shouldBeActive && currentlyExists)
        {
            if (currentVfx != null) GameObject.Destroy(currentVfx);
            core.ActiveVisuals.Remove(VfxDictionaryId);
        }
    }

    public override void CleanupVFX(SpellCreatedCore core)
    {
        if (core.ActiveVisuals.TryGetValue(VfxDictionaryId, out GameObject currentVfx))
        {
            if (currentVfx != null) GameObject.Destroy(currentVfx);
            core.ActiveVisuals.Remove(VfxDictionaryId);
        }
    }
}