using UnityEngine;

[CreateAssetMenu(menuName = "AI Commands/Look/Look At Point")]
public class Look_AtPointCommand : NPCCommand
{
    public override CommandType Type => CommandType.Look_AtPoint;

    public override void ActiveTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {
        var moveManager = manager.GetComponent<NPCMovementManager>();

        if (moveManager != null)
        {
            moveManager.LookInDirection(data.VectorData);
        }
    }
}