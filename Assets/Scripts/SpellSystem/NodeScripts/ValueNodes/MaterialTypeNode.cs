using System;
using UnityEngine;

[CreateAssetMenu(fileName = "MaterialTypeNode", menuName = "SpellNodes/ValueNodes/Material Type Node")]
public class MaterialTypeNode: ValueNode<PhysicsObjectMaterial>
{
    public PhysicsObjectMaterial material;
    public DataTypeTag tag;

    public override Type ValueType => typeof(PhysicsObjectMaterial);
    public override DataTypeTag ValueTag => tag;

    public override ValueModifier<PhysicsObjectMaterial> GetModifier(SpellState state)
    {
        return new ValueModifier<PhysicsObjectMaterial>(material, ValueModifierType.Set);
    }
}
