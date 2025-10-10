using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "EntryPointControler", menuName = "SpellNodes/EntryPointControlNode")]
public class EntryPointControlNode : SpellNode
{
    public int maxComboSize = 3;

    public List<CasterNode> orderedEntries = new List<CasterNode>();

    public override List<SocketDefinition> GetSockets()
    {
        var sockets = new List<SocketDefinition>();

        
        for (int i = 0; i < maxComboSize; i++)
        {
            sockets.Add(new SocketDefinition(
                name: $"Combo {i + 1}",
                type: SocketType.ExecutionLink,
                direction: SocketDirection.Output,
                tag: DataTypeTag.Generic,
                dataType: null, 
                owningNodeGUID: this.InstanceGuid
            ));
        }

        return sockets;
    }
    public override List<SpellNode> GetAllDependentNodes()
    {
        return orderedEntries.ConvertAll(node => (node as SpellNode));
    }

}
