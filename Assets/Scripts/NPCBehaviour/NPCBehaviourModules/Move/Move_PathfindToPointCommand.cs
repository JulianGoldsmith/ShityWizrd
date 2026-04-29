using UnityEngine;

[CreateAssetMenu(menuName = "AI Commands/Move/Pathfind To Point")]
public class Move_PathfindToPointCommand : NPCCommand
{
    public override void PreTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {
        if (!manager.HasStateAuthority) return;

        var moveManager = manager.GetComponent<NPCMovementManager>();

        Vector3 targetPos = data.VectorData;
        Vector3[] corners = moveManager.CalculatePathCorners(targetPos);

        if (data.IntData == 0)
        {
            data.IntData = moveManager.BakeNewPath(default, targetPos, false, corners);
        }
        else
        {
            moveManager.UpdatePath((byte)data.IntData, default, targetPos, false, corners);
        }
    }

    public override void ActiveTick(ref NPCCommandData data, NPCBehaviourManager manager, NPCActiveRagdollController muscle)
    {
        var moveManager = manager.GetComponent<NPCMovementManager>();

        if (manager.HasStateAuthority)
        {
            Vector3 targetPos = data.VectorData;
            Vector3[] corners = moveManager.CalculatePathCorners(targetPos);
            moveManager.UpdatePath((byte)data.IntData, default, targetPos, false, corners);
        }

        Vector3 steeringTarget = moveManager.GetSteeringTarget((byte)data.IntData, manager.transform.position);

        moveManager.MoveToPoint(steeringTarget, data.FloatData);

        moveManager.LookInMoveDirection();

         Debug.Log($"{this.name} Pathfinding to Point: {data.VectorData}");
    }
}