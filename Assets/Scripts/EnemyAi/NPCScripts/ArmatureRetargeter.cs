using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[DefaultExecutionOrder(+10)]
public class ArmatureRetargeter : MonoBehaviour
{
    [Tooltip("Root transform of the armature that has the source animations (animationToTargetArmature).")]
    public Transform animationSourceArmature;

    [Tooltip("list of bones to be retargeted. Populated by the 'Map Bones by Name' context menu")]
    public List<RetargetedBone> retargetedBones = new List<RetargetedBone>();

    public Vector3 animatedHipRootMotion = Vector3.zero;
    public Quaternion animatedHipRotation = Quaternion.identity;

    public bool disableRetargetingToProxys = false;
    public float lerpTProxy = 0;
    public bool overRideAndInjectAnimatedHipsRootMotionToAll = false;
    public bool overRideAndKillInjectedMotion = false;

    // --- DICTIONARY CACHE FOR VIRTUAL PARENTING ---
    private Dictionary<Transform, RetargetedBone> _sourceToBoneMap;

    [System.Serializable]
    public class RetargetedBone
    {
        public Transform sourceBone;
        public Transform targetBone;
        public Transform physicsProxy;
        public bool enabled = true;
        public bool ragDollBone = false;
        public bool injectAnimatedHipsRootMotion = false;
    }

    void Awake()
    {
        // Build the fast-lookup dictionary on startup
        _sourceToBoneMap = new Dictionary<Transform, RetargetedBone>();
        foreach (var b in retargetedBones)
        {
            if (b.sourceBone != null)
            {
                _sourceToBoneMap[b.sourceBone] = b;
            }
        }
    }

    [ContextMenu("Map Bones by Name")]
    private void MapBonesByName()
    {
        if (animationSourceArmature == null)
        {
            Debug.LogError("Animation Source Armature is not assigned. Cannot map bones.", this);
            return;
        }

        Debug.Log("Mapping bones...");
        retargetedBones.Clear();

        var targetBones = this.GetComponentsInChildren<Transform>();
        var targetBoneMap = targetBones.ToDictionary(t => t.name, t => t);

        // This recursive function guarantees Top-to-Bottom order!
        AddBoneAndChildrenRecursive(animationSourceArmature, targetBoneMap);

        Debug.Log($"Successfully mapped {retargetedBones.Count} bones. Please assign Physics Proxies manually.");
    }

    private void AddBoneAndChildrenRecursive(Transform sourceBone, Dictionary<string, Transform> targetBoneMap)
    {
        if (targetBoneMap.TryGetValue(sourceBone.name, out Transform matchingTargetBone))
        {
            retargetedBones.Add(new RetargetedBone
            {
                sourceBone = sourceBone,
                targetBone = matchingTargetBone,
                physicsProxy = null
            });
        }
        else
        {
            Debug.LogWarning($"Could not find matching target bone for source bone: {sourceBone.name}", this);
        }

        foreach (Transform child in sourceBone)
        {
            AddBoneAndChildrenRecursive(child, targetBoneMap);
        }
    }

    public void SetRagdollBlend(float blendToProxy)
    {
        lerpTProxy = Mathf.Clamp01(blendToProxy);
    }

    void LateUpdate()
    {
        if (retargetedBones == null || retargetedBones.Count == 0) return;

        var animatedRootMotion = animatedHipRootMotion * (1-lerpTProxy);
       

        // 1. Process Hips (Root)
        var rootBone = retargetedBones[0];
        if (rootBone.physicsProxy != null && !disableRetargetingToProxys)
        {
            rootBone.targetBone.SetPositionAndRotation(rootBone.physicsProxy.position, rootBone.physicsProxy.rotation);
            rootBone.targetBone.localScale = rootBone.physicsProxy.localScale;

            if (rootBone.injectAnimatedHipsRootMotion || overRideAndInjectAnimatedHipsRootMotionToAll)
            {
                rootBone.targetBone.position += animatedRootMotion;
            }
        }
        else if (rootBone.sourceBone != null)
        {
            rootBone.targetBone.SetPositionAndRotation(rootBone.sourceBone.position, rootBone.sourceBone.rotation);
            rootBone.targetBone.localScale = rootBone.sourceBone.localScale;
        }

        for (int i = 1; i < retargetedBones.Count; i++)
        {
            var bone = retargetedBones[i];
            if (!bone.enabled) continue;

            bool hasActiveProxy = bone.physicsProxy != null && !disableRetargetingToProxys;
            bool injectRootMotion = ((bone.injectAnimatedHipsRootMotion || overRideAndInjectAnimatedHipsRootMotionToAll) && (!overRideAndKillInjectedMotion));

            Vector3 projectedPos = bone.sourceBone.position;
            Quaternion projectedRot = bone.sourceBone.rotation;

            if (bone.sourceBone.parent != null && _sourceToBoneMap.TryGetValue(bone.sourceBone.parent, out var parentBoneData))
            {
                projectedPos = parentBoneData.targetBone.TransformPoint(bone.sourceBone.localPosition);
                projectedRot = parentBoneData.targetBone.rotation * bone.sourceBone.localRotation;
            }

            if (bone.ragDollBone)
            {
                if (hasActiveProxy)
                {
                    Vector3 proxyPos = bone.physicsProxy.position;
                    Quaternion proxyRot = bone.physicsProxy.rotation;

                    Vector3 targetPos = Vector3.Lerp(projectedPos, proxyPos, lerpTProxy);
                    Quaternion targetRot = Quaternion.Slerp(projectedRot, proxyRot, lerpTProxy);
                    Vector3 targetScale = Vector3.Lerp(bone.sourceBone.localScale, bone.physicsProxy.localScale, lerpTProxy);

                    bone.targetBone.SetPositionAndRotation(targetPos, targetRot);
                    bone.targetBone.localScale = targetScale;
                }
                else
                {
                    bone.targetBone.SetPositionAndRotation(projectedPos, projectedRot);
                    bone.targetBone.localScale = bone.sourceBone.localScale;
                }
            }
            else
            {
                if (hasActiveProxy)
                {
                    bone.targetBone.SetPositionAndRotation(bone.physicsProxy.position, bone.physicsProxy.rotation);
                    bone.targetBone.localScale = bone.physicsProxy.localScale;
                }
                else
                {
                    // Force the explicit world projection, completely bypassing transform hierarchy bugs
                    bone.targetBone.SetPositionAndRotation(projectedPos, projectedRot);
                    bone.targetBone.localScale = bone.sourceBone.localScale;
                }
            }

            if ((injectRootMotion) && hasActiveProxy)
            {
                bone.targetBone.position += animatedRootMotion;
            }
        }
    }
}