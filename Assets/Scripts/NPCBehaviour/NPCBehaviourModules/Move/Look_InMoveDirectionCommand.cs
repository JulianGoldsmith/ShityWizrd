using UnityEngine;

[CreateAssetMenu(menuName = "AI Commands/Look/Look In Move Direction")]
public class Look_InMoveDirectionCommand : NPCCommand
{
    public override void ActiveTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {
        var moveManager = manager.GetComponent<NPCMovementManager>();

        if (moveManager != null)
        {
            moveManager.LookInMoveDirection();
        }
    }
}