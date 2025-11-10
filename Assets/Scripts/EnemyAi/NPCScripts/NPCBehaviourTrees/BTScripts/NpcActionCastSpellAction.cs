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
    private NPCAction _actionData; 
    private SpellState _currentSpellState;
    private float _phaseTimer;

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

        _currentPhase = Phase.Windup;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        Debug.Log($"NPC Action - Phase = {_currentPhase} current clip time = {CurrentClipTime} / {CurrentClipLength}");
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
        // Wait for our timer to finish
        if (Time.time >= _phaseTimer)
        {
            // --- Transition to Release ---
            _actionController.PlayActionClip(NPCActionID.Value, 2); // Play Release clip
            _currentPhase = Phase.Release;

            // This is the moment to "release" the spell
            _actionController.ExecuteAction(_currentSpellState);

            return Status.Running;
        }
        return Status.Running; // Still holding
    }

    private Status HandleRelease()
    {
        // Wait for the "Release" animation to finish
        if (CurrentClipTime >= CurrentClipLength)
        {
            // --- Action is complete ---
            _actionController.CleanupAction(null); // Just set canAttack = true, isCasting = false
            _currentPhase = Phase.Idle;
            return Status.Success; // We are done!
        }
        return Status.Running; // Still in release animation
    }
}

