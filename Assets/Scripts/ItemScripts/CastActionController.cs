using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Security.Cryptography;
using NUnit.Framework.Interfaces;
using Fusion;
/// <summary>
/// Main script controlling player casting, ie sword swing, cast spell etc, works with player movement controller and player animation controller
/// </summary>


[RequireComponent(typeof(NetworkedInventoryManager))]
public abstract class CastActionController : NetworkBehaviour
{
    //public Animator animator;
    public NetworkedInventoryManager inventory;
    
    public bool isCasting;
    public bool canCombo;
    public bool isUpperBodyAction;
    public float currentAttackCooldown = 0f;

    [Header("Combo settings")]
    public int primaryComboCounter = 0;
    public float comboTimer = 0;
    public bool primaryAttackBuffered = false;
    public bool primaryAttackReleaseBuffered = false;


    public List<SpellState> activeCasts = new List<SpellState>();
    public int _nextActionId = 0;

    //eventName (called by animation), action
    private Dictionary<string, List<Action>> _pendingAnimationActions = new Dictionary<string, List<Action>>();
    private GameObject _activeHitboxInstance;

    public override void Render()
    {
        if (currentAttackCooldown > 0f)
        {
            currentAttackCooldown -= Time.deltaTime;
        }

        if (comboTimer > 0)
        {
            comboTimer -= Time.deltaTime;
        }
        else
        {
            comboTimer = 0;
        }

        if (primaryAttackBuffered && currentAttackCooldown <= 0)
        {
            primaryAttackBuffered = false;
            StartCast(primaryAttackReleaseBuffered);
            primaryAttackReleaseBuffered = false;
        }
        UpdateActiveCasts();

    }

    public void StartCast(bool isAlreadyReleased)
    {
        if (isCasting) return;

        if (inventory.activeItem == null) return;

        if (comboTimer <= 0 && !isAlreadyReleased)
        {
            primaryComboCounter = 0;
        }

        EquipableItem item = inventory.activeItem?.GetComponent<EquipableItem>(); //Get the active item from the inventory manager
        if (item == null || item.primaryActionSpell == null)
        {
            Debug.Log("No active spell or item found for this cast");
            return;
        }
            

        var entries = item.primaryActionSpell.GetComboEntries(); //Get the entry point of the spell from the entryPointController
        if (entries.Count == 0)
        {
            Debug.LogWarning("No Cast entries wired to EntryPointController.");
            return;
        }

        if (primaryComboCounter >= entries.Count) primaryComboCounter = 0; //Loop the entryPoint

        var entryCast = item.primaryActionSpell.GetEntryPoint(primaryComboCounter);
        if (entryCast == null)
        {
            Debug.LogWarning($"Entry Cast at index {primaryComboCounter} is null.");
            return;
        }

        Debug.Log($"{this.gameObject.name} cast {item.name} with spell {item.primarySpellID} at entry node {entryCast.name}");

        int actionId = _nextActionId;
        SpellState newCast = new SpellState(this, item, entryCast);
        
        SpellStateManager.instance.AddSpellState(newCast);

        activeCasts.Add(newCast);
        isCasting = true;
        newCast.isHeld = true;
        
        entryCast.OnCastStarted(newCast, this);

        primaryComboCounter++;
        if (primaryComboCounter >= entries.Count) primaryComboCounter = 0;

        if (isAlreadyReleased) EndCast();
    }

    public void UpdateActiveCasts()
    {
        for (int i = activeCasts.Count - 1; i >= 0; i--)
        {
            SpellState activeCast = activeCasts[i];
            if (activeCast.OriginalCasterNode != null)
            {
                activeCast.OriginalCasterNode.OnCastUpdate(activeCast, this);
            }
        }
    }

    public void EndCast()
    {
        if (primaryAttackBuffered)
        {
            primaryAttackReleaseBuffered = true;
            return;
        }
        isCasting = false;

        _nextActionId++;
        SpellState castToEnd = null;
        foreach (var cast in activeCasts)
        {
            if (cast.isHeld)
            {
                castToEnd = cast;
                break;
            }
        }
        if (castToEnd != null)
        {
            castToEnd.isHeld = false;

            if (castToEnd.OriginalCasterNode is CasterNode originalCastNode)
            {
                originalCastNode.OnCastCanceled(castToEnd, this); //calls the castCancelled function in the caster Node for clean up etc
            }
            activeCasts.Remove(castToEnd);
        }
    }

