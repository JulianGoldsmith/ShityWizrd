using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public abstract class EffectNode : SpellNode
{
    public CasterTriggerMethod casterTriggerMethod = CasterTriggerMethod.OnCast;

    [Header("Network Identity")]
    [Tooltip("This is assigned automatically when published to the Master Dictionary. Do not edit manually.")]
    public int NetworkStatusID = 0;

    [Header("Execution Settings")]
    public EffectLifecycle Lifecycle = EffectLifecycle.Duration;

    public abstract override IRuntimeNode CompileNode(SpellCompilationContext context);
    public abstract void Execute( List<SpellTriggerInfo> triggerInfo);

    public void Execute( SpellTriggerInfo singleTriggerInfo)
    {
        this.Execute(new List<SpellTriggerInfo> { singleTriggerInfo });
    }

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
                    owningNodeGUID: this.InstanceGuid
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


public enum EffectLifecycle
{
    Instant = 0,
    Duration = 1,
    Channeled = 2
}