using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Security.Cryptography;
using NUnit.Framework.Interfaces;
using Fusion;


public abstract class CastActionController : NetworkBehaviour
{
    //public Animator animator;
    public NetworkedInventoryManager inventory;
    public ActiveCastTracker CastTracker { get; private set; }

    public bool isCasting;
    public bool isUpperBodyAction;
    public float currentAttackCooldown = 0f;

    [Header("Combo settings")]
    public int primaryComboCounter = 0;
    public float comboTimer = 0;
    public bool primaryAttackBuffered = false;
    public bool primaryAttackReleaseBuffered = false;


    public List<SpellState> activeCasts = new List<SpellState>();
    public int _nextActionId = 0;

    private Dictionary<string, List<Action>> _pendingAnimationActions = new Dictionary<string, List<Action>>();
    private GameObject _activeHitboxInstance;
    [Networked] public int TotalSpellCasts { get; set; }

    public List<Vector3> spellCastPoints = new List<Vector3>();
    public override void Spawned()
    {
        base.Spawned();
        CastTracker = GetComponent<ActiveCastTracker>();
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out NetworkInputData input))
        {
           //hmm
        }

        SimulateCasting(Runner.DeltaTime);
    }

    private void SimulateCasting(float dt)
    {
        if (currentAttackCooldown > 0f)
            currentAttackCooldown -= dt;

        if (comboTimer > 0f)
            comboTimer -= dt;
        else
            comboTimer = 0f;

        if (primaryAttackBuffered && currentAttackCooldown <= 0f)
        {
            primaryAttackBuffered = false;
            StartCast(primaryAttackReleaseBuffered);
            primaryAttackReleaseBuffered = false;
        }

        UpdateActiveCasts();   // again this should be called from the item not here? 
        //TickItemActions(dt);   // this should be called from the item -- not here? 
    }

    public ActiveCastID GenerateNewCastID()
    {
        TotalSpellCasts++;
        return new ActiveCastID(this.Object.Id, TotalSpellCasts);
    }

    public void RegisterAndTrackCast(SpellState newCast, SpellGraph graph)
    {
        if (!activeCasts.Contains(newCast))
        {
            activeCasts.Add(newCast);
        }

        if (CastTracker != null)
        {
            CastTracker.RegisterNetworkedCast(newCast.NetCastData);
        }

        ActiveSpell newActiveSpell = new ActiveSpell(newCast.ActiveCastID, graph, newCast);
        newActiveSpell.AddToken();
        SpellStateManager.instance.RegisterNewCast(newCast.ActiveCastID, newActiveSpell);
    }

    public virtual void StartCast(bool isAlreadyReleased)
    {
        if (isCasting) return;
        if (inventory.activeItem == null) return;

        if (comboTimer <= 0 && !isAlreadyReleased)
        {
            primaryComboCounter = 0;
        }

        EquipableItem item = inventory.activeItem.GetComponent<EquipableItem>();
        if (item == null || item.primaryActionSpell == null)
        {
            Debug.Log("No active spell or item found for this cast");
            return;
        }

        SpellGraph graph = item.primaryActionSpell;

        graph.CompileSpell();

        int comboCount = graph.GetComboCount();
        if (comboCount <= 0)
        {
            Debug.LogWarning("No combo roots wired to EntryPointControlNode.");
            return;
        }

        if (primaryComboCounter >= comboCount)
            primaryComboCounter = 0;

        var netObj = GetComponent<NetworkObject>();
        ActiveCastID newCastID = GenerateNewCastID();
        SpellState newCast = new SpellState(newCastID,this, item, graph, null, netObj);
        newCast.isHeld = true;

        RegisterAndTrackCast(newCast, graph);

        //activeCasts.Add(newCast);
        isCasting = true;
       


        graph.ExecuteComboIndex(primaryComboCounter, newCast, this);

        Debug.Log(
            $"{gameObject.name} cast {item.name} with spell {graph.name} at combo index {primaryComboCounter}");


        primaryComboCounter++;
        if (primaryComboCounter >= comboCount)
            primaryComboCounter = 0;

 
        if (isAlreadyReleased)
        {
            EndCast();
        }
    }

    public virtual void UpdateActiveCasts()
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

    public virtual void EndCast()
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
                originalCastNode.OnCastCanceled(castToEnd, this); 
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

    public void AdvancePrimaryCombo(int totalActions)
    {
        if (totalActions <= 0)
        {
            primaryComboCounter = 0;
            return;
        }

        primaryComboCounter++;
        if (primaryComboCounter >= totalActions)
            primaryComboCounter = 0;
    }

    public void SetCoolDown(float cooldown) //Sets currentAttackCooldown
    {
        currentAttackCooldown = cooldown;
    }

    public void StartComboTimer(float duration) //starts the timer that if another action is used the next spell entry point will be used
    {
        comboTimer = duration;
    }

    public void ClearCastsForItem(EquipableItem item)
    {
        for (int i = activeCasts.Count - 1; i >= 0; i--)
        {
            if (activeCasts[i].CastItem == item)
            {
                activeCasts.RemoveAt(i);
            }
        }
    }

    public virtual void OnItemHit(SpellState state,GameObject hitObject, Vector3 hitPoint,Vector3 swingMomentum)
    {
        if (state == null)
            return;

        var graph = state.Spell;
        if (graph == null)
        {
            Debug.LogWarning($"OnItemHit: SpellState has no Spell reference on {name}.");
            return;
        }

        Quaternion hitRotation = Quaternion.LookRotation(GetForward());

        var triggerInfo = new SpellTriggerInfo(
            isCast: true,
            source: gameObject,
            state: state,
            position: hitPoint,
            rotation: hitRotation,
            triggerVector: swingMomentum,
            hitObject: hitObject
        );

        state.CastAimTargetPos = GetAimTarget();
        state.CastPosition = triggerInfo.TriggerPoint;
        state.CastRotation = triggerInfo.TriggerRotation;

        int comboIndex = state.ComboIndex;  
        graph.ExecuteComboIndex(comboIndex, triggerInfo);
    }

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

    #region ItemAnimations

    public abstract EyePosAndLookDir GetEyePosAndLookDir();


    protected virtual void TickItemActions(float deltaTime) //maybe this should be called from the item, not here
    {
        if (inventory == null || inventory.activeItem == null)
            return;

        var item = inventory.activeItem.GetComponent<EquipableItem>();
        if (item == null)
            return;

        if (item.primaryActions == null || item.primaryActions.Count == 0)
            return;

        for (int i = 0; i < item.primaryActions.Count; i++)
        {
            var action = item.primaryActions[i];
            if (action == null) continue;

            action.Tick( i, deltaTime);
        }
    }
    #endregion



    public abstract void ActivateHitbox(int hitBoxID, SpellState state);
    public abstract void DeactivateHitbox(int hitBoxID);

    public virtual Vector3 GetSpellCastPoint()
    {
        return transform.position;
    }


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


    //not used 
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



    public SpellState GetActiveSpellState(int comboIndex)
    {
        for (int i = activeCasts.Count - 1; i >= 0; i--)
        {
            if (activeCasts[i].ComboIndex == comboIndex && activeCasts[i].isHeld)
                return activeCasts[i];
        }
        return null;
    }


}


