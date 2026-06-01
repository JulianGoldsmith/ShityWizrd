using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FloatValueNode", menuName = "SpellNodes/ValueNodes/Float Value Node")]
public class FloatValueNode : ValueNode<float>
{
    public float value = 1f;
    public DataTypeTag tag;
    public ValueModifierType modifierType;

    public override Type ValueType => typeof(float);
    public override DataTypeTag ValueTag => tag;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        throw new NotImplementedException();
    }

    public override ValueModifier<float> GetModifier(SpellState state)
    {
        return new ValueModifier<float>(value, modifierType);
    }
}
