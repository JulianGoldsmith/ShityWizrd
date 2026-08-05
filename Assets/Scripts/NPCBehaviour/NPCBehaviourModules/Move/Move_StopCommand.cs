using UnityEngine;

[CreateAssetMenu(fileName = "Move_StopProcessor", menuName = "AI Commands/Move/Move Stop")]
public class Move_StopCommand : NPCCommand
{
    public override CommandType Type => CommandType.Move_Stop;
    public override NPCCommandChannel Channel => NPCCommandChannel.Locomotion;

    public override void ActiveTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {
        NPCMovementManager movement = manager.GetComponent<NPCMovementManager>();
        movement.MoveInDirection(Vector3.zero, NPCMovementMode.Stop);
    }
}
