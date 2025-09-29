using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.VisualScripting;

public abstract class ValueNode : SpellNode
{
    public abstract System.Type ValueType { get; }
    public abstract DataTypeTag ValueTag { get; }

    public override List<SocketDefinition> GetSockets()
    {
        return new List<SocketDefinition>
            {
                new SocketDefinition(
                    name: "Value Out",
                    type: SocketType.Data,
                    direction: SocketDirection.Output,
                    tag: this.ValueTag,      
                    dataType: this.ValueType,
                    owningNodeGUID: this.InstanceGuid
                )
            };
    }
}

public abstract class ValueNode<T> : ValueNode
{
    public override System.Type ValueType => typeof(T);
    public abstract ValueModifier<T> GetModifier(SpellState state);
}

public enum ValueModifierType
{
    Set,
    Add,
    Multiply
}

public struct ValueModifier<T>
{
    public T Value;
    public ValueModifierType Type;

    public ValueModifier(T value, ValueModifierType type)
    {
        Value = value;
        Type = type;
    }
}

[System.Serializable]
public class PropertyBinder //Modifiers (intermediary parameters) = mutable properties which combine with base properties.
                            //This is the stuff spells can change, and can impact many derived properies.
{
    public string TargetSocketName;      // like motion Speed
    public string TargetFieldName;       // e.g speed
    public string OwningNodeGUID;   // The asset GUID of the Behaviour/effect e.g SimpleMovement
    public List<ValueNode> ModifyingNodes = new List<ValueNode>();
}

[AttributeUsage(AttributeTargets.Field)]
public class PromotableAttribute : Attribute
{
    public string DisplayName;
    public DataTypeTag Tag;

    public PromotableAttribute(string displayName, DataTypeTag tag = DataTypeTag.Generic)
    {
        this.DisplayName = displayName;
        this.Tag = tag;
    }
}