using UnityEngine;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// This "castNode" is a hybrid, it also monitors for hitbox collision and this is where the triggers lie rather than creating a new 
/// expensive networked object it uses the hitbox we already have 
/// HitBox's are trigger colliders! they do NOT produce any knockback etc by default. 
/// </summary>

[CreateAssetMenu(fileName = "HitBoxCastNode", menuName = "SpellNodes/CastNodes/HitBoxCastNode")]
public class HitBoxCastNode : CasterNode
{

    

    public override void OnCastStarted(SpellState state, CastActionController castController)
    {
        //castController.ActivateHitbox(hitBoxID, state);
    }

    public override void OnCastUpdate(SpellState state, CastActionController castController)
    {
    }

    public override void OnCastCanceled(SpellState state, CastActionController castController)
    {
        //castController.DeactivateHitbox(hitBoxID);
    }


    //public void ActivateHitBox(SpellState state, CastActionController castController)
    //{
    //    castController.ActivateHitbox(hitBoxID, state);
    //}

    //public void DeactivateHitBox(SpellState state, CastActionController castController)
    //{
    //    castController.DeactivateHitbox(hitBoxID);
    //}


    public void HandleTrigger(SpellState state, CastActionController castController, GameObject hitObject, Vector3 hitPoint, Vector3 swingMomentum)
    {
        var triggerInfo = new SpellTriggerInfo(
            true,
            castController.gameObject, 
            state,
            hitPoint,                  
            Quaternion.LookRotation(castController.GetForward()),
            swingMomentum, // will need changing to the hit direction/ momentum/ some calculation for hit direction
            hitObject              
        );

        triggerInfo.State.CastAimTargetPos = castController.GetAimTarget();
        state.CastRotation = triggerInfo.TriggerRotation;
        state.CastPosition = triggerInfo.TriggerPoint;
        Debug.Log($"Collision on HitBox with {hitObject.name} lead to out come nodes {string.Join(", ", outcomeCoreNodes.Select(node => node.name))}");
        foreach (var node in outcomeCoreNodes)
        {
            if (node is CoreNode coreNode)
            {
                //coreNode.CreateSpellCore(triggerInfo);
            }
            else if (node is EffectNode effectNode)
            {
                Debug.Log("Handle trigger Called to execute an effect node");
                effectNode.Execute(triggerInfo);
            }
        }
    }

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        throw new System.NotImplementedException();
    }
}
