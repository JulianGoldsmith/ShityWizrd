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
        // Spit out the stateless math block!
        return new RuntimeFloatValueNode()
        {
            Value = this.value,
            ModifierType = this.modifierType
        };
    }
}

// THE RUNTIME MATH CLASS
public class RuntimeFloatValueNode : RuntimeValueNodeBase<float>
{
    public float Value;
    public ValueModifierType ModifierType;

    // The math evaluates here, entirely in Engine RAM!
    public override ValueModifier<float> GetModifier(SpellTriggerInfo info)
    {
        return new ValueModifier<float>(Value, ModifierType);
    }
}