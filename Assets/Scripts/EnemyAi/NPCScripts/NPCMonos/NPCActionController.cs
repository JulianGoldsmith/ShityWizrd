using UnityEngine;
using System.Collections.Generic;
using Unity.Behavior;

public class NPCActionController : CastActionController
{
    [SerializeField] private BehaviorGraphAgent agent;

    [SerializeField] private string targetVariableName = "Target";
    [SerializeField] private string canAttackVariableName = "CanAttack";

    [SerializeField] public List<NPCAction> actions = new List<NPCAction>(); 
    private List<NPCAction> _runtimeActions = new List<NPCAction>(); // this is cloned list at runtime

    [SerializeField] private AnimationStateController _animStateController;

    private Dictionary<NPCAction, int> _actionBaseIndices = new Dictionary<NPCAction, int>();

    //public NPCAction _currentCastingAction;
    //private int _currentCastingBaseIndex;
    //private int _currentCastingPhase;

    public Transform spellSpawnPoint;

    public NPCActiveRagdollController activeRagdollController;

    private void Awake()
    {
        if (_animStateController == null)
            _animStateController = GetComponent<AnimationStateController>();

        agent = this.GetComponent<BehaviorGraphAgent>();
        activeRagdollController = this.GetComponent<NPCActiveRagdollController>();
    }

    public void Start()
    {
        //// clone all the actions here so were not editing assets. 
        _runtimeActions.Clear();
        foreach (NPCAction actionTemplate in actions)
        {
            if (actionTemplate == null)
            {
                _runtimeActions.Add(null); // Keep list indices in sync, im sure there is a better way to do this but seems ok
                continue;
            }

            //Clone the ScriptableObject asset
            NPCAction runtimeInstance = Instantiate(actionTemplate);
            runtimeInstance.name = actionTemplate.name + " (Runtime Instance)";
            _runtimeActions.Add(runtimeInstance);

            // load its runtime spellGraph from the baked JSON
            if (runtimeInstance is NPCActionSpell npcAS)
            {
                npcAS.LoadSpells(this.GetComponent<NetworkObjectBuffer>());
            }
        }

    }

    
    public void RegisterActionBaseIndex(NPCAction action, int baseIndex)
    {
        //This is called by our animationStateControler - it registers a "base" int that is used for action animation clips to a dictionary
        if (action == null) return;
        _actionBaseIndices[action] = baseIndex;
    } 

    //public void StartCast(int actionID)
    //{
    //    if (isCasting) return;

    //    if (actionID < 0 || actionID >= _runtimeActions.Count)
    //    {
    //        Debug.LogError($"Invalid actionID: {actionID}");
    //        return;
    //    }

    //    NPCAction actionToCast = _runtimeActions[actionID];

    //    if (actionToCast == null) return;

    //    NPCAction actionTemplate = actions[actionID];
    //    if (actionTemplate == null || !_actionBaseIndices.ContainsKey(actionTemplate))
    //    {
    //        Debug.LogError($"No base index registered for action: {actionTemplate.name}");
    //        return;
    //    }
    //    int baseIndex = _actionBaseIndices[actionTemplate];

    //    agent.SetVariableValue(canAttackVariableName, false);

    //    _currentCastingAction = actionToCast;
    //    _currentCastingBaseIndex = baseIndex;
    //    _currentCastingPhase = 0;
    //    _animStateController.PlayClip(_currentCastingBaseIndex + 0);

    //    if (actionToCast is NPCActionSpell spellToCast)
    //    {
    //        Debug.Log($"NPC cast spell StartCast called and action is spell");
    //        if (spellToCast.spell == null) return;
    //        spellToCast.spell.CompileSpell();
    //        var entries = spellToCast.spell.GetComboEntries();
    //        Debug.Log($"NPC cast spell got comboentries {entries.Count}");
    //        if (entries.Count == 0)
    //        {
    //            Debug.LogWarning("No Cast entries wired to EntryPointController.");
    //            return;
    //        }

    //        int combo = actionToCast.comboPoint;

    //        if (combo >= entries.Count) actionToCast.comboPoint = 0; //Loop the entryPoint

    //        var entryCast = spellToCast.spell.GetEntryPoint(combo);
    //        Debug.Log($"NPC cast spell got enty point{entryCast.name}");
    //        if (entryCast == null)
    //        {
    //            Debug.LogWarning($"NPC Entry Cast at index {combo} is null.");
    //            return;
    //        }
    //        Debug.Log($"{this.gameObject.name} cast ability with spell {spellToCast.spell.name} at entry node {entryCast.name}");

    //        SpellState newCast = new SpellState(this, null, spellToCast.spell, entryCast);

    //        activeCasts.Add(newCast);
    //        isCasting = true;
    //        newCast.isHeld = true;

            

    //        entryCast.OnCastStarted(newCast, this);

    //        actionToCast.comboPoint++;
    //        if (actionToCast.comboPoint >= entries.Count) actionToCast.comboPoint = 0;

    //    }
    //    else
    //    {
    //        //some other method of NPC action like melee swing etc...
    //    }



    //}

