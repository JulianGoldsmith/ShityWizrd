using UnityEngine;

[CreateAssetMenu(menuName = "AI Commands/Actions/Execute Action")]
public class Action_ExecuteCommand : NPCCommand
{
    public override CommandType Type => CommandType.Action_Execute;
    public override void ActiveTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {
        var actionManager = manager.GetComponent<NPCActionManager>();
        var moveManager = manager.GetComponent<NPCMovementManager>();

        actionManager.StartAction(data.IntData);

       
    }
}