using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "SpellNodes/CastNodes/ChargeCast")]
public class ChargeCasterNode : CasterNode
{
    public float maxChargeTime = 2f;
    public float castDelay = 0.34f;

    public bool autoRelease = false; //needs logic to auto release .

    [Header("Animation Clips")]
    public AnimationClip startChargeClip;
    public AnimationClip loopChargeClip;
    public AnimationClip releaseClip;

    public override void OnCastStarted(SpellState state, CastActionController castController)
    {
        //castController.animationController.overrideController["DummyAction"] = startChargeClip;
        //castController.animationController.overrideController["DummyChargeLoop"] = loopChargeClip;
        //castController.animationController.overrideController["DummyRelease"] = releaseClip;
        ////castController.animationController.animator.runtimeAnimatorController = castController.animationController.overrideController;

        //castController.animationController.PlayAnimationActionState(startChargeClip, upperBodyOnly);
        //castController.animationController.EnterAnimationLoopState(loopChargeClip, upperBodyOnly);

        castController.isCasting = true;
        castController.SetCoolDown(Mathf.Infinity);

        state.ChargeStartTime = Time.time;

        state.chargeCastVFX = SpellSystemHelpers.CreateVFX(VFXContext.CastChargeEffect, ModifierType.Arcane, state.CastItem.projectileSpawnPoint, 1);

    }
    public override void OnCastCanceled(SpellState state, CastActionController castController)
    {
        //if (upperBodyOnly) { castController.animator.SetTrigger("UpperBodyCastRelease"); }
        //else { castController.animator.SetTrigger("FullBodyCastRelease"); }
        //castController.animator.SetBool("IsLoopingFullBodyAction", false);
        //castController.animator.SetBool("IsLoopingUpperBodyAction", false);
        castController.SetCoolDown(cooldown);
        state.Controller.SetCastTimer(releaseClip.length);
        castController.StartComboTimer(comboResetTime);



        float chargeDuration = Time.time - state.ChargeStartTime;
        state.CastChargeLevel = Mathf.Clamp01(chargeDuration / maxChargeTime);

        //Debug.Log($"Fired with charge level: {state.chargeLevel}");
        state.CastItem = state.Controller.inventory.activeItem.GetComponent<EquipableItem>();



        var nodesForOnCast = new List<SpellNode>();
        var nodesForHitbox = new List<SpellNode>();

        foreach (var node in outcomeCoreNodes)
        {
            if(node is CoreNode cn)
            {
                switch (cn.casterTriggerMethod)
                {
                    case CasterTriggerMethod.OnCast:
                        nodesForOnCast.Add(node);
                        break;
                    case CasterTriggerMethod.OnHitboxActivate:
                        nodesForHitbox.Add(node);
                        break;
                }
            }
            else if (node is EffectNode en)
            {
                switch (en.casterTriggerMethod)
                {
                    case CasterTriggerMethod.OnCast:
                        nodesForOnCast.Add(node);
                        break;
                    case CasterTriggerMethod.OnHitboxActivate:
                        nodesForHitbox.Add(node);
                        break;
                }
            }
            
        }

        if (nodesForOnCast.Count > 0)
        {
            castController.ExecuteNodesOnAnimationEvent("TriggerStartCast", nodesForOnCast, state); 
        }
        if (nodesForHitbox.Count > 0)
        {
            castController.ExecuteNodesOnAnimationEvent("ActivateHitBox", nodesForHitbox, state);
        }

        if (state.chargeCastVFX != null)
            GameObject.Destroy(state.chargeCastVFX);
    }
}