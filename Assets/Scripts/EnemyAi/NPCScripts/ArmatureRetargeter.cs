using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[DefaultExecutionOrder(+10)]
public class ArmatureRetargeter : MonoBehaviour
{
    [Tooltip("Root transform of the armature that has the source animations (animationToTargetArmature).")]
    public Transform animationSourceArmature;

    [Tooltip("Optional visual container scaled once for the whole creature. Auto-detected when left empty.")]
    public Transform creatureVisualScaleRoot;

    [Tooltip("list of bones to be retargeted. Populated by the 'Map Bones by Name' context menu")]
    public List<RetargetedBone> retargetedBones = new List<RetargetedBone>();

    public Vector3 animatedHipRootMotion = Vector3.zero;
    public Quaternion animatedHipRotation = Quaternion.identity;

    public bool disableRetargetingToProxys = false;
    public float lerpTProxy = 0;
    public bool overRideAndInjectAnimatedHipsRootMotionToAll = false;
    public bool overRideAndKillInjectedMotion = false;

    public bool readRootMotion = true;

    // --- DICTIONARY CACHE FOR VIRTUAL PARENTING ---
    private Dictionary<Transform, RetargetedBone> _sourceToBoneMap;
    private NPCActiveRagdollController _creatureScaleProvider;
    private Vector3 _referenceVisualScale = Vector3.one;

    [System.Serializable]
    public class RetargetedBone
    {
        public Transform sourceBone;
        public Transform targetBone;
        public Transform physicsProxy;
        public bool enabled = true;
        public bool ragDollBone = false;
        public bool injectAnimatedHipsRootMotion = false;
        [System.NonSerialized] public PhysicsObjectProperties physicsProperties;
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

            if(b.physicsProxy != null)
            {
                PhysicsObject po = b.physicsProxy.TryGetComponent<PhysicsObject>(out po) ? po:
                    b.physicsProxy.TryGetComponent<LocalSmoothingForNetworkedRenderTarget>(out var lsnrt)? lsnrt.target.parent.GetComponent<PhysicsObject>() : null;


                if (po != null)
                {
                    b.physicsProperties = po.GetComponent<PhysicsObjectProperties>();
                    po.nonChildRenderers.Add(b.targetBone.GetComponent<Renderer>());
                    Renderer[] renderers = b.targetBone.GetComponentsInChildren<Renderer>();
                    foreach (var r in renderers)
                    {
                        if(!po.nonChildRenderers.Contains(r))
                            po.nonChildRenderers.Add(r);
                    }
                }
            }
        }

        _creatureScaleProvider = GetComponentInParent<NPCActiveRagdollController>();
        if (creatureVisualScaleRoot == null && _creatureScaleProvider != null && retargetedBones.Count > 0 && retargetedBones[0].targetBone != null)
        {
            creatureVisualScaleRoot = retargetedBones[0].targetBone;
            while (creatureVisualScaleRoot.parent != null && creatureVisualScaleRoot.parent != _creatureScaleProvider.transform) creatureVisualScaleRoot = creatureVisualScaleRoot.parent;
        }

        if (creatureVisualScaleRoot != null) _referenceVisualScale = creatureVisualScaleRoot.localScale;
       
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

    private Vector3 GetProxyTargetScale(RetargetedBone bone, float creatureScale)
    {
        if (bone.physicsProperties != null && bone.sourceBone != null)
        {
            Vector3 initialScale = bone.physicsProperties.InitialEditorScale;
            Vector3 currentScale = bone.physicsProperties.transform.localScale;
            Vector3 totalScaleMultiplier = new Vector3(currentScale.x / Mathf.Max(0.0001f, initialScale.x), currentScale.y / Mathf.Max(0.0001f, initialScale.y), currentScale.z / Mathf.Max(0.0001f, initialScale.z));
            Vector3 boneOnlyScaleMultiplier = totalScaleMultiplier / creatureScale;
            return Vector3.Scale(bone.sourceBone.localScale, boneOnlyScaleMultiplier);
        }

        return bone.physicsProxy != null ? bone.physicsProxy.localScale / creatureScale : bone.sourceBone.localScale;
    }

    void LateUpdate()
    {
        if (retargetedBones == null || retargetedBones.Count == 0) return;

        float creatureScale = _creatureScaleProvider != null ? _creatureScaleProvider.CurrentCreatureScale : 1f;
        creatureScale = Mathf.Max(0.01f, creatureScale);
        if (creatureVisualScaleRoot != null) creatureVisualScaleRoot.localScale = _referenceVisualScale * creatureScale;

        var animatedRootMotion = animatedHipRootMotion * (1-lerpTProxy);
       

        // 1. Process Hips (Root)
        var rootBone = retargetedBones[0];
        if (rootBone.physicsProxy != null && !disableRetargetingToProxys)
        {
            rootBone.targetBone.SetPositionAndRotation(rootBone.physicsProxy.position, rootBone.physicsProxy.rotation);
            rootBone.targetBone.localScale = GetProxyTargetScale(rootBone, creatureScale);

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
                    Vector3 targetScale = Vector3.Lerp(bone.sourceBone.localScale, GetProxyTargetScale(bone, creatureScale), lerpTProxy);

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
                    bone.targetBone.localScale = GetProxyTargetScale(bone, creatureScale);
                }
                else
                {
                    // Force the explicit world projection, completely bypassing transform hierarchy bugs
                    bone.targetBone.SetPositionAndRotation(projectedPos, projectedRot);
                    bone.targetBone.localScale = bone.sourceBone.localScale;
                }
            }

            if ((injectRootMotion) && hasActiveProxy && !bone.ragDollBone)
            {
                bone.targetBone.position += animatedRootMotion;
            }
        }
    }
}
