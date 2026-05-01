using UnityEngine;

[CreateAssetMenu(menuName = "AI Commands/Look/Look At ID")]
public class Look_AtIDCommand : NPCCommand
{
    public override CommandType Type => CommandType.Look_AtID;

    public override void ActiveTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {
        var moveManager = manager.GetComponent<NPCMovementManager>();

        if (!manager.Runner.TryFindObject(data.TargetID, out var targetObj)) return;

        Vector3 myPos = manager.muscleController.GetCoreTransform().position;

        if (moveManager != null)
        {
            moveManager.LookInDirection(targetObj.transform.position - myPos);
        }
    }
}