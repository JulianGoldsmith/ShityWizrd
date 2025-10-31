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

    /// <summary>
    /// Holds the references for a single bone in the retargeting chain.
    /// </summary>
    [System.Serializable]
    public class RetargetedBone
    {
        public Transform sourceBone;

        public Transform targetBone;

        public Transform physicsProxy;

        public bool enabled = true;

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

    void LateUpdate()
    {
        if (retargetedBones == null || retargetedBones.Count == 0)
        {
            return;
        }

        var rootBone = retargetedBones[0];
        if (rootBone.physicsProxy != null)
        {
            rootBone.targetBone.SetPositionAndRotation(rootBone.physicsProxy.position, rootBone.physicsProxy.rotation);
        }
        else if (rootBone.sourceBone != null)
        {
            rootBone.targetBone.SetPositionAndRotation(rootBone.sourceBone.position, rootBone.sourceBone.rotation);
        }

        for (int i = 1; i < retargetedBones.Count; i++)
        {

            var bone = retargetedBones[i];

            if (!bone.enabled) continue;

            Vector3 targetPos = Vector3.zero;
            Quaternion targetRot = Quaternion.identity;
            if (bone.physicsProxy != null)
            {
                targetPos = bone.physicsProxy.position;
                targetRot= bone.physicsProxy.rotation;
                bone.targetBone.SetPositionAndRotation(targetPos, targetRot);
            }
            else if (bone.sourceBone != null)
            {
                targetPos = bone.sourceBone.localPosition;
                targetRot = bone.sourceBone.localRotation;
                bone.targetBone.SetLocalPositionAndRotation(targetPos, targetRot);
            }

            if (bone.injectAnimatedHipsRootMotion)
            {
                bone.targetBone.position += animatedHipRootMotion;
            }
            
        }
    }
}
