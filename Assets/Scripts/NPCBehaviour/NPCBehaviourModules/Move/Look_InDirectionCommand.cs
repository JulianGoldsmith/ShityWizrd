using UnityEngine;

[CreateAssetMenu(menuName = "AI Commands/Look/Look In Direction")]
public class Look_InDirectionCommand : NPCCommand
{
    public override CommandType Type => CommandType.Look_InDirection;

    public override void ActiveTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {
        var moveManager = manager.GetComponent<NPCMovementManager>();

        if (moveManager != null)
        {
            moveManager.LookInDirection(data.VectorData);
        }
    }
}