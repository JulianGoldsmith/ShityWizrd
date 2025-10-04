using UnityEngine;
using System.Collections.Generic;

public class BoneMapper : MonoBehaviour
{
    private Transform targetAnimator, finalAnimator;
    private List<BoneMapping> boneMappings = new List<BoneMapping>();
    private class BoneMapping { public Transform source; public Transform destination; }
    public bool syncEnabled = false;

    public void Spawn(bool enabled, Transform t, Transform f)
    {
        targetAnimator = t; finalAnimator = f;
        syncEnabled = enabled;
        InitializeBoneMappings();
    }


    void Update()
    {
        if (!syncEnabled) return;

        foreach (var map in boneMappings)
        {
            map.destination.localPosition = map.source.localPosition;
            map.destination.localRotation = map.source.localRotation;
        }
    }

    private void InitializeBoneMappings()
    {
        var sourceArmatureRoot = targetAnimator;

        var sourceBones = sourceArmatureRoot.GetComponentsInChildren<Transform>();
        var sourceBoneMap = new Dictionary<string, Transform>(sourceBones.Length);
        foreach (var sourceBone in sourceBones)
        {
            sourceBoneMap[sourceBone.name] = sourceBone;
        }

        var finalArmatureRoot = finalAnimator;
        var destBones = finalArmatureRoot.transform.GetComponentsInChildren<Transform>();

        // 3. Iterate through our destination bones and find the matching source bone.
        foreach (var destBone in destBones)
        {
            if (sourceBoneMap.TryGetValue(destBone.name, out Transform sourceBone))
            {
                boneMappings.Add(new BoneMapping { source = sourceBone, destination = destBone });
            }
        }
    }
}