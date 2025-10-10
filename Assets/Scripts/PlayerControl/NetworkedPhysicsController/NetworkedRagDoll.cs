using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public class NetworkedRagDoll : NetworkBehaviour
{
    [SerializeField]public List<RagDollBone> bones = new List<RagDollBone>();
    public Transform ragdollRoot, targetArmatureRoot;

    public Transform rig;

    public bool active;



    public void Spawn() { }

    public override void Spawned()
    {
        ragdollRoot.parent = null;
        ragdollRoot.GetComponent<NetworkObject>().RemoveInputAuthority();

        
    }

    public void ActivateRagDoll()
    {
        
        foreach (RagDollBone bone in bones)
        {
            bone.WakeBone(HasStateAuthority);
        }
        //foreach (RagDollBone bone in bones)
        //{
        //    bone.rb3d.gameObject.SetActive(true);
        //}
        ragdollRoot.gameObject.SetActive(true);


    }

    public void DeactivateRagDoll()
    {
        
        foreach (RagDollBone bone in bones)
        {
            bone.SleepBone(HasStateAuthority);
        }
        ragdollRoot.gameObject.SetActive(false);
    }

 

    void MapBones()
    {
        var sourceBoneMap = new Dictionary<string, RagDollBone>(bones.Count);
        foreach (var sourceBone in bones)
        {
            sourceBoneMap[sourceBone.rb.transform.name] = sourceBone;
        }

        var destBones = targetArmatureRoot.transform.GetComponentsInChildren<Transform>();

        // 3. Iterate through our destination bones and find the matching source bone.
        foreach (var destBone in destBones)
        {
            if (sourceBoneMap.TryGetValue(destBone.name, out RagDollBone sourceBone))
            {
                sourceBone.targetTransform = destBone.transform;

            }
        }
        var IKRiggers = rig.transform.GetComponentsInChildren<OverrideTransform>();

        foreach (var rig in IKRiggers)
        {
            if (sourceBoneMap.TryGetValue(rig.transform.name, out RagDollBone sourceBone))
            {
                sourceBone.IK = rig;

                
            }
        }

        rig.transform.parent.GetComponent<RigBuilder>().Build();
    }

    

    [System.Serializable]
    public class RagDollBone
    {
        public Rigidbody rb;
        public NetworkObject no;
        public NetworkRigidbody3D rb3d;
        public Rigidbody equivalentbone;

        public bool initiatedKinematic = false;

        public Transform targetTransform;

        public OverrideTransform IK;

        public RagDollBone(Rigidbody _rb, NetworkObject _no, NetworkRigidbody3D _rb3d) { 
            rb = _rb;
            no = _no;
            rb3d = _rb3d;
            
        }

        public void SleepBone(bool _hasStateAuth)
        {
            initiatedKinematic = rb.isKinematic;
            rb3d.RBIsKinematic = true;
            rb.GetComponent<Collider>().enabled = false;

            
            //rb3d.Teleport(new Vector3(0, 1000,0), Quaternion.identity);
        }

        public void WakeBone(bool _hasStateAuth)
        {
            rb3d.Teleport(targetTransform.position, targetTransform.rotation);
            rb3d.RBIsKinematic = !_hasStateAuth;
            rb.GetComponent<Collider>().enabled = true;
            rb.angularVelocity = Vector3.zero;
            rb.linearVelocity = Vector3.zero;
            if (equivalentbone != null)
                rb.AddForce(equivalentbone.linearVelocity, ForceMode.VelocityChange);
            no.RemoveInputAuthority();
            no.ForceRemoteRenderTimeframe = true;
        }

    }
}
