using Fusion;
using UnityEngine;
public class SpawnDownstreamCoreEffect : IEffect
{
    public NetworkPrefabRef ChildPrefabRef;
    public CoreExecutionPlan ChildPlan;
    public void Execute(SpellCreatedCore parentCore, SpellTriggerInfo hitInfo)
    {
        NetworkObject newObj = null;

        if (parentCore.Context.BufferSourceID.IsValid &&
            parentCore.Runner.TryFindObject(parentCore.Context.BufferSourceID, out NetworkObject bufferOwner))
        {
            if (bufferOwner.TryGetComponent<NetworkObjectBuffer>(out var buffer))
            {
                // We found the pool! Pull a dormant core from it.
                newObj = buffer.Get(ChildPrefabRef, hitInfo.TriggerPoint, hitInfo.TriggerRotation);
            }
        }

        if (newObj == null && parentCore.Object.HasStateAuthority)
        {
            newObj = parentCore.Runner.Spawn(ChildPrefabRef,hitInfo.TriggerPoint,
            hitInfo.TriggerRotation, parentCore.Object.InputAuthority);
        }

        CoreContext childContext = parentCore.Context;
        childContext.SpawnPosition = hitInfo.TriggerPoint;
        childContext.AliveTime = 0; 

        if (newObj.TryGetComponent<SpellCreatedCore>(out var childCore))
        {
            childCore.Initialize(parentCore.ActiveCastID, parentCore.BlueprintID, ChildPlan, childContext);
        }
    }
}