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
    public SpellGraphId spellGraphId;
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

    //I think we should move this to a 1 time event -- after recieving a spell over network or sending (rather than every cast?)
    //We could have a compile for all of this, and an Update for special circustances. But we shouldnt re-compile without sending etc...
    public void CompileSpell(CastActionController caster) 
    {
        foreach (KeyValuePair<string, SpellNode> kv in liveNodeClonesByGuid)
        {
            kv.Value.Compile();
        }
        foreach (KeyValuePair<string, SpellNode> kv in liveNodeClonesByGuid)
        {
            if (kv.Value is HitBoxCastNode hitBoxNode)
            {
                //GetCaster 
                //Find hitBox object to "clone" and add triggers too 
                
                // logic here for assigning hitBox to the cloned hitBoxNode so we can reference it in the node. 
                //needs to be after all
            }
        }
    }


    //specific node instance in the graph by its guid
    public NodeInstanceData GetNodeInstance(string guid)
    {
        return nodes.FirstOrDefault(n => n.guid == guid);
    }

    #region Save/Load
    public string ToJson()
    {
        // Here we can clean it up if necessary.
        return JsonUtility.ToJson(this, true);
    }
    public static SpellGraph FromJson(string json)
    {
        SpellGraph graph = ScriptableObject.CreateInstance<SpellGraph>();
        JsonUtility.FromJsonOverwrite(json, graph);

        graph.runeUIsByGuid = new Dictionary<string, RuneUI>();
        graph.liveNodeClonesByGuid = new Dictionary<string, SpellNode>();

        foreach (var nodeData in graph.nodes)
        {
            SpellNode nodeTemplate = SpellGraphController.Instance.FindTemplateByName(nodeData.nodeTemplateName);
            if (nodeTemplate != null)
            {
                SpellNode clone = nodeTemplate.CloneThisNode();
                clone.InstanceGuid = nodeData.guid;
                graph.liveNodeClonesByGuid[nodeData.guid] = clone;
                if (clone is CoreNode core)
                {
                    core.defaultBehaviourNodes = new List<BehaviourNode>();
                    core.defaultTriggerNodes = new List<TriggerNode>();
                    core.behaviourNodes = new List<BehaviourNode>();
                    core.triggerNodes = new List<TriggerNode>();
                }
            }
        }

        foreach (var nodeData in graph.nodes)
        {
            var parentClone = graph.liveNodeClonesByGuid[nodeData.guid];
            if (parentClone is CoreNode coreClone)
            {
                foreach (var childGuid in nodeData.childNodeGUIDs)
                {
                    var childClone = graph.liveNodeClonesByGuid[childGuid];

                    if (childClone is BehaviourNode behaviour)
                    {
                        coreClone.defaultBehaviourNodes.Add(behaviour);
                    }
                    else if (childClone is TriggerNode trigger)
                    {
                        coreClone.defaultTriggerNodes.Add(trigger);
                    }
                }
            }
        }


        foreach (var nodeData in graph.nodes)
        {
            if (!graph.liveNodeClonesByGuid.TryGetValue(nodeData.guid, out var clone)) continue;
            if (clone is not SubgraphNode subClone) continue;

            var guidMap = new Dictionary<string, string>();
            var liveInternalNodes = new List<NodeInstanceData>();

            foreach (var childGuid in nodeData.childNodeGUIDs)
            {
                var liveChild = graph.GetNodeInstance(childGuid);
                if (liveChild == null) continue;

                if (!string.IsNullOrEmpty(liveChild.sourceTemplateNodeGuid))
                {
                    guidMap[liveChild.sourceTemplateNodeGuid] = liveChild.guid;
                }
                liveInternalNodes.Add(liveChild);
            }

            subClone.internalNodes = liveInternalNodes;

            if (!string.IsNullOrEmpty(subClone.rootNodeGuid) && guidMap.TryGetValue(subClone.rootNodeGuid, out var liveRoot))
            {
                subClone.rootNodeGuid = liveRoot;
            }

            for (int i = 0; i < subClone.exposedSockets.Count; i++)
            {
                var info = subClone.exposedSockets[i];
                if (guidMap.TryGetValue(info.internalNodeGuid, out var liveOwner))
                {
                    info.internalNodeGuid = liveOwner;
                    subClone.exposedSockets[i] = info;
                }
                else
                {
                    Debug.LogWarning($"[Load] Subgraph '{subClone.name}': no live owner for template guid '{info.internalNodeGuid}'.");
                }
            }
        }

        foreach (var nodeData in graph.nodes)
        {
            foreach (var connectionData in nodeData.connections)
            {
                graph.CreateNodeLink(nodeData, graph.GetNodeInstance(connectionData.targetNodeGUID), connectionData);
            }
        }

        if (!string.IsNullOrEmpty(graph.entryPointControllerNodeGuid) &&
        graph.liveNodeClonesByGuid.TryGetValue(graph.entryPointControllerNodeGuid, out SpellNode entryNode))
        {
            graph.entryPointControllerNode = entryNode as EntryPointControlNode;
        }
        return graph;
    }

    public void CreateNodeLink(NodeInstanceData sourceData, NodeInstanceData targetData, NodeConnection connectionData)
    {
        if (!liveNodeClonesByGuid.TryGetValue(connectionData.fromOutputOwnerGUID, out var logicalSource) ||
        !liveNodeClonesByGuid.TryGetValue(connectionData.toInputOwnerGUID, out var logicalTarget))
        {
            Debug.LogError($"Could not find the live node clones for the connection owners. From: {connectionData.fromOutputOwnerGUID}, To: {connectionData.toInputOwnerGUID}");
            return;
        }
        var outputSocketDef = logicalSource.GetSockets().FirstOrDefault(s =>
        (s.Name == connectionData.fromOutputSocketName) || (s.TargetFieldName == connectionData.fromOutputSocketName));

        var inputSocketDef = logicalTarget.GetSockets().FirstOrDefault(s =>
            (s.Name == connectionData.toInputSocketName) || (s.TargetFieldName == connectionData.toInputSocketName));

        if (string.IsNullOrEmpty(outputSocketDef.Name) || string.IsNullOrEmpty(inputSocketDef.Name))
        {
            Debug.LogError($"SocketDefs not found on the resolved logical nodes. Looking for '{connectionData.fromOutputSocketName}' on '{logicalSource.name}' and '{connectionData.toInputSocketName}' on '{logicalTarget.name}'.");
            return;
        }

        /*Debug.Log($"CreateNodeLink: {sourceData.nodeTemplateName} [{outputSocketDef.Name} -> {inputSocketDef.Name}] {targetData.nodeTemplateName} | " +
                  $"ownerOut='{outputSocketDef.OwningNodeGUID}', ownerIn='{inputSocketDef.OwningNodeGUID}', " +
                  $"resolvedSource={logicalSource.GetType().Name}, resolvedTarget={logicalTarget.GetType().Name}");*/


        switch (outputSocketDef.Type)
        {
            case SocketType.ExecutionLink:
                if (logicalSource is EntryPointControlNode entryPoint && logicalTarget is CasterNode casterNode)
                {
                    int comboIndex = int.Parse(connectionData.fromOutputSocketName.Split(' ')[1]) - 1;
                    while (entryPoint.orderedEntries.Count <= comboIndex) entryPoint.orderedEntries.Add(null);
                    entryPoint.orderedEntries[comboIndex] = casterNode;
                }
                else if (logicalSource is CasterNode caster && (logicalTarget is CoreNode || logicalTarget is EffectNode))
                {
                    if (!caster.outcomeCoreNodes.Contains(logicalTarget))
                    {
                        caster.outcomeCoreNodes.Add(logicalTarget);
                    }
                }
                else if (logicalSource is CoreNode sourceCore && logicalTarget is TriggerNode trigger)
                {
                    if (!sourceCore.triggerNodes.Contains(trigger)) sourceCore.triggerNodes.Add(trigger);
                }
                else if (logicalSource is TriggerNode sourceTrigger && (logicalTarget is EffectNode || logicalTarget is CoreNode))
                {
                    if (!sourceTrigger.outcomeNodes.Contains(logicalTarget))
                        sourceTrigger.outcomeNodes.Add(logicalTarget);
                }
                break;

            case SocketType.BehaviourLink:
                if (logicalSource is BehaviourNode behaviour && logicalTarget is CoreNode targetCore)
                {
                    if (!targetCore.behaviourNodes.Contains(behaviour)) targetCore.behaviourNodes.Add(behaviour);
                }
                break;

            case SocketType.FilterLink:
                if (logicalSource is FilterNode filter && logicalTarget is TriggerNode targetTrigger)
                {
                    if (!targetTrigger.filterNodes.Contains(filter)) targetTrigger.filterNodes.Add(filter);
                }
                break;

            case SocketType.Data:
                if (logicalSource is ValueNode valueNode)
                {
                    AddPropertyBinding(logicalTarget, inputSocketDef, valueNode);
                }
                break;
        }
    }
    private static void AddPropertyBinding(SpellNode targetNode, SocketDefinition socketDef, ValueNode sourceNode)
    {
        Debug.Log($"AddPropertyBinding -> target={targetNode.GetType().Name}, field={socketDef.TargetFieldName}, owner={socketDef.OwningNodeGUID}");
        var binder = targetNode.valueContainers.FirstOrDefault(c =>
            c.TargetFieldName == socketDef.TargetFieldName &&
            c.OwningNodeGUID == socketDef.OwningNodeGUID);

        if (binder == null)
        {
            binder = new PropertyBinder
            {

                OwningNodeGUID = socketDef.OwningNodeGUID,
                TargetFieldName = socketDef.TargetFieldName
            };
            targetNode.valueContainers.Add(binder);
        }

        if (!binder.ModifyingNodes.Contains(sourceNode))
        {
            binder.ModifyingNodes.Add(sourceNode);
        }
    }

    public void InitilizeFromNodeData(SpellGraphController controller)
    {

        this.runeUIsByGuid = new Dictionary<string, RuneUI>();
        this.liveNodeClonesByGuid = new Dictionary<string, SpellNode>();

        foreach (var nodeData in this.nodes)
        {
            SpellNode nodeTemplate = controller.FindTemplateByName(nodeData.nodeTemplateName);
            if (nodeTemplate != null)
            {
                SpellNode clone = nodeTemplate.CloneThisNode();
                clone.InstanceGuid = nodeData.guid;
                this.liveNodeClonesByGuid[nodeData.guid] = clone;

                if (clone is CoreNode core)
                {
                    core.defaultBehaviourNodes = new List<BehaviourNode>();
                    core.defaultTriggerNodes = new List<TriggerNode>();
                    core.behaviourNodes = new List<BehaviourNode>();
                    core.triggerNodes = new List<TriggerNode>();
                }
            }
            else
            {
                Debug.LogWarning($"[Hydrate] Could not find template: {nodeData.nodeTemplateName}");
            }
        }

        foreach (var nodeData in this.nodes)
        {
            if (!this.liveNodeClonesByGuid.TryGetValue(nodeData.guid, out var parentClone)) continue;

            if (parentClone is CoreNode coreClone)
            {
                foreach (var childGuid in nodeData.childNodeGUIDs)
                {
                    if (!this.liveNodeClonesByGuid.TryGetValue(childGuid, out var childClone)) continue;

                    if (childClone is BehaviourNode behaviour)
                    {
                        coreClone.defaultBehaviourNodes.Add(behaviour);
                    }
                    else if (childClone is TriggerNode trigger)
                    {
                        coreClone.defaultTriggerNodes.Add(trigger);
                    }
                }
            }
        }

        foreach (var nodeData in this.nodes)
        {
            if (!this.liveNodeClonesByGuid.TryGetValue(nodeData.guid, out var clone)) continue;
            if (clone is not SubgraphNode subClone) continue;

            var guidMap = new Dictionary<string, string>();
            var liveInternalNodes = new List<NodeInstanceData>();

            foreach (var childGuid in nodeData.childNodeGUIDs)
            {
                var liveChild = this.GetNodeInstance(childGuid); 
                if (liveChild == null) continue;

                if (!string.IsNullOrEmpty(liveChild.sourceTemplateNodeGuid))
                {
                    guidMap[liveChild.sourceTemplateNodeGuid] = liveChild.guid;
                }
                liveInternalNodes.Add(liveChild);
            }

            subClone.internalNodes = liveInternalNodes;

            if (!string.IsNullOrEmpty(subClone.rootNodeGuid) && guidMap.TryGetValue(subClone.rootNodeGuid, out var liveRoot))
            {
                subClone.rootNodeGuid = liveRoot;
            }

            for (int i = 0; i < subClone.exposedSockets.Count; i++)
            {
                var info = subClone.exposedSockets[i];
                if (guidMap.TryGetValue(info.internalNodeGuid, out var liveOwner))
                {
                    info.internalNodeGuid = liveOwner;
                    subClone.exposedSockets[i] = info;
                }
                else
                {
                    Debug.LogWarning($"[Load] Subgraph '{subClone.name}': no live owner for template guid '{info.internalNodeGuid}'.");
                }
            }
        }

        foreach (var nodeData in this.nodes)
        {
            foreach (var connectionData in nodeData.connections)
            {
                this.CreateNodeLink(nodeData, this.GetNodeInstance(connectionData.targetNodeGUID), connectionData);
            }
        }

        if (!string.IsNullOrEmpty(this.entryPointControllerNodeGuid) &&
            this.liveNodeClonesByGuid.TryGetValue(this.entryPointControllerNodeGuid, out SpellNode entryNode))
        {
            this.entryPointControllerNode = entryNode as EntryPointControlNode;
        }
        else
        {
            Debug.LogWarning($"[Hydrate] Could not find Entry Point Node ({this.entryPointControllerNodeGuid}) for graph '{this.name}'.");
        }
    }

    #endregion
}
