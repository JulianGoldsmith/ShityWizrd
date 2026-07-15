using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MaterialTypeNode", menuName = "SpellNodes/ValueNodes/Material Type Node")]
public class MaterialTypeNode : ValueNode<ushort>
{
    [MaterialID]
    public ushort material;
    public DataTypeTag tag;
    
    public override Type ValueType => typeof(ushort);
    public override DataTypeTag ValueTag => tag;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        return new RuntimeMaterialValueNode()
        {
            Material = this.material
        };
    }
}

// THE RUNTIME MATH CLASS
public class RuntimeMaterialValueNode : RuntimeValueNodeBase<ushort>
{
    public ushort Material;

    public override ValueModifier<ushort> GetModifier(SpellTriggerInfo info)
    {
        return new ValueModifier<ushort>(Material, ValueModifierType.Set);
    }
}

public class RuntimeMaterialProperty : IRuntimeDataProperty
{
    public ushort BaseValue;
    public List<IRuntimeValueNode<ushort>> Modifiers;

    public RuntimeMaterialProperty(ushort baseValue) => BaseValue = baseValue;

    public void AddValueNode(IRuntimeValueNode node)
    {
        if (node is IRuntimeValueNode<ushort> matMod)
        {
            if (Modifiers == null) Modifiers = new List<IRuntimeValueNode<ushort>>();
            Modifiers.Add(matMod);
        }
    }

    public ushort GetValue(SpellTriggerInfo info)
    {
        if (Modifiers == null) return BaseValue;
        ushort finalValue = BaseValue;
        foreach (var mod in Modifiers)
        {
            var valMod = mod.GetModifier(info);
            if (valMod.Type == ValueModifierType.Set) finalValue = valMod.Value;
        }
        return finalValue;
    }
}