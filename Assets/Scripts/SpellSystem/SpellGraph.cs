using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class NodeInstanceData
{
    public string guid;
    public Vector3 position;
    public string nodeTemplateName;
    public List<NodeConnection> connections = new List<NodeConnection>();

    public List<string> childNodeGUIDs = new List<string>();

    public string sourceTemplateNodeGuid;
}

//info on the connection
[System.Serializable]
public struct NodeConnection
{
    // name of the output socket e.g, On Event
    public string fromOutputSocketName;
    public string fromOutputOwnerGUID;

    public string targetNodeGUID;

    // name of the input socket e.g, Exec In
    public string toInputSocketName;
    public string toInputOwnerGUID;
}


//the main container for the whole spell, can be just data or "loaded with visuals as well
[CreateAssetMenu(fileName = "NewSpellGraph", menuName = "SpellNodes/Spell Graph")]
public class SpellGraph : ScriptableObject
{
    //for Saving
    public string entryPointControllerNodeGuid;
    public List<NodeInstanceData> nodes = new List<NodeInstanceData>();

    //for runtime
    [System.NonSerialized]
    public EntryPointControlNode entryPointControllerNode;
    [System.NonSerialized]
    public Dictionary<string, RuneUI> runeUIsByGuid = new Dictionary<string, RuneUI>();
    [System.NonSerialized]
    public Dictionary<string, SpellNode> liveNodeClonesByGuid = new Dictionary<string, SpellNode>();


    public List<CasterNode> GetComboEntries()
    {
        if (entryPointControllerNode == null) return new List<CasterNode>();
        return entryPointControllerNode.orderedEntries?.OfType<CasterNode>().ToList() ?? new List<CasterNode>();
    }

    public CasterNode GetEntryPoint(int index)
    {
        var entries = GetComboEntries();
        if (entries.Count == 0)
        {
            Debug.LogWarning("SpellNodeGraph: No CasterNode children connected to EntryPointControlNode.");
            return null;
        }
        if (index < 0) index = 0;
        if (index >= entries.Count) index = 0; // wrap
        return entries[index];
    }



    //specific node instance in the graph by its guid
    public NodeInstanceData GetNodeInstance(string guid)
    {
        return nodes.FirstOrDefault(n => n.guid == guid);
    }



}
