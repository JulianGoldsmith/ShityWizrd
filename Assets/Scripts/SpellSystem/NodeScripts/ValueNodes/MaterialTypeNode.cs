using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MaterialTypeNode", menuName = "SpellNodes/ValueNodes/Material Type Node")]
public class MaterialTypeNode : ValueNode<PHYSICS_OBJECT_MATERIAL>
{
    public PHYSICS_OBJECT_MATERIAL material;
    public DataTypeTag tag;

    public override Type ValueType => typeof(PHYSICS_OBJECT_MATERIAL);
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
public class RuntimeMaterialValueNode : RuntimeValueNodeBase<PHYSICS_OBJECT_MATERIAL>
{
    public PHYSICS_OBJECT_MATERIAL Material;

    public override ValueModifier<PHYSICS_OBJECT_MATERIAL> GetModifier(SpellTriggerInfo info)
    {
        return new ValueModifier<PHYSICS_OBJECT_MATERIAL>(Material, ValueModifierType.Set);
    }
}

public class RuntimeMaterialProperty : IRuntimeDataProperty
{
    public PHYSICS_OBJECT_MATERIAL BaseValue;
    public List<IRuntimeValueNode<PHYSICS_OBJECT_MATERIAL>> Modifiers;

    public RuntimeMaterialProperty(PHYSICS_OBJECT_MATERIAL baseValue) => BaseValue = baseValue;

    public void AddValueNode(IRuntimeValueNode node)
    {
        if (node is IRuntimeValueNode<PHYSICS_OBJECT_MATERIAL> matMod)
        {
            if (Modifiers == null) Modifiers = new List<IRuntimeValueNode<PHYSICS_OBJECT_MATERIAL>>();
            Modifiers.Add(matMod);
        }
    }

    public PHYSICS_OBJECT_MATERIAL GetValue(SpellTriggerInfo info)
    {
        if (Modifiers == null) return BaseValue;
        PHYSICS_OBJECT_MATERIAL finalValue = BaseValue;
        foreach (var mod in Modifiers)
        {
            var valMod = mod.GetModifier(info);
            if (valMod.Type == ValueModifierType.Set) finalValue = valMod.Value;
        }
        return finalValue;
    }
}