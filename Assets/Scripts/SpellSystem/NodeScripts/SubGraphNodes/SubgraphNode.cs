using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SubgraphNode", menuName = "SpellNodes/Subgraph Node")]
public class SubgraphNode : SpellNode
{
    [Header("Subgraph Data")]
    public byte RootNodeIndex;
    public SpellNetworkData InternalGraph;

    public List<ExposedSocketInfo> ExposedSockets = new List<ExposedSocketInfo>();

    public override List<SocketDefinition> GetSockets()
    {
        var sockets = new List<SocketDefinition>();
        // NOTE: We will rewrite this shortly to automatically generate 
        // sockets based on the new InternalGraph array!
        return sockets;
    }

    public override List<SpellNode> GetAllDependentNodes()
    {
        return new List<SpellNode>();
    }

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        return null;
    }
}

[System.Serializable]
public struct ExposedSocketInfo
{
    public string ExposedName;
    public byte InternalNodeIndex;    // Replaced GUID!
    public byte InternalSocketIndex;  // Replaced String Name!

    public SocketDirection Direction;
    public SocketType Type;
    public DataTypeTag Tag;
}