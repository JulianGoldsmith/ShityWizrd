using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public abstract class CasterNode : SpellNode
{

    public bool upperBodyOnly = true;
    public Transform castPointTransform;
    public float cooldown = 0.5f;
    public float comboResetTime = 1;


    public abstract void OnCastStarted(SpellState state, CastActionController castController);
    public abstract void OnCastCanceled(SpellState state, CastActionController castController);

    public virtual void OnCastUpdate(SpellState state, CastActionController castController)
    {

    }

    public List<SpellNode> outcomeCoreNodes;

    public override List<SocketDefinition> GetSockets()
    {
        var sockets = new List<SocketDefinition>
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
                    name: "Exec Out",
                    type: SocketType.ExecutionLink,
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
        return outcomeCoreNodes;
    }
}


