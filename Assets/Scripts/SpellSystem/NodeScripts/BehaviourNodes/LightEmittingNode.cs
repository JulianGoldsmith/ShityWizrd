using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[CreateAssetMenu(fileName = "LightEmittingNode", menuName = "SpellNodes/Behaviour/LightEmittingNode")]
public class LightEmittingNode : BehaviourNode
{
    [Promotable("Radius", DataTypeTag.Radius)]
    public float radius = 20f;
    public float lumenPower = 3000f;

    public override IRuntimeNode CompileNode(SpellCompilationContext context)
    {
        throw new NotImplementedException();
    }

    public override void SetUp(GameObject spellCore, SpellTriggerInfo triggerInfo)
    {
        var gravity = spellCore.AddComponent<LightEmiitingSB>();
        gravity.Init(triggerInfo, lumenPower, radius);
    }
}

public class LightEmiitingSB : SpellBehaviour
{
    public float lumens;
    public float radius;

 

    public void Init(SpellTriggerInfo _triggerInfo, float _lumens, float _radius)
    {
        triggerInfo = _triggerInfo;
        lumens = _lumens;
        radius = _radius;
    

        
    }

    void FixedUpdate()
    {
        
    }
}