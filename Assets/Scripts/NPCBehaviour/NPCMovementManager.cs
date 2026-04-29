using Fusion;
using UnityEngine;
using UnityEngine.AI;

public class NPCMovementManager : NetworkBehaviour
{
    public NPCActiveRagdollController muscle;

    const int PATHCAPACITY = 3;
    const int MAX_WAYPOINTS = 8;
    [Networked, Capacity(PATHCAPACITY)]
    public NetworkArray<NPCPathData> PathBuffer { get; }

    [Networked, Capacity(PATHCAPACITY * MAX_WAYPOINTS)]
    public NetworkArray<Vector3> WaypointBuffer { get; }

    [Networked] public byte MasterPathCounter { get; set; }

    // REMOVED the global CurrentWaypointIndex from here!

    [Networked] public byte ActivePathID { get; set; }

    private NavMeshPath _navMeshPath;
    public Transform pathingSource;

    [Header("Debug")]
    public bool showDebug = false;

    public override void Spawned()
    {
        _navMeshPath = new NavMeshPath();
    }

    public void MoveInDirection(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude > 0.01f)
            muscle.SetMovementTarget(direction.normalized * speed);
        else
            muscle.SetMovementTarget(Vector3.zero);
    }

    public void MoveToPoint(Vector3 point, float speed)
    {
        Vector3 dir = point - pathingSource.transform.position;
        dir.y = 0;
        MoveInDirection(dir, speed);
    }

    public void LookInDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude > 0.01f)
            muscle.SetLookDirection(direction);
    }

    public void LookAtPoint(Vector3 point)
    {
        Vector3 dir = point - pathingSource.transform.position;
        dir.y = 0;
        LookInDirection(dir);
    }

    public void LookInMoveDirection()
    {
        Vector3 velocity = muscle.coreRB.linearVelocity;
        velocity.y = 0;
        LookInDirection(velocity);
    }

    // Creates a brand new path and assigns an ID
    public byte BakeNewPath(NetworkId targetID, Vector3 targetPos, bool hasLOS, Vector3[] corners)
    {
        MasterPathCounter++;
        byte newPathID = MasterPathCounter;
        int slot = newPathID % PATHCAPACITY;

        var pathData = PathBuffer[slot];
        pathData.PathID = newPathID;
        pathData.FinalTargetID = targetID;
        pathData.FinalPosition = targetPos;
        pathData.HasLineOfSight = hasLOS;
        pathData.WaypointCount = (byte)Mathf.Min(corners.Length, MAX_WAYPOINTS);

        // FIX: Start at 1 to skip our own feet!
        pathData.CurrentWaypointIndex = 1;

        PathBuffer.Set(slot, pathData);

        int waypointOffset = slot * MAX_WAYPOINTS;
        for (int i = 0; i < pathData.WaypointCount; i++)
        {
            WaypointBuffer.Set(waypointOffset + i, corners[i]);
        }

        return newPathID;
    }

    // Updates an existing path IF it hasn't been overwritten
    public void UpdatePath(byte pathID, NetworkId targetID, Vector3 targetPos, bool hasLOS, Vector3[] corners)
    {
        int slot = pathID % PATHCAPACITY;
        var existingPath = PathBuffer[slot];

        if (existingPath.PathID == pathID)
        {
            existingPath.FinalTargetID = targetID;
            existingPath.FinalPosition = targetPos;
            existingPath.HasLineOfSight = hasLOS;
            existingPath.WaypointCount = (byte)Mathf.Min(corners.Length, MAX_WAYPOINTS);

            // FIX: Re-calculating the path means corner[0] is our current pos. Seek corner 1!
            existingPath.CurrentWaypointIndex = 1;

            PathBuffer.Set(slot, existingPath);

            int waypointOffset = slot * MAX_WAYPOINTS;
            for (int i = 0; i < existingPath.WaypointCount; i++)
            {
                WaypointBuffer.Set(waypointOffset + i, corners[i]);
            }
        }
    }

    public Vector3 GetSteeringTarget(byte requestedPathID, Vector3 currentPos, bool debug = false)
    {
        // FIX: Don't let the visual render loop overwrite the physics state!
        if (!debug) ActivePathID = requestedPathID;

        int bufferSlot = requestedPathID % PATHCAPACITY;
        NPCPathData path = PathBuffer[bufferSlot];

        if (path.PathID != requestedPathID) return currentPos;

        // FIX: If WaypointCount is <= 1, it means the only corner is our feet, so we just use final targeting.
        if (path.WaypointCount <= 1 || path.CurrentWaypointIndex >= path.WaypointCount)
        {
            if (path.HasLineOfSight && path.FinalTargetID.IsValid && Runner.TryFindObject(path.FinalTargetID, out var targetObj))
            {
                return targetObj.transform.position;
            }

            return path.FinalPosition;
        }

        int waypointOffset = bufferSlot * MAX_WAYPOINTS;
        // Read index safely from the struct
        Vector3 targetWaypoint = WaypointBuffer[waypointOffset + path.CurrentWaypointIndex];

        float distSq = (targetWaypoint - currentPos).sqrMagnitude;
        if (distSq < 0.25f && !debug)
        {
            // Increment and SAVE the struct back to the array!
            path.CurrentWaypointIndex++;
            PathBuffer.Set(bufferSlot, path);
        }

        return targetWaypoint;
    }

    public Vector3[] CalculatePathCorners(Vector3 targetPosition)
    {
        bool foundPath = NavMesh.CalculatePath(pathingSource.position, targetPosition, NavMesh.AllAreas, _navMeshPath);

        if (foundPath && _navMeshPath.status == NavMeshPathStatus.PathComplete)
        {
            return _navMeshPath.corners;
        }

        return new Vector3[] { pathingSource.position, targetPosition };
    }

    public override void Render()
    {
        if (!showDebug || RuntimeDebugRenderer.Instance == null) return;

        for (int slot = 0; slot < PATHCAPACITY; slot++)
        {
            NPCPathData path = PathBuffer[slot];

            if (path.WaypointCount == 0) continue;

            bool isCurrentPath = (path.PathID == ActivePathID);
            bool isNextPath = (path.PathID != ActivePathID && path.PathID == MasterPathCounter);

            if (!isCurrentPath && !isNextPath) continue;

            Color pathColor = isCurrentPath ? Color.green : new Color(0.6f, 0.2f, 0.8f);

            int offset = slot * MAX_WAYPOINTS;
            Vector3 previousPoint = isCurrentPath ? pathingSource.position : WaypointBuffer[offset];

            for (int i = 0; i < path.WaypointCount; i++)
            {
                Vector3 currentPoint = WaypointBuffer[offset + i];
                Color pointColor = pathColor;

                // FIX: Update renderer to read from the struct's index
                if (isCurrentPath && i == path.CurrentWaypointIndex)
                {
                    pointColor = Color.cyan;
                    RuntimeDebugRenderer.DrawSphere(currentPoint, 0.25f, pointColor);
                }
                else if (i >= path.CurrentWaypointIndex || !isCurrentPath)
                {
                    RuntimeDebugRenderer.DrawSphere(currentPoint, 0.15f, pointColor);
                }

                if (i > 0 || isCurrentPath)
                {
                    RuntimeDebugRenderer.DrawLine(previousPoint, currentPoint, 0.03f, pathColor);
                }

                previousPoint = currentPoint;
            }

            RuntimeDebugRenderer.DrawBox(path.FinalPosition, Quaternion.identity, Vector3.one * 0.4f, Color.red);
        }

        Vector3 immediateTarget = GetSteeringTarget(ActivePathID, pathingSource.position, true);
        RuntimeDebugRenderer.DrawBox(immediateTarget + Vector3.up, Quaternion.identity, Vector3.one * 0.3f, Color.cyan);
    }
}

public struct NPCPathData : INetworkStruct
{
    public byte PathID;
    public NetworkId FinalTargetID;
    public Vector3 FinalPosition;
    public NetworkBool HasLineOfSight;
    public byte WaypointCount;
    public byte CurrentWaypointIndex;
}