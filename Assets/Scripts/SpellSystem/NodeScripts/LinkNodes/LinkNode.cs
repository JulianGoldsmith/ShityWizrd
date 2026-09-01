using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Link Knot", menuName = "SpellNodes/Links/Link Knot")]
public class LinkNode : SpellNode
{
    public float Duration;
    public float SpellLoad = 1f;

    public override List<SocketDefinition> GetSockets()
    {
        return new List<SocketDefinition> { new SocketDefinition("Link Law", SocketType.ExecutionLink, SocketDirection.Input, DataTypeTag.Generic, typeof(LinkLawNode), InstanceGuid) };
    }

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        return new RuntimeLink
        {
            Duration = Duration,
            SpellLoad = SpellLoad
        };
    }

    public override List<SpellNode> GetAllDependentNodes()
    {
        return new List<SpellNode>();
    }
}

public class RuntimeLink : IRuntimeNode
{
    public float Duration;
    public float SpellLoad;

    public RuntimeLinkLaw Law;
}