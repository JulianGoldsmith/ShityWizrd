using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;


public class CharacterBonkController : NetworkBehaviour
{
    [HideInInspector] public HybridCharacterController characterController;

    [Networked, OnChangedRender(nameof(OnBonkedStateChanged))] public BONKEDSTATE BonkedState { get; set; }
    [HideInInspector] int _swapAtTick = -1;
    [HideInInspector] bool wasKinematic;

    public Transform ragDollHips;
    public GameObject ragDoll;
    public NetworkedRagDoll ragDollController;
    public Rig aliveRig, bonkedRig;

    [Header("Skeletons")]
    public Transform animatedSkeletonRoot;
    public Transform ragdollSkeletonRoot;

    [Header("Renderers to Rebind")]
    private CharacterCustomization characterCustomization;
    //public List<SkinnedMeshRenderer> clothingRenderers;

    private Dictionary<SkinnedMeshRenderer, Transform[]> animatedBoneMap;
    private Dictionary<SkinnedMeshRenderer, Transform[]> ragdollBoneMap;

    public override void Spawned()
    {
        characterCustomization = this.GetComponent<CharacterCustomization>();
        characterController = this.GetComponent<HybridCharacterController>();
        ragDollController = transform.GetComponent<NetworkedRagDoll>();
        
        BuildBoneMaps();
        
        if (BonkedState == BONKEDSTATE.ALIVE)
        {
            ragDollController.DeactivateRagDoll();
        }
        else
        {
            ragDollController.ActivateRagDoll();
        }
    }

    public void OnBonkedStateChanged()
    {
        if (HasStateAuthority) return;

        if (BonkedState == BONKEDSTATE.BONKED)
        {
            GetBonked();
        }
        else
        {
            GetUnBonked();
        }
    }

    public void GetBonked()
    {

        Debug.Log("Ran got bonked");
        ragDollController.ActivateRagDoll();

        foreach (PdBone headAndTorso in characterController.pdBones)
        {
            var rb3d = headAndTorso.childRigidbody.transform.GetComponent<NetworkRigidbody3D>();
            //headAndTorso.wasKinematicOnDisable = rb3d.RBIsKinematic;
            rb3d.RBIsKinematic = true;
            rb3d.GetComponent<Collider>().enabled = false;
        }
        var hipsNRB = characterController.hipsRb.GetComponent<NetworkRigidbody3D>();
        wasKinematic = hipsNRB.RBIsKinematic;
        hipsNRB.RBIsKinematic = true;
        hipsNRB.GetComponent<Collider>().enabled = false;

        if (HasStateAuthority)
        {
            BonkedState = BONKEDSTATE.BONKED;
        }
        _swapAtTick = Runner.Tick + 1;

        characterController.handController.DisableHands();

    }

    public void GetUnBonked()
    {
        ragDollController.DeactivateRagDoll();
        if (HasStateAuthority)
        {
            BonkedState = BONKEDSTATE.ALIVE;
        }
        _swapAtTick = Runner.Tick + 1;


        foreach (PdBone headAndTorso in characterController.pdBones)
        {
            var rb3d = headAndTorso.childRigidbody.transform.GetComponent<NetworkRigidbody3D>();
            rb3d.RBIsKinematic = false;
            rb3d.GetComponent<Collider>().enabled = true;

        }
        var hipsNRB = characterController.hipsRb.GetComponent<NetworkRigidbody3D>();
        hipsNRB.RBIsKinematic = false;
        hipsNRB.GetComponent<Collider>().enabled = true;


        characterController.handController.EnableHands();
    }

    public override void Render()
    {
        if (_swapAtTick >= 0 && Runner.Tick >= _swapAtTick)
        {
            _swapAtTick = -1;
            bool showRagdoll = (BonkedState == BONKEDSTATE.BONKED);


            if (showRagdoll)
            {
                SwitchToRagdoll();
            }
            else
            {
                SwitchToAnimated();
            }
            //characterController.modelRenderer.enabled = HasInputAuthority ? false : !showRagdoll;
            //characterController.ragDollRenderer.enabled = showRagdoll;
        }
    }



    public void SwitchToRagdoll()
    {
        characterController.modelRenderer.enabled = false;
        characterController.ragDollRenderer.enabled = true;

        foreach (SkinnedMeshRenderer renderer in characterCustomization.apparel)
        {
            if (ragdollBoneMap.ContainsKey(renderer))
            {
                renderer.bones = ragdollBoneMap[renderer];
                renderer.rootBone = ragdollSkeletonRoot;
            }
        }
    }

    // Call this from your 'GetUnBonked' method
    public void SwitchToAnimated()
    {
        characterController.modelRenderer.enabled = HasInputAuthority ? false : true;
        characterController.ragDollRenderer.enabled = false;
        foreach (SkinnedMeshRenderer renderer in characterCustomization.apparel)
        {
            if (animatedBoneMap.ContainsKey(renderer))
            {
                renderer.bones = animatedBoneMap[renderer];
                renderer.rootBone = animatedSkeletonRoot;
            }
        }
    }

    private void BuildBoneMaps()
    {
        animatedBoneMap = new Dictionary<SkinnedMeshRenderer, Transform[]>();
        ragdollBoneMap = new Dictionary<SkinnedMeshRenderer, Transform[]>();

        var animatedBones = animatedSkeletonRoot.GetComponentsInChildren<Transform>().ToDictionary(b => b.name);
        var ragdollBones = ragdollSkeletonRoot.GetComponentsInChildren<Transform>().ToDictionary(b => b.name);

        foreach (SkinnedMeshRenderer renderer in characterCustomization.apparel)
        {
            var animBones = new Transform[renderer.bones.Length];
            var ragBones = new Transform[renderer.bones.Length];

            for (int i = 0; i < renderer.bones.Length; i++)
            {
                string boneName = renderer.bones[i].name;

                // Find the matching bone in both skeletons
                if (animatedBones.TryGetValue(boneName, out var animBone))
                {
                    animBones[i] = animBone;
                }
                if (ragdollBones.TryGetValue(boneName, out var ragdollBone))
                {
                    ragBones[i] = ragdollBone;
                }
            }

            // Store the pre-calculated arrays in our dictionaries
            animatedBoneMap[renderer] = animBones;
            ragdollBoneMap[renderer] = ragBones;
        }
    }
}