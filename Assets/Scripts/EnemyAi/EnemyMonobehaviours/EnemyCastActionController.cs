using UnityEngine;
using Unity.Behavior;


public class EnemyCastActionController : CastActionController
{
    [SerializeField] private BehaviorGraphAgent agent;

    [SerializeField] private string isCastingVariableName = "IsCasting";
    [SerializeField] private string isCastOffCoolDownVariableName = "IsCastOffCoolDown";
    [SerializeField] private string targetVariableName = "Target";

    public BlackboardVariable Target;

    private void Awake()
    {
        //if (animator == null) animator = GetComponentInChildren<Animator>();
        if (inventory == null) inventory = GetComponent<NetworkedInventoryManager>();
        //if (animationController == null) animationController = GetComponent<EnemyAnimationController>();
        if (agent == null) agent = this.GetComponent<BehaviorGraphAgent>();
        //if (animationController != null)
        //{
        //    animationController = GetComponent<GenericAnimationController>();
        //    animationController.OnAnimationEventTriggered += HandleAnimationEvent;
        //}
    }

    //private void Update()
    //{
    //    base.Update();
    //    agent.SetVariableValue(variableName: isCastingVariableName, isCasting);
    //    agent.SetVariableValue(variableName: isCastOffCoolDownVariableName, (currentAttackCooldown<=0));
    //}


    public override Vector3 GetAimTarget()
    {
        agent.GetVariable(variableName: targetVariableName, out BlackboardVariable<GameObject> Target);
        GameObject target = Target.Value;
        if (target != null)
        {
            return target.transform.position;
        }
        else
        {
            return transform.position + transform.forward * 10f;
        }
    }

    public override void ActivateHitbox(int hitBoxID, SpellState state)
    {
       
    }

    public override void DeactivateHitbox(int hitBoxID)
    {

    }

    public override EyePosAndLookDir GetEyePosAndLookDir()
    {
        throw new System.NotImplementedException();
    }
}
