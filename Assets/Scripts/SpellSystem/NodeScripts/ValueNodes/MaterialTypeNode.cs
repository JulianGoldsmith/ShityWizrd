using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MaterialTypeNode", menuName = "SpellNodes/ValueNodes/Material Type Node")]
public class MaterialTypeNode: ValueNode<PHYSICS_OBJECT_MATERIAL>
{
    public PHYSICS_OBJECT_MATERIAL material;
    public DataTypeTag tag;

    public override Type ValueType => typeof(PHYSICS_OBJECT_MATERIAL);
    public override DataTypeTag ValueTag => tag;

    public override ValueModifier<PHYSICS_OBJECT_MATERIAL> GetModifier(SpellState state)
    {
        return new ValueModifier<PHYSICS_OBJECT_MATERIAL>(material, ValueModifierType.Set);
    }
}
