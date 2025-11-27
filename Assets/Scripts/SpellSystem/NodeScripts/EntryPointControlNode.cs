using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "EntryPointControler", menuName = "SpellNodes/EntryPointControlNode")]
public class EntryPointControlNode : SpellNode
{
    [Tooltip("How many combo slots this entry point exposes (Combo 1, Combo 2, ...).")]
    public int maxComboSize = 3;

    [HideInInspector]
    public List<List<SpellNode>> comboRoots = new List<List<SpellNode>>();

    public void EnsureComboCapacity()
    {
        while (comboRoots.Count < maxComboSize)
        {
            comboRoots.Add(new List<SpellNode>());
        }
    }

    public List<SpellNode> GetComboRoots(int index)
    {
        EnsureComboCapacity();

        if (index < 0 || index >= comboRoots.Count)
            return new List<SpellNode>();

        return comboRoots[index];
    }

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
        EnsureComboCapacity();

        var all = new List<SpellNode>();
        foreach (var roots in comboRoots)
        {
            if (roots != null)
                all.AddRange(roots);
        }
        return all;
    }
}
