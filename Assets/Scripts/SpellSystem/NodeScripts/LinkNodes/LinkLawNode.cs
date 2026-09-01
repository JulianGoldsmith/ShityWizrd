using System.Collections.Generic;

public abstract class LinkLawNode : SpellNode
{
    public override List<SocketDefinition> GetSockets()
    {
        return new List<SocketDefinition> { new SocketDefinition("Link Law", SocketType.ExecutionLink, SocketDirection.Output, DataTypeTag.Generic, typeof(LinkNode), InstanceGuid) };
    }

    public override List<SpellNode> GetAllDependentNodes()
    {
        return new List<SpellNode>();
    }
}

public abstract class RuntimeLinkLaw : IRuntimeNode
{
}