using Fusion;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class CharacterBonkController : NetworkBehaviour
{
    public HybridCharacterController characterController;
    public List<StretchyArmIK> armIKs;
    public GameObject ragdollProxysRoot;

    public XPBDPosAndRotSolver ragDollSolver;
    [Networked] public int BonkTick { get; set; }
    [Networked] public int UnbonkTick { get; set; }

    public BONKEDSTATE BonkedState => BonkTick>UnbonkTick ? BONKEDSTATE.BONKED: BONKEDSTATE.ALIVE;

    private bool _localRagdollActive = false;

    public override void Spawned()
    {
        //if(characterController != null)
        ragDollSolver = this.GetComponent<XPBDPosAndRotSolver>();

        EvaluateRagdollState();
    }

    public override void FixedUpdateNetwork()
    {
        EvaluateRagdollState();
    }

    private void EvaluateRagdollState()
    {
        bool shouldBeBonked = BonkTick > UnbonkTick;


        
        if (ragdollProxysRoot!=null && ragdollProxysRoot.activeSelf != shouldBeBonked)
        {
            
            if (shouldBeBonked) ActivateRagDoll(snapBones: false);
            else DeactivateRagDoll();

            _localRagdollActive = shouldBeBonked;
        }

        if (characterController != null && ragDollSolver != null)
        {
            ragDollSolver.isRagdolling = shouldBeBonked;
        }
    }

    // --- LOCAL / PREDICTED ACTIONS ---

    public void GetBonked()
    {
        // 1. Immediately predict the visual and physics change locally! 
        // Pass TRUE to snap the rigidbodies to the current animation frame so they fall correctly.
        if (ragdollProxysRoot != null && !ragdollProxysRoot.activeSelf)
        {
            ActivateRagDoll(snapBones: true);
            _localRagdollActive = true;
        }

        // 2. Tell the network when this happened
        BonkTick = Runner.Tick;
    }

    public void GetUnBonked()
    {
        if (ragdollProxysRoot.activeSelf)
        {
            DeactivateRagDoll();
            _localRagdollActive = false;
        }

        UnbonkTick = Runner.Tick;
    }

    // --- EXECUTION METHODS ---

    private void ActivateRagDoll(bool snapBones)
    {
        if (ragdollProxysRoot != null) ragdollProxysRoot.SetActive(true);
        if (characterController != null)
        {
            foreach (StretchyArmIK ik in armIKs) ik.enabled = false;
            characterController.handController.DisableHands();
            characterController.armatureRetargetingLerp = 1f;
        }
        ragDollSolver.SetRagdollState(true, snapBones);
        Debug.Log("Character Ragdoll deactivated");

    }

    private void DeactivateRagDoll()
    {
        if (characterController != null)
        {
            foreach (StretchyArmIK ik in armIKs) ik.enabled = true;
            characterController.armatureRetargetingLerp = 0f;
            characterController.handController.EnableHands();
        }
        ragDollSolver.SetRagdollState(false, false);
        if (ragdollProxysRoot != null) ragdollProxysRoot.SetActive(false);
        Debug.Log("Character Ragdoll deactivated");
    }
}