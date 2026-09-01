using UnityEngine;
using System.Collections.Generic;

public enum EntryPointType : byte
{
    SpawnCore = 0,
    Trigger = 1,
    Effect = 2,
    Link = 3
}

[CreateAssetMenu(fileName = "EntryPointNode", menuName = "SpellNodes/Entry Point")]
public class EntryPointNode : SpellNode
{
    [Header("Weapon Hardware Contract")]
    public EntryPointType Type = EntryPointType.SpawnCore;

    public override List<SocketDefinition> GetSockets()
    {
        var sockets = new List<SocketDefinition>();

        switch (Type)
        {
            case EntryPointType.SpawnCore:
                sockets.Add(new SocketDefinition("Spawn Core", SocketType.ExecutionLink, SocketDirection.Output, DataTypeTag.Generic, null, InstanceGuid));
                break;

            case EntryPointType.Trigger:
                sockets.Add(new SocketDefinition("Emission Trigger", SocketType.ExecutionLink, SocketDirection.Output, DataTypeTag.Generic, null, InstanceGuid));
                break;

            case EntryPointType.Effect:
                sockets.Add(new SocketDefinition("On Hit Effect", SocketType.ExecutionLink, SocketDirection.Output, DataTypeTag.Generic, null, InstanceGuid));
                break;

            case EntryPointType.Link:
                sockets.Add(new SocketDefinition("Link", SocketType.ExecutionLink, SocketDirection.Output, DataTypeTag.Generic, null, InstanceGuid));
                break;
        }

        return sockets;
    }

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        return new RuntimeEntryPoint() { ExpectedType = this.Type };
    }

    public override List<SpellNode> GetAllDependentNodes() => new List<SpellNode>();
}