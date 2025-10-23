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

    private Light light;
    private HDAdditionalLightData hdLight;

    public void Init(SpellTriggerInfo _triggerInfo, float _lumens, float _radius)
    {
        triggerInfo = _triggerInfo;
        lumens = _lumens;
        radius = _radius;
        light = this.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = _radius;
 

        light.useColorTemperature = true;
        light.colorTemperature = 3938f;

        hdLight = gameObject.AddComponent<HDAdditionalLightData>();
        hdLight.shapeRadius = 5;
        light.lightUnit = LightUnit.Lumen; 
        light.intensity = lumens; 
        hdLight.EnableColorTemperature(true);
    }

    void FixedUpdate()
    {
        
    }
}