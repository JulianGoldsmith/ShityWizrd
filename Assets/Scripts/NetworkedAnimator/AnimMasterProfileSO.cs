using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMasterProfile", menuName = "CustomAnim/Master Profile")]
public class AnimMasterProfileSO : ScriptableObject
{
    [AnimStateDropdown]
    public byte DefaultEntryStateID;

    [SerializeReference, SubclassSelector]
    public List<AnimStateBase> AllStates = new List<AnimStateBase>();

    [SerializeField]
    public List<AnimTransition> AnyStateTransitions = new List<AnimTransition>();

    [Header("Blackboard Parameters")]
    public List<FloatParameterDef> FloatParameters = new List<FloatParameterDef>();
    public List<BoolParameterDef> BoolParameters = new List<BoolParameterDef>();
    public List<TriggerParameterDef> TriggerParameters = new List<TriggerParameterDef>(); // ADDED THIS
}

[System.Serializable]
public struct FloatParameterDef
{
    public string Name;
    public float DefaultValue;
}

[System.Serializable]
public struct BoolParameterDef
{
    public string Name;
    public bool DefaultValue;
}

[System.Serializable]
public struct TriggerParameterDef
{
    public string Name;
}

public enum AnimParamType { Float, Int, Bool, Trigger }

[System.Serializable]
public struct AnimParameterDef
{
    public string Name;
    public AnimParamType Type;
    public float DefaultFloat;
    public bool DefaultBool;
    public int DefaultInt;
}

public class AnimParameterAttribute : PropertyAttribute
{
    public AnimParamType ParameterType;

    public AnimParameterAttribute(AnimParamType type)
    {
        ParameterType = type;
    }
}

public class AnimStateDropdownAttribute : PropertyAttribute { }