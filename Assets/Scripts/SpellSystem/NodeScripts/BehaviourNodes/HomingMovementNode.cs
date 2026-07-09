using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HomingMovementNode", menuName = "SpellNodes/Behaviour/HomingMovementNode")]
public class HomingMovementNode : BehaviourNode
{
    [Promotable("Turn Rate Speed", DataTypeTag.Speed)]
    public float turn_rate = 1.0f;
    public bool maintain_target = true; // keep same target until out of range.
    public float search_range = 5.0f;
    public float min_speed = 1.0f;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        int targetMemorySlot = context.ClaimIntSlot();

        return new HomingBehaviour()
        {
            TurnRate = new RuntimeFloatProperty(this.turn_rate),
            MaintainTarget = maintain_target,
            SearchRange = search_range,
            MinSpeed = min_speed,
            TargetMemoryIndex = targetMemorySlot
        };
    }

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        /*var homingMovement = spellCore.AddComponent<HomingMovementSB>();
        homingMovement.triggerInfo = triggerInfo;
        homingMovement.turn_rate = turn_rate;
        homingMovement.maintain_target = maintain_target;
        homingMovement.search_range = search_range;
        homingMovement.min_speed = min_speed;

        homingMovement.OnAttach(this);*/
    }
}

public class HomingBehaviour : IBehaviour
{
    public RuntimeFloatProperty TurnRate;
    public bool MaintainTarget;
    public float SearchRange;
    public float MinSpeed;

    public int TargetMemoryIndex;

    public void InitTick(ISpellExecutionCore core)
    {
        core.SetInt(TargetMemoryIndex, 0);
    }

    public void Tick(ISpellExecutionCore core, float deltaTime)
    {
        if (!core.TryGetCoreComponent<IMovementHandler>(out var mover)) return;

        NetworkObject targetNetObj = null;

        uint rawId = (uint)core.GetInt(TargetMemoryIndex);
        NetworkId currentTargetId = new NetworkId() { Raw = rawId };

        if (currentTargetId.IsValid)
        {
            core.Runner.TryFindObject(currentTargetId, out targetNetObj);
        }

        bool needsNewTarget = targetNetObj == null ||
                              !MaintainTarget ||
                              Vector3.Distance(core.Position, targetNetObj.transform.position) > SearchRange;

        if (needsNewTarget)
        {
            targetNetObj = FindNewTarget(core);

            if (targetNetObj != null)
            {
                core.SetInt(TargetMemoryIndex, (int)targetNetObj.Id.Raw);
            }
            else
            {
                core.SetInt(TargetMemoryIndex, 0); // Clear memory if nothing found
            }
        }

        if (targetNetObj != null)
        {
            Vector3 currentVel = mover.CurrentVelocity;
            Vector3 targetDir = (targetNetObj.transform.position - core.Position).normalized;

            Vector3 desiredVel = targetDir * Mathf.Max(MinSpeed, currentVel.magnitude);

            if (currentVel.magnitude > 0.0001f || MinSpeed > 0)
            {
                Vector3 newVel = Vector3.RotateTowards(currentVel, desiredVel, TurnRate.GetValue(default) * Mathf.Deg2Rad * deltaTime, float.MaxValue);

                mover.ApplyForce(newVel - currentVel, ForceMode.VelocityChange);
            }
        }
    }

    private NetworkObject FindNewTarget(ISpellExecutionCore core)
    {
        // Use Fusion's Lag-Compensated sphere to safely check the past/future during rollbacks
        List<LagCompensatedHit> hits = new List<LagCompensatedHit>();
        int hitCount = core.Runner.LagCompensation.OverlapSphere(
            core.Position,
            SearchRange,
            core.InputAuthority,
            hits,
            SpellSystemHelpers.GeneralCollisionLayerMask()
        );

        for (int i = 0; i < hitCount; i++)
        {
            var hitObj = hits[i].GameObject;

            if (hitObj == core.SourceObject) continue;

            if (hitObj.TryGetComponent<NetworkObject>(out var netObj))
            {
                if (netObj.Id == core.Context.OriginalCaster) continue;

                return netObj; 
            }
        }
        return null;
    }

    public void CleanupVFX(ISpellExecutionCore core)
    {
    }
    public void TickVFX(ISpellExecutionCore core)
    {
    }
}