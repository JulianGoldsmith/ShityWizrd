using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class AnimTransition
{
    [AnimStateDropdown]
    public byte TargetStateID;
    public float BlendDurationSeconds = 0.2f;
    public int Priority = 0;

    // Conditions can ALSO use [SerializeReference] if we want polymorphic conditions!
    [SerializeReference, SubclassSelector]
    public List<AnimConditionBase> Conditions = new List<AnimConditionBase>();
}