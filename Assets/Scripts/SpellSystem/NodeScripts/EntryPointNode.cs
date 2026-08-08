using UnityEngine;
using System.Collections.Generic;

public enum EntryPointType : byte
{
    SpawnCore = 0,
    Trigger = 1,
    Effect = 2
}

[CreateAssetMenu(fileName = "EntryPointNode", menuName = "SpellNodes/Entry Point")]
public class EntryPointNode : SpellNode
{
    [Header("Weapon Hardware Contract")]
    public EntryPointType Type = EntryPointType.SpawnCore;

    public override List<SocketDefinition> GetSockets()
    {
        var sockets = new List<SocketDefinition>();

        // The enum strictly dictates what the player is allowed to plug into this weapon!
        switch (Type)
        {
            case EntryPointType.SpawnCore:
                sockets.Add(new SocketDefinition("Spawn Core", SocketType.ExecutionLink, SocketDirection.Output, DataTypeTag.Generic, null, this.InstanceGuid));
                break;

            case EntryPointType.Trigger:
                sockets.Add(new SocketDefinition("Emission Trigger", SocketType.ExecutionLink, SocketDirection.Output, DataTypeTag.Generic, null, this.InstanceGuid));
                break;

            case EntryPointType.Effect:
                sockets.Add(new SocketDefinition("On Hit Effect", SocketType.ExecutionLink, SocketDirection.Output, DataTypeTag.Generic, null, this.InstanceGuid));
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