
using UnityEngine;
using System.Collections.Generic;
using System.Reflection; 
using System;


public abstract class BehaviourNode : SpellNode
{
    public abstract void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo);

    public override List<SocketDefinition> GetSockets()
    {
        var sockets = new List<SocketDefinition>
        {
            new SocketDefinition(
                name: "Behaviour Out",
                type: SocketType.BehaviourLink,
                direction: SocketDirection.Output,
                tag: DataTypeTag.Generic,
                dataType: null,
                owningNodeGUID: this.InstanceGuid
            )
        };

        var fields = this.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            var promotableAttr = field.GetCustomAttribute<PromotableAttribute>();
            if (promotableAttr != null)
            {
                sockets.Add(new SocketDefinition(
                    name: promotableAttr.DisplayName,
                    type: SocketType.Data,
                    direction: SocketDirection.Input,
                    tag: promotableAttr.Tag,
                    dataType: field.FieldType,
                    owningNodeGUID: this.InstanceGuid,
                    targetFieldName: field.Name
                ));
            }
        }
        return sockets;
    }

    public override List<SpellNode> GetAllDependentNodes()
    {
        return new List<SpellNode>();
    }
}

      