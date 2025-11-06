using UnityEngine;
using System.Collections.Generic;
using Unity.Behavior;

public class NPCActionController : CastActionController
{
    [SerializeField] private BehaviorGraphAgent agent;

    [SerializeField] private string targetVariableName = "Target";
    [SerializeField] private string canAttackVariableName = "CanAttack";

    [SerializeField] public List<NPCAction> actions = new List<NPCAction>();

    private List<NPCAction> _runtimeActions = new List<NPCAction>();

    [SerializeField] private AnimationStateController _animStateController;

    private Dictionary<NPCAction, int> _actionBaseIndices = new Dictionary<NPCAction, int>();

    public NPCAction _currentCastingAction;
    private int _currentCastingBaseIndex;
    private int _currentCastingPhase;

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
        _runtimeActions.Clear();
        foreach (NPCAction actionTemplate in actions)
        {
            if (actionTemplate == null)
            {
                _runtimeActions.Add(null); // Keep list indices in sync
                continue;
            }

            // 1. Clone the ScriptableObject asset
            NPCAction runtimeInstance = Instantiate(actionTemplate);
            runtimeInstance.name = actionTemplate.name + " (Runtime Instance)";
            _runtimeActions.Add(runtimeInstance);

            // 2. If it's a spell, load its runtime SpellGraph from the baked JSON
            if (runtimeInstance is NPCActionSpell npcAS)
            {
                npcAS.LoadSpells(this.GetComponent<NetworkObjectBuffer>());
            }
        }

    }


    public void RegisterActionBaseIndex(NPCAction action, int baseIndex)
    {
        if (action == null) return;
        _actionBaseIndices[action] = baseIndex;
    }

    public void StartCast(int actionID)
    {
        if (isCasting) return;

        if (actionID < 0 || actionID >= _runtimeActions.Count)
        {
            Debug.LogError($"Invalid actionID: {actionID}");
            return;
        }

        NPCAction actionToCast = _runtimeActions[actionID];

        if (actionToCast == null) return;

        NPCAction actionTemplate = actions[actionID];
        if (actionTemplate == null || !_actionBaseIndices.ContainsKey(actionTemplate))
        {
            Debug.LogError($"No base index registered for action: {actionTemplate.name}");
            return;
        }
        int baseIndex = _actionBaseIndices[actionTemplate];

        agent.SetVariableValue(canAttackVariableName, false);

        if (actionToCast is NPCActionSpell spellToCast)
        {
            Debug.Log($"NPC cast spell StartCast called and action is spell");
            spellToCast.spell.CompileSpell();
            var entries = spellToCast.spell.GetComboEntries();
            Debug.Log($"NPC cast spell got comboentries {entries.Count}");
            if (entries.Count == 0)
            {
                Debug.LogWarning("No Cast entries wired to EntryPointController.");
                return;
            }

            int combo = actionToCast.comboPoint;

            if (combo >= entries.Count) actionToCast.comboPoint = 0; //Loop the entryPoint

            var entryCast = spellToCast.spell.GetEntryPoint(combo);
            Debug.Log($"NPC cast spell got enty point{entryCast.name}");
            if (entryCast == null)
            {
                Debug.LogWarning($"NPC Entry Cast at index {combo} is null.");
                return;
            }
            Debug.Log($"{this.gameObject.name} cast ability with spell {spellToCast.spell.name} at entry node {entryCast.name}");

            SpellState newCast = new SpellState(this, null, spellToCast.spell, entryCast);

            activeCasts.Add(newCast);
            isCasting = true;
            newCast.isHeld = true;

            _currentCastingAction = actionToCast;
            _currentCastingBaseIndex = baseIndex;
            _currentCastingPhase = 0;
            _animStateController.PlayClip(_currentCastingBaseIndex + 0);

            entryCast.OnCastStarted(newCast, this);

            actionToCast.comboPoint++;
            if (actionToCast.comboPoint >= entries.Count) actionToCast.comboPoint = 0;

        }
        else
        {
            //some other method of NPC action like melee swing etc...
        }



    }

    public override void UpdateActiveCasts()
    {
        if (isCasting && _currentCastingAction != null && _currentCastingPhase == 0) // If in Windup
        {
            int windupClipIndex = _currentCastingBaseIndex + 0;
            float windupTime = _animStateController.ClipTimes[windupClipIndex];
            float windupLength = _animStateController.GetClipLength(windupClipIndex);

            if (windupTime >= windupLength)
            {
                _currentCastingPhase = 1;
                int holdClipIndex = _currentCastingBaseIndex + 1;
                _animStateController.PlayClip(holdClipIndex);
            }
        }


        for (int i = activeCasts.Count - 1; i >= 0; i--)
        {
            SpellState activeCast = activeCasts[i];
            if (activeCast.OriginalCasterNode != null)
            {
                activeCast.OriginalCasterNode.OnCastUpdate(activeCast, this);
            }
        }
    }

    public override void EndCast()
    {


        SpellState castToEnd = null;
        foreach (var cast in activeCasts)
        {
            if (cast.isHeld)
            {
                castToEnd = cast;
                break;
            }
        }

        agent.SetVariableValue(canAttackVariableName, true);

        if (castToEnd == null || !isCasting || _currentCastingAction == null)
        {
            isCasting = false;
            _currentCastingAction = null;
            if (activeCasts.Count == 0 && _animStateController != null)
            {
                _animStateController.GoToLocomotion();
            }
            return;
        }

        _currentCastingPhase = 2; // Set state to releasing
        int releaseClipIndex = _currentCastingBaseIndex + 2;
        _animStateController.PlayClip(releaseClipIndex);
        isCasting = false;
        _currentCastingAction = null;

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

    public override Vector3 GetAimTarget()
    {
        return activeRagdollController.coreRB.transform.forward;
    }


}