    public SpellState BeginAction(int actionID)
    {
        if (isCasting) return null;

        if (actionID < 0 || actionID >= _runtimeActions.Count)
        {
            Debug.LogError($"Invalid actionID: {actionID}");
            return null;
        }
        NPCAction actionToCast = _runtimeActions[actionID];
        if (actionToCast == null) return null;

        int baseIndex = _actionBaseIndices[actions[actionID]]; //get the base index from our dictionary of actions to index

        agent.SetVariableValue(canAttackVariableName, false);
        _animStateController.PlayClip(baseIndex + 0); //baseIndex + 0 == windup maybe we should have a better way to reference these

        if(actionToCast is NPCActionSpell spellAction) //if this action should cast a spell -- this will expand to other action types big IF is ok here
        {
            return Spell_StartCast(spellAction, actionToCast);
        }

        isCasting = true;
        return null;
    }

    public void ExecuteAction(SpellState state)
    {
        state.isHeld = false;
        if (state.OriginalCasterNode is CasterNode originalCastNode)
        {
            originalCastNode.OnCastCanceled(state, this);
        }
        activeCasts.Remove(state);
    }

    public void CleanupAction(SpellState state)
    {
        agent.SetVariableValue(canAttackVariableName, true);
        isCasting = false;

        if (state != null && activeCasts.Contains(state))
        {
            // This is a true ABORT/CANCEL
            state.isHeld = false;
            // We need a real OnCastAborted method in CasterNode!! TODO implement......
            if (state.OriginalCasterNode is CasterNode originalCastNode)
            {
                originalCastNode.OnCastCanceled(state, this);
            }
            activeCasts.Remove(state);
        }
    }

    public override void UpdateActiveCasts()
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

    //public override void EndCast()
    //{


    //    SpellState castToEnd = null;
    //    foreach (var cast in activeCasts)
    //    {
    //        if (cast.isHeld)
    //        {
    //            castToEnd = cast;
    //            break;
    //        }
    //    }

    //    agent.SetVariableValue(canAttackVariableName, true);

    //    if (castToEnd == null || !isCasting || _currentCastingAction == null)
    //    {
    //        isCasting = false;
    //        _currentCastingAction = null;
    //        if (activeCasts.Count == 0 && _animStateController != null)
    //        {
    //            _animStateController.GoToLocomotion();
    //        }
    //        return;
    //    }

    //    _currentCastingPhase = 2; // Set state to releasing
    //    int releaseClipIndex = _currentCastingBaseIndex + 2;
    //    _animStateController.PlayClip(releaseClipIndex);
    //    isCasting = false;
    //    _currentCastingAction = null;

    //    if (castToEnd != null)
    //    {
    //        castToEnd.isHeld = false;

    //        if (castToEnd.OriginalCasterNode is CasterNode originalCastNode)
    //        {
    //            originalCastNode.OnCastCanceled(castToEnd, this); //calls the castCancelled function in the caster Node for clean up etc
    //        }
    //        activeCasts.Remove(castToEnd);
    //    }
    //}


    #region spells
    public SpellState Spell_StartCast(NPCActionSpell spellToCast, NPCAction actionToCast)
    {
        Debug.Log($"NPC cast spell StartCast called and action is spell");
        if (spellToCast.spell == null) return null;

        spellToCast.spell.CompileSpell(this);

        var entries = spellToCast.spell.GetComboEntries();
        Debug.Log($"NPC cast spell got comboentries {entries.Count}");
        if (entries.Count == 0)
        {
            Debug.LogWarning("No Cast entries wired to EntryPointController.");
            return null;
        }

        int combo = actionToCast.comboPoint;

        if (combo >= entries.Count) actionToCast.comboPoint = 0;

        var entryCast = spellToCast.spell.GetEntryPoint(combo);
        Debug.Log($"NPC cast spell got enty point{entryCast.name}");
        if (entryCast == null)
        {
            Debug.LogWarning($"NPC Entry Cast at index {combo} is null.");
            return null;
        }


        SpellState newCast = new SpellState(this, null, spellToCast.spell, entryCast);

        activeCasts.Add(newCast);
        isCasting = true;
        newCast.isHeld = true;

        entryCast.OnCastStarted(newCast, this);
        Debug.Log($"{this.gameObject.name} cast ability with spell {spellToCast.spell.name} at entry node {entryCast.name}");

        actionToCast.comboPoint++;
        if (actionToCast.comboPoint >= entries.Count) actionToCast.comboPoint = 0;

        return newCast;
    }

    #endregion


    public override Vector3 GetAimTarget()
    {
        return activeRagdollController.coreRB.transform.forward;
    }

    #region animations

    public void PlayActionClip(int actionID, int clipPhaseIndex)
    {
        int baseIndex = _actionBaseIndices[actions[actionID]];
        _animStateController.PlayClip(baseIndex + clipPhaseIndex);
    }

    public float GetActionClipLength(int actionID, int clipPhaseIndex)
    {
        int baseIndex = _actionBaseIndices[actions[actionID]];
        return _animStateController.GetClipLength(baseIndex + clipPhaseIndex);
    }

    public float GetActionClipTime(int actionID, int clipPhaseIndex)
    {
        int baseIndex = _actionBaseIndices[actions[actionID]];
        return _animStateController.ClipTimes[baseIndex + clipPhaseIndex];
    }


    #endregion
}

