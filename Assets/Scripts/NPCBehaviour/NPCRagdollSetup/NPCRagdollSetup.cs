using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NPCRagdollSetup : MonoBehaviour
{
    public enum ColliderShape { Capsule, Sphere, Box }
    public enum CapsuleAxis { X, Y, Z }

    [System.Serializable]
    public class BoneSetup
    {
        public Transform bone;
        public Transform proxy;
        public Transform endpoint;

        public ColliderShape shape = ColliderShape.Capsule;
        public CapsuleAxis axis = CapsuleAxis.Y;
        public Vector3 centre;
        public Vector3 size = Vector3.one * 0.2f;
        public float radius = 0.1f;
        public float height = 0.4f;

        public BoneSetup Copy()
        {
            return (BoneSetup)MemberwiseClone();
        }
    }

    public Transform armatureRoot;
    public Transform coreBone;
    public List<BoneSetup> selectedBones = new List<BoneSetup>();
    public List<Transform> proxiesToRemove = new List<Transform>();
}