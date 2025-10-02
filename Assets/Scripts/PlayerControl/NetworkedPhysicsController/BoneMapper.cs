using UnityEngine;
using System.Collections.Generic;

public class BoneMapper : MonoBehaviour
{
    [SerializeField] private Transform _animationArmatureRoot;
    [SerializeField] private Transform _ikArmatureRoot;

    private Dictionary<string, Transform> _animationBoneMap = new Dictionary<string, Transform>();
    private Dictionary<string, Transform> _ikBoneMap = new Dictionary<string, Transform>();

    void Awake()
    {
        // Populate the dictionaries by scanning the hierarchies
        MapBonesRecursively(_animationArmatureRoot, _animationBoneMap);
        MapBonesRecursively(_ikArmatureRoot, _ikBoneMap);
    }

    private void MapBonesRecursively(Transform bone, Dictionary<string, Transform> map)
    {
        map[bone.name] = bone;
        foreach (Transform child in bone)
        {
            MapBonesRecursively(child, map);
        }
    }

    public Transform GetAnimationBone(string boneName)
    {
        _animationBoneMap.TryGetValue(boneName, out Transform bone);
        return bone;
    }

    public Transform GetIKBone(string boneName)
    {
        _ikBoneMap.TryGetValue(boneName, out Transform bone);
        return bone;
    }
}