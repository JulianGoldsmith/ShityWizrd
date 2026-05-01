using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "NPCAction_CastSpell", story: "[Self] Casts Spell ActionID [NPCActionID]", category: "Action", id: "045c9722a288ce011ea7e7424296f433")]
public partial class NpcActionCastSpellAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<int> NPCActionID;

    private enum Phase {  Windup = 0, Hold = 1, Release =2, Idle = -1, }
    private Phase _currentPhase;

    private NPCActionController _actionController;
    private NPC_Action _actionData; 
    private SpellState _currentSpellState;
    private float _phaseTimer;

    private float activateHitBoxTime =-1f, deactivateHitBoxTime =-1f;

    private float CurrentClipTime => _actionController.GetActionClipTime(NPCActionID.Value, (int)_currentPhase);
    private float CurrentClipLength => _actionController.GetActionClipLength(NPCActionID.Value, (int)_currentPhase);

    protected override Status OnStart()
    {
        _actionController = Self.Value.GetComponent<NPCActionController>();
        if (_actionController == null) return Status.Failure;

        _actionData = _actionController.actions[NPCActionID.Value];
        if (_actionData == null) return Status.Failure;

        _currentSpellState = _actionController.BeginAction(NPCActionID.Value);
        if (!_actionController.isCasting)
        {
            return Status.Failure;
        }

        activateHitBoxTime = -1; 
        deactivateHitBoxTime = -1f;

        _currentPhase = Phase.Windup;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        //Debug.Log($"NPC Action - Phase = {_currentPhase} current clip time = {CurrentClipTime} / {CurrentClipLength}");
        switch (_currentPhase)
        {
            case Phase.Windup: return HandleWindup();
            case Phase.Hold: return HandleHold();
            case Phase.Release: return HandleRelease();
        }
        return Status.Failure;
    }

    protected override void OnEnd()
    {
        CleanUpHitBoxsOnEnd();

        if (_currentPhase != Phase.Idle)
        {
            if (this.CurrentStatus != Status.Success) //if this failed
            {
                _actionController.CleanupAction(_currentSpellState); // True cancel
                //_actionController.GoToLocomotion(); // Hard stop
            }
        }

        _currentPhase = Phase.Idle; // Reset for next time
    }

    private Status HandleWindup()
    {
        
        if (CurrentClipTime >= CurrentClipLength)
        {

            //if (_actionData is NPCAction_Melee melee)
            //{
            //    _actionController.PlayActionClip(NPCActionID.Value, 1); // Play Hold/Telegraph
            //    _currentPhase = Phase.Hold;
            //    _phaseTimer = Time.time + melee.telegraphDuration;
            //}
            //else if (_actionData is NPCAction_StandardSpell standard)
            //{
            //    _actionController.PlayActionClip(NPCActionID.Value, 1); // Play Hold
            //    _currentPhase = Phase.Hold;
            //    _phaseTimer = Time.time + standard.holdDuration;
            //}
            _actionController.PlayActionClip(NPCActionID.Value, 1); // Play Hold
            _currentPhase = Phase.Hold;
            _phaseTimer = Time.time + _actionData.holdDuration;
            return Status.Running;
        }
        return Status.Running; 
    }

    private Status HandleHold()
    {
        if (Time.time >= _phaseTimer)
        {
            _actionController.PlayActionClip(NPCActionID.Value, 2);
            _currentPhase = Phase.Release;

            _actionController.ExecuteAction(_currentSpellState);

            StartHitBoxTimers();
            return Status.Running;
        }
        return Status.Running; 
    }

    private Status HandleRelease()
    {
        ActivateHitBoxIfUsingHitBox();
        DeactivateHitBoxIfUsingHitBox();
        if (CurrentClipTime >= CurrentClipLength)
        {
            _actionController.CleanupAction(null); 
            _currentPhase = Phase.Idle;
            DeactivateHitBoxIfUsingHitBox();
            return Status.Success; 
        }
        return Status.Running; 
    }

    private void StartHitBoxTimers()
    {
        if(_actionData is NPCActionSpell spell)
        {
            activateHitBoxTime = Time.time + spell.timeAfterReleaseToActivateHitBox;
            deactivateHitBoxTime = activateHitBoxTime + spell.hitBoxDuration;
        }
    }

    private void ActivateHitBoxIfUsingHitBox()
    {
        if (_currentSpellState == null)
            return;

        if (activateHitBoxTime == -1f)
            return;

        if (_actionData is NPCActionSpell spell)
        {
            if (spell.hitBoxID < 0 || spell.hitBoxID >= _actionController.hitboxes.Count)
                return;

            if (Time.time > activateHitBoxTime &&
                _actionController.hitboxes[spell.hitBoxID].hitBoxCollider.enabled == false)
            {
                Debug.Log("Activating hit box from the NPC BT ACTION");
                _actionController.ActivateHitbox(spell.hitBoxID, _currentSpellState);
            }
        }
    }

    private void DeactivateHitBoxIfUsingHitBox()
    {
        if (_currentSpellState == null)
            return;

        if (activateHitBoxTime == -1f)
            return;

        if (_actionData is NPCActionSpell spell)
        {
            if (spell.hitBoxID < 0 || spell.hitBoxID >= _actionController.hitboxes.Count)
                return;

            if (Time.time > deactivateHitBoxTime &&
                _actionController.hitboxes[spell.hitBoxID].hitBoxCollider.enabled == true)
            {
                _actionController.DeactivateHitbox(spell.hitBoxID);
            }
        }
    }

    private void CleanUpHitBoxsOnEnd()
    {
        if (_actionData is NPCActionSpell spell && spell.hitBoxID != -1) //cleanUpHitbox
        {
            if (spell.hitBoxID >= 0 && spell.hitBoxID < _actionController.hitboxes.Count && _actionController.hitboxes[spell.hitBoxID].hitBoxCollider.enabled)
            {
                _actionController.DeactivateHitbox(spell.hitBoxID);
            }
        }
    }
}

