using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(menuName = "AI Commands/Move/Pathfind To Target")]
public class Move_PathfindCommand : NPCCommand
{
    public override CommandType Type => CommandType.Move_PathfindToID;

    public override void PreTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {
        if (!manager.HasStateAuthority) return;

        var moveManager = manager.GetComponent<NPCMovementManager>();

        if (!manager.Runner.TryFindObject(data.TargetID, out var targetObj)) return;

        Vector3[] corners = moveManager.CalculatePathCorners(targetObj.transform.position);

        bool canSeeTarget = manager.aggroController.CanSeeTarget(targetObj);
        Vector3 targetPos = manager.aggroController.GetKnownPositionForTarget(targetObj);

        if (data.IntData == 0)
        {
            data.IntData = moveManager.BakeNewPath(data.TargetID, targetPos, canSeeTarget, corners);
        }
        else
        {
            moveManager.UpdatePath((byte)data.IntData, data.TargetID, targetPos, canSeeTarget, corners);
        }
    }

    public override void ActiveTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {
        var moveManager = manager.GetComponent<NPCMovementManager>();

        if (manager.HasStateAuthority && manager.Runner.TryFindObject(data.TargetID, out var targetObj))
        {
            Vector3 perceivedPos = manager.aggroController.GetKnownPositionForTarget(targetObj);
            bool hasLOS = manager.aggroController.CanSeeTarget(targetObj); // (You'd add this quick getter)

            Vector3[] corners = moveManager.CalculatePathCorners(perceivedPos);
            moveManager.UpdatePath((byte)data.IntData, data.TargetID, perceivedPos, hasLOS, corners);
        }

        Vector3 steeringTarget = moveManager.GetSteeringTarget((byte)data.IntData, manager.transform.position);

        moveManager.MoveToPoint(steeringTarget, data.FloatData);

        if (manager.Runner.TryFindObject(data.TargetID, out var lookTarget))
        {
            moveManager.LookAtPoint(lookTarget.transform.position);
        }
        else
        {
            moveManager.LookInMoveDirection();
        }

        //Debug.Log($"{this.name} Pathfinding to {lookTarget.gameObject.name} ");
    }
}