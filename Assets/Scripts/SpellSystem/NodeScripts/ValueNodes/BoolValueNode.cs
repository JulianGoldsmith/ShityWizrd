using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SetBool", menuName = "SpellNodes/ValueNodes/Bool Value Node")]
public class SetBoolValueNode : ValueNode<bool>
{
    public bool value;
    public DataTypeTag tag;

    public override DataTypeTag ValueTag => tag;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        return new RuntimeBoolValueNode()
        {
            Value = this.value
        };
    }
}

// THE RUNTIME MATH CLASS
public class RuntimeBoolValueNode : RuntimeValueNodeBase<bool>
{
    public bool Value;

    public override ValueModifier<bool> GetModifier(SpellTriggerInfo info)
    {
        return new ValueModifier<bool>(Value, ValueModifierType.Set);
    }
}

public class RuntimeBoolProperty : IRuntimeDataProperty
{
    public bool BaseValue;
    public List<IRuntimeValueNode<bool>> Modifiers;

    public RuntimeBoolProperty(bool baseValue) => BaseValue = baseValue;

    public void AddValueNode(IRuntimeValueNode node)
    {
        if (node is IRuntimeValueNode<bool> boolMod)
        {
            if (Modifiers == null) Modifiers = new List<IRuntimeValueNode<bool>>();
            Modifiers.Add(boolMod);
        }
    }

    public bool GetValue(SpellTriggerInfo info)
    {
        if (Modifiers == null) return BaseValue;
        bool finalValue = BaseValue;
        foreach (var mod in Modifiers)
        {
            var valMod = mod.GetModifier(info);
            if (valMod.Type == ValueModifierType.Set) finalValue = valMod.Value;
        }
        return finalValue;
    }
}