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

    public float lerpTProxy = 0; // value of lerp from "target" to "prxy"

    public bool overRideAndInjectAnimatedHipsRootMotionToAll = false;

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
        if (retargetedBones == null || retargetedBones.Count == 0)
        {
            return;
        }

        

        var rootBone = retargetedBones[0];
        if (rootBone.physicsProxy != null && !disableRetargetingToProxys)
        {
            rootBone.targetBone.SetPositionAndRotation(rootBone.physicsProxy.position, rootBone.physicsProxy.rotation);
            rootBone.targetBone.localScale = rootBone.physicsProxy.localScale;

            if ((rootBone.injectAnimatedHipsRootMotion || overRideAndInjectAnimatedHipsRootMotionToAll))
            {
                rootBone.targetBone.position += animatedHipRootMotion;
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

            if (bone.ragDollBone)
            {
                if (bone.physicsProxy != null && !disableRetargetingToProxys)
                {
                    Vector3 sourcePos = bone.sourceBone.position;
                    Quaternion sourceRot = bone.sourceBone.rotation;
                    Vector3 proxyPos = bone.physicsProxy.position;
                    Quaternion proxyRot = bone.physicsProxy.rotation;

                    Vector3 targetPos = Vector3.Lerp(sourcePos, proxyPos, lerpTProxy);
                    Quaternion targetRot = Quaternion.Slerp(sourceRot, proxyRot, lerpTProxy);

                    Vector3 targetScale = Vector3.Lerp(bone.sourceBone.localScale, bone.physicsProxy.localScale, lerpTProxy);

                    bone.targetBone.SetPositionAndRotation(targetPos, targetRot);
                    bone.targetBone.localScale = targetScale;
                }
                else
                {
                    bone.targetBone.SetLocalPositionAndRotation(bone.sourceBone.localPosition, bone.sourceBone.localRotation);
                    bone.targetBone.localScale = bone.sourceBone.localScale;
                }
            }
            else
            {
                if (bone.physicsProxy != null && !disableRetargetingToProxys)
                {
                    bone.targetBone.SetPositionAndRotation(bone.physicsProxy.position, bone.physicsProxy.rotation);
                    bone.targetBone.localScale = bone.physicsProxy.localScale;
                }
                else
                {
                    bone.targetBone.SetLocalPositionAndRotation(bone.sourceBone.localPosition, bone.sourceBone.localRotation);
                    bone.targetBone.localScale = bone.sourceBone.localScale;
                    //Debug.Log($"bone is at local {bone.sourceBone.name} local pos {bone.sourceBone.localPosition}");
                }
            }

            //Vector3 targetPos = Vector3.zero;
            //Quaternion targetRot = Quaternion.identity;
            //if (bone.physicsProxy != null && !disableRetargetingToProxys)
            //{
            //    targetPos = bone.physicsProxy.position;
            //    targetRot= bone.physicsProxy.rotation;
            //    bone.targetBone.SetPositionAndRotation(targetPos, targetRot);



            //}
            //else if (bone.sourceBone != null)
            //{
            //    targetPos = bone.sourceBone.localPosition;
            //    targetRot = bone.sourceBone.localRotation;
            //    bone.targetBone.SetLocalPositionAndRotation(targetPos, targetRot);
            //}

            if ((bone.injectAnimatedHipsRootMotion || overRideAndInjectAnimatedHipsRootMotionToAll ) && hasActiveProxy)
            {
                bone.targetBone.position += animatedHipRootMotion;
            }
            
        }
    }
}
