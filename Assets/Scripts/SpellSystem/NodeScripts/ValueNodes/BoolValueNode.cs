using UnityEngine;

[CreateAssetMenu(fileName = "SetBool", menuName = "SpellNodes/ValueNodes/Bool Value Node")]
public class SetBoolValueNode : ValueNode<bool> 
{
    public bool value;
    public DataTypeTag tag; 

    public override DataTypeTag ValueTag => tag;

    public override ValueModifier<bool> GetModifier(SpellState state)
    {
        return new ValueModifier<bool>(value, ValueModifierType.Set);
    }
}