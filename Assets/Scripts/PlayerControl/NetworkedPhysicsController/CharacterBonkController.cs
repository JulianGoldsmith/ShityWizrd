using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using static NetworkedRagDoll;

[DefaultExecutionOrder(100)]
public class CharacterBonkController : NetworkBehaviour
{
    [HideInInspector] public HybridCharacterController characterController;
    public List<StretchyArmIK> armIKs;

    [Networked] public BONKEDSTATE BonkedState { get; set; }
    public bool bonkChangedThisUpdate = false;
    [HideInInspector] int _swapAtTick = -1;
    public GameObject ragdollProxysRoot;

    public ChangeDetector _changeDetector;


    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        characterController = this.GetComponent<HybridCharacterController>();

        if (BonkedState == BONKEDSTATE.ALIVE)
        {
            //DeActivateConfigurableJoint()
            DeactivateRagDoll();
        }
        else
        {
            ActivateRagDoll();
        }

    }

    public override void FixedUpdateNetwork()
    {
        bonkChangedThisUpdate = false;
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(BonkedState):
                    bonkChangedThisUpdate = true;
                    OnBonkedStateChangedFixed();
                    break;
            }
        }
    }

    public void OnBonkedStateChangedFixed()
    {
        //if (HasStateAuthority) return;

        if (BonkedState == BONKEDSTATE.BONKED)
        {
            GetBonked();
        }
        else
        {
            GetUnBonked();
        }
        bonkChangedThisUpdate = true;
    }

    public void GetBonked()
    {
        if (HasStateAuthority)
        {
            BonkedState = BONKEDSTATE.BONKED;
        }
        _swapAtTick = Runner.Tick + 1;

        ActivateRagDoll();
        if (characterController != null) //character 
        {

            foreach(StretchyArmIK ik in armIKs)
            {
                ik.enabled = false;
            }
            characterController.handController.DisableHands();
            characterController.armatureRetargetingLerp = 1;
            //characterController.armatureRetargetingLerp = 1;
            //foreach(PdBone bone in characterController.ragDollBones)
            //{
            //    bone.childRigidbody.MovePosition(bone.targetTransform.position);
            //    bone.childRigidbody.MoveRotation(bone.targetTransform.rotation);
            //}
        }
    }

    public void GetUnBonked()
    {
        //ragDollController.DeactivateRagDoll();
        if (HasStateAuthority)
        {
            BonkedState = BONKEDSTATE.ALIVE;
        }
        _swapAtTick = Runner.Tick + 1;

        DeactivateRagDoll();
        if (characterController != null) //character 
        {
            foreach (StretchyArmIK ik in armIKs)
            {
                ik.enabled = true;
            }
            characterController.armatureRetargetingLerp = 0;
            characterController.handController.EnableHands();
        }
    }

    public override void Render()
    {
        if (_swapAtTick >= 0 && Runner.Tick >= _swapAtTick)
        {
            _swapAtTick = -1;
            bool showRagdoll = (BonkedState == BONKEDSTATE.BONKED);


            //if (showRagdoll)
            //{
            //    characterController.armatureRetargetingLerp = 1;
            //}
            //else
            //{
            //   characterController.armatureRetargetingLerp = 0;
            //}

        }
    }



    public void ActivateRagDoll()
    {

        foreach (PdBone bone in characterController.ragDollBones)
        {
            bone.WakeBone(HasStateAuthority);
        }
        //ragdollProxysRoot.SetActive(true);
        foreach (PdBone bone in characterController.ragDollBones)
        {
            bone.AddForcesAndApplyPhycis(HasStateAuthority);
        }
       
        //ragdollProxysRoot.SetActive(true);
    }

    public void DeactivateRagDoll()
    {

        foreach (PdBone bone in characterController.ragDollBones)
        {
            bone.SleepBone(HasStateAuthority);
        }
      
        //ragdollProxysRoot.SetActive(false);
    }

    public void SwitchToRagdoll()
    {

    }

    // Call this from your 'GetUnBonked' method
    public void SwitchToAnimated()
    {
       
    }

}