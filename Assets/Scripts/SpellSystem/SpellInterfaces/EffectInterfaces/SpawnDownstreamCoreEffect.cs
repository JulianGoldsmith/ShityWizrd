using Fusion;
using UnityEngine;
using System.Collections.Generic;
public class SpawnDownstreamCoreEffect : IEffect
{
    public NetworkPrefabRef ChildPrefabRef;
    public CoreExecutionPlan ChildPlan;
    public string ChildNodeGuid;
    public void Execute(SpellCreatedCore parentCore, List<SpellTriggerInfo> hitInfos)
    {
        // 1. Find the Buffer using the ID we permanently stamped into the parent's Context!
        NetworkObjectBuffer activeBuffer = null;
        if (parentCore.Context.BufferSourceID.IsValid)
        {
            if (parentCore.Runner.TryFindObject(parentCore.Context.BufferSourceID, out NetworkObject bufferObj))
            {
                activeBuffer = bufferObj.GetComponent<NetworkObjectBuffer>();
            }
        }

        // 2. Loop through EVERY target hit in this exact tick!
        foreach (var hit in hitInfos)
        {
            NetworkObject newObj = null;

            // 3. Try to pull the child core from the buffer first
            if (activeBuffer != null)
            {
                newObj = activeBuffer.Get(ChildPrefabRef, hit.TriggerPoint, hit.TriggerRotation);
            }

            // 4. Fallback to Spawn (Bulletproofed against Client-Side crashes!)
            if (newObj == null)
            {
                if (parentCore.Object.HasStateAuthority)
                {
                    newObj = parentCore.Runner.Spawn(
                        ChildPrefabRef,
                        hit.TriggerPoint,
                        hit.TriggerRotation,
                        parentCore.Object.InputAuthority
                    );
                }
                else
                {
                    // We are a predicting client, but the buffer was empty!
                    // Safely skip this specific hit rather than crashing Fusion.
                    continue;
                }
            }

            // 5. Initialize the new child core!
            if (newObj != null && newObj.TryGetComponent<SpellCreatedCore>(out var childCore))
            {
                CoreContext childContext = new CoreContext()
                {
                    SpawnPosition = hit.TriggerPoint,
                    CastChargeLevel = parentCore.Context.CastChargeLevel,
                    TriggerVector = hit.TriggerVector,
                    AliveTime = 0f,

                    // CRITICAL: Hand the Buffer ID down to the grandchild!
                    BufferSourceID = parentCore.Context.BufferSourceID,

                    // Pass the active state reference down
                    //State = hit.State
                };

                // Boot up the child core using the pre-compiled plan
                childCore.Initialize(
                    parentCore.ActiveCastID,
                    parentCore.BlueprintID,
                    ChildNodeGuid,
                    childContext, 
                    0
                );
            }
        }
    }
}

public class ExecuteDownstreamCoreEffect : IEffect
{
    public ObjectCore CoreToExecute;

    public void Execute(SpellCreatedCore parentCore, List<SpellTriggerInfo> hitInfos)
    {
        foreach (var hit in hitInfos)
        {
            // Route perfectly through your existing buffer, context, GUID, 
            // and physics initialization logic!
            //CoreToExecute.CreateSpellCore(hit);
        }
    }
}