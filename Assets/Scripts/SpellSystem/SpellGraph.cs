using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public struct NetworkNodeData //data for a single node
{
    public ushort TemplateID;
    public Vector3 Position;
}

[System.Serializable]
public struct WireData //data for the connections node to node
{
    public byte FromNodeIndex;
    public byte FromSocketIndex;
    public byte ToNodeIndex;
    public byte ToSocketIndex;
}

[System.Serializable]
public struct SpellNetworkData //the full spell as a list of nodes and connections
{
    public NetworkNodeData[] Nodes;
    public WireData[] Wires;

    public byte MaxNodeIndex;
    public byte MaxWireIndex;
}

[CreateAssetMenu(fileName = "NewSpellGraph", menuName = "SpellNodes/Spell Graph")]
public class SpellGraph : ScriptableObject
{
    [Header("The Master Data")]
    public SpellNetworkData Data;

    // --- Runtime Execution Variables ---
    [System.NonSerialized]
    public SpellGraphId spellGraphId;
    [System.NonSerialized]
    public EntryPointControlNode entryPointControllerNode; // We will phase this out later for the Weapon Entry Points!

    // --- TEMPORARY SHADOW VARIABLES ---
    // (We keep these here for one more step so the UI doesn't totally break while we transition)
    [System.NonSerialized]
    public Dictionary<string, RuneUI> runeUIsByGuid = new Dictionary<string, RuneUI>();
    [System.NonSerialized]
    public Dictionary<string, SpellNode> liveNodeClonesByGuid = new Dictionary<string, SpellNode>();

    /// <summary>
    /// Initializes the empty arrays if this is a brand new spell.
    /// </summary>
    public void InitializeEmptyGraph()
    {
        // Hard limits: 64 nodes and 128 wires per spell.
        Data.Nodes = new NetworkNodeData[64];
        Data.Wires = new WireData[128];
        Data.MaxNodeIndex = 0;
        Data.MaxWireIndex = 0;
    }

    // --- LEGACY EXECUTION METHODS ---
    // (We leave these untouched for now so your active gameplay doesn't break)

    public int GetComboCount()
    {
        if (entryPointControllerNode == null) return 0;
        entryPointControllerNode.EnsureComboCapacity();
        return entryPointControllerNode.comboRoots.Count;
    }

    public List<SpellNode> GetComboRoots(int index)
    {
        if (entryPointControllerNode == null) return new List<SpellNode>();
        return entryPointControllerNode.GetComboRoots(index);
    }

    public void ExecuteComboIndex(int comboIndex, SpellState state, CastActionController caster)
    {
        var roots = GetComboRoots(comboIndex);
        if (roots == null || roots.Count == 0) return;

        var triggerInfo = new SpellTriggerInfo(
            isCast: true,
            source: caster.gameObject,
            state: state,
            position: state.CastPosition,
            rotation: state.CastRotation,
            triggerVector: caster.GetForward(),
            hitObject: caster.gameObject
        );
        triggerInfo.State.CastAimTargetPos = caster.GetAimTarget();

        foreach (var node in roots)
        {
            switch (node)
            {
               // case CoreNode core: core.CreateSpellCore(triggerInfo); break;
                case EffectNode effect: effect.Execute(triggerInfo); break;
            }
        }
    }

    public void ExecuteComboIndex(int comboIndex, SpellTriggerInfo triggerInfo)
    {
        if (entryPointControllerNode == null) return;
        if (!triggerInfo.IsValid || triggerInfo.State == null) return;

        var chain = entryPointControllerNode.GetComboRoots(comboIndex);
        if (chain == null || chain.Count == 0) return;

        foreach (var node in chain)
        {
            //if (node is CoreNode coreNode) coreNode.CreateSpellCore(triggerInfo);
             if (node is EffectNode effectNode) effectNode.Execute(triggerInfo);
        }
    }

    public void CompileSpell()
    {
        foreach (KeyValuePair<string, SpellNode> kv in liveNodeClonesByGuid)
        {
            kv.Value.Compile();
        }
    }
}