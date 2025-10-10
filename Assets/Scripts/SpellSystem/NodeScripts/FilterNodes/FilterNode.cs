using System.Collections.Generic;
using UnityEngine;

public abstract class FilterNode : SpellNode
{
    public abstract bool Evaluate(GameObject go);

    public override List<SocketDefinition> GetSockets()
    {
        return new List<SocketDefinition>
            {
                new SocketDefinition(
                    name: "Filter Out",
                    type: SocketType.FilterLink,
                    direction: SocketDirection.Output,
                    tag: DataTypeTag.Generic,
                    dataType: typeof(FilterNode),
                    owningNodeGUID: this.InstanceGuid
                )
            };
    }
    public override List<SpellNode> GetAllDependentNodes()
    {
        return new List<SpellNode>();
    }

}
