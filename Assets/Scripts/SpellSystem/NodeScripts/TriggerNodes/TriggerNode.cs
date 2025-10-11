using System.Collections.Generic;
using UnityEngine;

public abstract class TriggerNode : SpellNode
{

    public List<FilterNode> filterNodes = new();
    public List<SpellNode> outcomeNodes = new();

    public abstract void SetUp(GameObject spellCore, SpellState state);

    public void Execute()
    {

    }

    public override List<SocketDefinition> GetSockets()
    {
        return new List<SocketDefinition>
            {
                new SocketDefinition(
                    name: "Exec In",
                    type: SocketType.ExecutionLink,
                    direction: SocketDirection.Input,
                    tag: DataTypeTag.Generic,
                    dataType: null,
                    owningNodeGUID: this.InstanceGuid
                ),

                new SocketDefinition(
                    name: "Filters In",
                    type: SocketType.FilterLink,
                    direction: SocketDirection.Input,
                    tag: DataTypeTag.Generic,
                    dataType: typeof(FilterNode),
                    owningNodeGUID: this.InstanceGuid
                ),

                new SocketDefinition(
                    name: "On Event", 
                    type: SocketType.ExecutionLink,
                    direction: SocketDirection.Output,
                    tag: DataTypeTag.Generic,
                    dataType: null,
                    owningNodeGUID: this.InstanceGuid
                ),
            };
    }

    public override List<SpellNode> GetAllDependentNodes()
    {
        List<SpellNode> dependencies = new List<SpellNode>();
        if (filterNodes != null) dependencies.AddRange(filterNodes.ConvertAll(node => node as SpellNode));
        if (outcomeNodes != null) dependencies.AddRange(outcomeNodes);
        return dependencies;
    }

} 