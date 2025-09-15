using UnityEngine;
using Unity.Behavior; // The namespace for your Behaviour Graph

[RequireComponent(typeof(Health))]
public class StaggerOnDamage : MonoBehaviour
{
    private Health health;
    private BehaviorGraphAgent blackboard; 
 

    private const string IsStaggeredKey = "IsStaggered";

    private void Awake()
    {
        health = GetComponent<Health>();
        blackboard = GetComponent<BehaviorGraphAgent>();

    }

    private void OnEnable()
    {
        health.OnDamaged.AddListener(TriggerStagger);
    }

    private void OnDisable()
    {
        health.OnDamaged.RemoveListener(TriggerStagger);
    }

    private void OnDestroy()
    {
        health.OnDamaged.RemoveListener(TriggerStagger);
    }


    private void TriggerStagger()
    {
        if (blackboard != null)
        {
            blackboard.SetVariableValue(variableName: "IsStaggered", true);
            Debug.Log("Stagger triggered on " + name);
        }
    }
}