using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "SubgraphNode", menuName = "SpellNodes/Subgraph Node")]
public class SubgraphNode : SpellNode
{
    [Header("Subgraph Data")]
    public string rootNodeGuid;
    public List<NodeInstanceData> internalNodes = new List<NodeInstanceData>();

    public List<ExposedSocketInfo> exposedSockets = new List<ExposedSocketInfo>();


    public override List<SocketDefinition> GetSockets()
    {
        var sockets = new List<SocketDefinition>();
        if (SpellGraphController.Instance == null) return sockets; // Safety check

        // 1. Find the Root Node Template to determine the primary socket
        var rootNodeData = internalNodes.FirstOrDefault(n => n.guid == rootNodeGuid);
        if (rootNodeData != null)
        {
            var rootNodeTemplate = SpellGraphController.Instance.FindTemplateByName(rootNodeData.nodeTemplateName);
            if (rootNodeTemplate != null)
            {
                string ownerForExecIn = rootNodeGuid;
                if (rootNodeTemplate.category == RuneCategoryTag.Core)
                {
                    sockets.Add(new SocketDefinition(
                        name: "Exec In",
                        type: SocketType.ExecutionLink,
                        direction: SocketDirection.Input,
                        tag: DataTypeTag.Generic,
                        dataType: null,
                        owningNodeGUID: ownerForExecIn
                    ));
                }
                else if (rootNodeTemplate.category == RuneCategoryTag.Trigger)
                {
                    sockets.Add(new SocketDefinition("Exec In", SocketType.ExecutionLink, SocketDirection.Input, DataTypeTag.Generic, null, ownerForExecIn));
                }
                else if (rootNodeTemplate.category == RuneCategoryTag.Effect)
                {
                    sockets.Add(new SocketDefinition("Exec In", SocketType.ExecutionLink, SocketDirection.Input, DataTypeTag.Generic, null, ownerForExecIn));
                }
                else if (rootNodeTemplate.category == RuneCategoryTag.Behaviour)
                {
                    sockets.Add(new SocketDefinition("Behaviour Out", SocketType.BehaviourLink, SocketDirection.Output, DataTypeTag.Generic, null, ownerForExecIn));
                }
                // Add other categories as needed (Filter, etc.)
            }
        }

        foreach (var exposed in exposedSockets)
        {
            var internalNode = internalNodes.FirstOrDefault(n => n.guid == exposed.internalNodeGuid);
            if (internalNode == null) continue;

            var template = SpellGraphController.Instance.FindTemplateByName(internalNode.nodeTemplateName);
            if (template == null) continue;

            var originalSocket = template.GetSockets().FirstOrDefault(s =>
                s.Name == exposed.internalSocketName ||              
                s.TargetFieldName == exposed.internalSocketName);

            if (string.IsNullOrEmpty(originalSocket.Name))
            {
                Debug.LogError($"[Subgraph] Couldn’t resolve socket '{exposed.internalSocketName}' on template '{template.name}'.");
                continue;
            }

            sockets.Add(new SocketDefinition(
                name: exposed.exposedName,
                type: originalSocket.Type,
                direction: originalSocket.Direction,
                tag: originalSocket.Tag,
                dataType: originalSocket.DataType,
                owningNodeGUID: exposed.internalNodeGuid,
                targetFieldName: exposed.internalSocketName 
            ));
        }
        return sockets;
    }

    public override List<SpellNode> GetAllDependentNodes()
    {
        // This is used for Buffers.
        // Subgraphs don't 'exist' outside of spelleditor,
        // so this is left blank.
        Debug.LogError("Trying to get the dependent nodes of a subgraph.");
        return new List<SpellNode>();
    }
}

[System.Serializable]
public struct ExposedSocketInfo
{
    public string exposedName;      // given name 
    public string internalNodeGuid; // GUID of the node inside the subgraph
    public string internalSocketName; //original Name
    public SocketDirection direction;
    public SocketType type;
    public DataTypeTag tag;
}