    public void SetCastTimer(float duration) //Sets a timer for the bool isCasting to be true;
    {
        isCasting = true;
        StartCoroutine(EndAction(duration));
    }

    IEnumerator EndAction(float afterTime) //sets is casting to false after a duration. used for some cast types where input dosnt define duration
    {
        yield return new WaitForSeconds(afterTime*0.6f);
        isCasting = false;
    }

    public void SetCoolDown(float cooldown) //Sets currentAttackCooldown
    {
        currentAttackCooldown = cooldown;
    }

    public void StartComboTimer(float duration) //starts the timer that if another action is used the next spell entry point will be used
    {
        comboTimer = duration;
    }

    //new
    public void ExecuteNodeAfterDelay(SpellNode node, SpellState state, float delay) //Executes a node after a delay, 
    {
        StartCoroutine(ExecuteNodeCoroutine(node, state, delay));
    }

    public IEnumerator ExecuteNodeCoroutine(SpellNode node, SpellState state, float delay)
    {
        yield return new WaitForSeconds(delay);
        state.CastPosition = state.CastItem.projectileSpawnPoint.position;
        state.CastRotation = this.transform.rotation;
        var triggerInfo = new SpellTriggerInfo(true, gameObject, state, state.CastItem.projectileSpawnPoint.position, this.transform.rotation, this.gameObject);
        triggerInfo.State.CastAimTargetPos = GetAimTarget();
        if (node is CoreNode coreNode)
        {
            coreNode.CreateSpellCore(triggerInfo);

        }
        else if (node is EffectNode effectNode)
        {
            effectNode.Execute(triggerInfo); 
        }
    }

    public abstract Vector3 GetAimTarget();

    public virtual Vector3 GetForward() { return transform.forward; }

    public void HandleAnimationEvent(string eventName)
    {
        if (eventName == "DeactivateHitBox")
        {
            OnDeactivateHitbox(); 
        }
        //Debug.Log($"Handleing Animation Event {eventName} ");
        if (_pendingAnimationActions.TryGetValue(eventName, out List<Action> actionsToExecute))
        {
            foreach (var action in actionsToExecute)
            {
                //Debug.Log($"Executing action due to event {eventName} ");
                action.Invoke();
            }
            _pendingAnimationActions.Remove(eventName);
        }
    }

    public void ExecuteNodesOnAnimationEvent(string eventName, List<SpellNode> nodes, SpellState state)
    {
        //Debug.Log($"Subscribed to Execute Node on Animation event {eventName}");
        Action onEventAction = () =>
        {
            foreach (var node in nodes)
            {
                state.CastPosition = state.CastItem.projectileSpawnPoint.position;
                state.CastRotation = this.transform.rotation;
                var triggerInfo = new SpellTriggerInfo(true, gameObject, state, state.CastItem.projectileSpawnPoint.position, this.transform.rotation, this.gameObject);
                triggerInfo.State.CastAimTargetPos = GetAimTarget();

                if (node is CoreNode coreNode)
                {
                    //Debug.Log($"Executing {node.name} on Animation event {eventName}");
                    coreNode.CreateSpellCore(triggerInfo);
                }
                else if (node is EffectNode effectNode)
                {
                    effectNode.Execute(triggerInfo);
                }
            }
        };

        if (!_pendingAnimationActions.ContainsKey(eventName))
        {
            _pendingAnimationActions[eventName] = new List<Action>();
        }
        _pendingAnimationActions[eventName].Add(onEventAction);
    }

    public void RegisterActiveHitbox(GameObject hitbox)
    {
        // If there's somehow an old hitbox, destroy it first.
        if (_activeHitboxInstance != null)
        {
            Destroy(_activeHitboxInstance);
        }
        _activeHitboxInstance = hitbox;
    }

    public void OnDeactivateHitbox()
    {
        if (_activeHitboxInstance != null)
        {
            Destroy(_activeHitboxInstance);
            _activeHitboxInstance = null; 
        }
    }

    public void ChangeHandStateAfterDelay(PhysicsHandController handController, PhysicsHandController.Hand hand, HandState newState, float delay)
    {
        StartCoroutine(ChangeHandStateCoroutine(handController, hand, newState, delay));
    }

    private IEnumerator ChangeHandStateCoroutine(PhysicsHandController handController, PhysicsHandController.Hand hand, HandState newState, float delay)
    {
        yield return new WaitForSeconds(delay);
        handController.SetHandState(hand, newState);
    }
}


