using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.InputSystem;
public class LevelGenerator : MonoBehaviour
{
    [Header("Level Bounds")]
    public Vector2Int gridSize = new Vector2Int(200, 200);

    public CorridorConfiguration corridorConfig;

    public float maxShortCorridorLength = 15f;
    public float maxLongCorridorLength = 30f;
    public float maxBraidCorridorLength = 60f;
    public float maxCorridorToCorridorLength = 25f;

    public float corridorAlignmentTolerance = 0.9f;
    public float pathfindingLengthMultiplier = 2.0f;

    public RoomTemplate corridorPrefab;

    public List<KeyRoomConfiguration> keyRoomConfigs;

    [Header("Room Prefabs")]
    public List<RoomTemplate> standardRooms;

    public float stepPauseDuration = 0.05f;
    public bool IsGenerating { get; private set; } = false;

    public float earlyConnectionBias = 0.8f;

    private int[,] _grid; // 0 = Empty, 1 = Occupied >2 = Corridor types
    private List<PlacedRoomInstance> _placedRooms;
    private List<FrontierConnection> _frontier;
    private List<FrontierConnection> _deadEndConnections;


    public bool manualStepMode = true;
    public KeyCode stepKey = KeyCode.Space;




    public CorridorBuilder corridorBuilder;
    private class RoomToPlace
    {
        public RoomTemplate Prefab;
        public RoomType Type;
        public List<DistanceRequirement> Requirements;
        public float BoundingRadius;
    }

    private class PlacedPoint
    {
        public Vector2Int Position;
        public RoomType Type;
        public List<DistanceRequirement> Requirements;
        public float BoundingRadius;
    }

    private void OnDrawGizmos()
    {
 
        if (_grid == null || !Application.isPlaying) return;

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                Color gizmoColor;
                switch (_grid[x, y])
                {
                    case 1: // Room
                        gizmoColor = new Color(0, 1, 0, 0.2f); // Faint green
                        break;
                    case 2: // Corridor
                        gizmoColor = new Color(0, 0, 1, 0.3f); // Faint blue
                        break;
                    case 3: // CorridorLong
                        gizmoColor = new Color(1, 0, 0, 0.3f); // Faint red
                        break;
                    case 4: // CorridorClosed
                        gizmoColor = new Color(1, 0, 1, 0.3f); // Faint purple
                        break;
                    case 5: // CorridorClosed
                        gizmoColor = new Color(1, 1, 0, 0.3f); // Faint orange
                        break;
                    default: // Empty
                        continue; // No need to draw anything for empty cells, it's faster.
                }

                Gizmos.color = gizmoColor;

                Vector3 cellCenter = new Vector3(x + 0.5f, 0, y + 0.5f);

                Gizmos.DrawCube(cellCenter, new Vector3(0.9f, 0.1f, 0.9f));
            }
        }

        
    }


    public void StartGeneration()
    {
        StopAllCoroutines();
        StartCoroutine(GenerateLevelCoroutine());
    }

    public void StopGeneration()
    {
        if (!IsGenerating)
        {
            return; 
        }
        Debug.Log("Generation STOPPED");
        StopAllCoroutines();

        IsGenerating = false;
    }

    private IEnumerator GenerateLevelCoroutine()
    {
        if (IsGenerating)
        {
            Debug.LogWarning("<color=red>already generating</color>");
            yield break;
        }

        IsGenerating = true;

        ClearExistingLevel();
        _grid = new int[gridSize.x, gridSize.y];
        _placedRooms = new List<PlacedRoomInstance>();
        _frontier = new List<FrontierConnection>();
        _deadEndConnections = new List<FrontierConnection>();
        Debug.Log("Starting New Level Gene");

        yield return StartCoroutine(PlaceKeyRoomsCoroutine());
        yield return StartCoroutine(GrowStandardRoomsCoroutine());

        Debug.Log("<color=green>Level Generation COMPLETE!</color>");

        
        corridorBuilder.BuildCorridors(_grid, gridSize);
        Debug.Log("Corridor visuals built");


        IsGenerating = false;
    }


    private void ClearExistingLevel()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
    }

    private IEnumerator PlaceKeyRoomsCoroutine()
    {

        Debug.Log("Placing Key Rooms");

        List<RoomToPlace> roomsToPlaceQueue = new List<RoomToPlace>();
        foreach (var config in keyRoomConfigs)
        {
            for (int i = 0; i < config.targetCount; i++)
            {
                if (config.prefabs.Count > 0)
                {
                    RoomTemplate prefab = config.prefabs[0]; // NEEDS randomizing //////////////////////////////////////////////////////////////////////////////////
                    roomsToPlaceQueue.Add(new RoomToPlace
                    {
                        Prefab = prefab,
                        Type = config.roomType,
                        Requirements = config.distanceRequirements,
                        BoundingRadius = prefab.GetBoundingRadius() 
                    });
                }
            }
        }

        // Sort by the single largets distance requirement found in each room's list
        roomsToPlaceQueue = roomsToPlaceQueue.OrderByDescending(room =>
            room.Requirements.Count > 0 ? room.Requirements.Max(req => req.minDistance) : 0
        ).ToList();

        List<PlacedPoint> placedPoints = new List<PlacedPoint>();
        foreach (var roomToPlace in roomsToPlaceQueue)
        {
            Vector2Int? potentialPosition = FindValidPointForRoom(roomToPlace, placedPoints);
   
            if (potentialPosition.HasValue)
            {
                Vector3Int gridPosition = new Vector3Int(potentialPosition.Value.x, 0, potentialPosition.Value.y);
                bool wasPlaced = false;


                List<int> rotations = new List<int> { 0, 90, 180, 270 };
                rotations.Shuffle(); 

                foreach (var angle in rotations)
                {
                    Quaternion initialRotation = Quaternion.Euler(0, angle, 0);

                    if (!CheckForCollision(roomToPlace.Prefab, gridPosition, initialRotation))
                    {
                        Debug.Log($"Placing {roomToPlace.Type} at {gridPosition} with rotation {angle}");
                        PlaceOneRoom(roomToPlace.Prefab, gridPosition, initialRotation, 0);

                        placedPoints.Add(new PlacedPoint
                        {
                            Position = potentialPosition.Value,
                            Type = roomToPlace.Type,
                            Requirements = roomToPlace.Requirements,
                            BoundingRadius = roomToPlace.BoundingRadius
                        });

                        wasPlaced = true;
                        yield return new WaitForSeconds(stepPauseDuration);
                        break; 
                    }
                }

                if (!wasPlaced)
                {
                    Debug.LogWarning($"failed Point {gridPosition} for {roomToPlace.Type} was valid for distance but all 4 rotations collided");
                }
            }
            else
            {
                Debug.LogWarning($"Failed to find a valid position for room {roomToPlace.Prefab.name} - {roomToPlace.Type} - check keyrooms values");
            }
        }
    }


    private IEnumerator GrowStandardRoomsCoroutine()
    {
        Debug.Log($"Got to place rooms and corridors with - {_frontier.Count} initial frontier connections");
        int maxIterations = 1000; 
        int currentIteration = 0;

        while (_frontier.Count > 0 && currentIteration < maxIterations)
        {
            FrontierConnection connection = SelectConnectionFromFrontier(); 

            bool wasRoomPlaced = TryProcessConnection(connection);

            if (wasRoomPlaced)
            {
                if (manualStepMode)
                {
                    yield return new WaitForKeyPress(stepKey);
                }
                else
                {
                    yield return new WaitForSeconds(stepPauseDuration);
                }
            }
            else
            {
                connection.OwningRoom.CloseConnection(connection.ConnectionData);
                _deadEndConnections.Add(connection);
            }

            currentIteration++;
        }

        if (currentIteration >= maxIterations)
        {
            Debug.LogWarning($"<color=red> REACHED MAX ITTERATIONS!! - check generation values</color>");
        }
        Debug.Log($"Finished placeing rooms&corridors - {_placedRooms.Count} total rooms placed");
    }

    private bool TryProcessConnection(FrontierConnection connection)
    {
        //Attempt to close a loop by building a corridor
        if (TryPlaceCorridor(connection, maxShortCorridorLength, _frontier, 2))
        {
            return true;
        }

        //placing a new room
        if (TryPlaceRoomAtConnection(connection))
        {
            return true; 
        }

        if (TryPlaceCorridor(connection, maxBraidCorridorLength, _deadEndConnections, 4))
        {
            return true;
        }

        if (TryPlaceCorridor(connection, maxLongCorridorLength, _frontier, 3))
        {
            return true;
        }

        if (TryPlaceCorridorToCorridor(connection, maxCorridorToCorridorLength, 5))
        {
            return true;
        }
        return false;
    }

    private FrontierConnection SelectConnectionFromFrontier()
    {
        if (_frontier.Count == 1)
        {
            FrontierConnection singleConn = _frontier[0];
            _frontier.RemoveAt(0);
            return singleConn;
        }

        if (Random.value < earlyConnectionBias)
        {
            int minDepth = _frontier.Min(c => c.depth);
            var earliestConnections = _frontier.Where(c => c.depth == minDepth).ToList();
            int randomIndex = Random.Range(0, earliestConnections.Count);
            FrontierConnection chosenConnection = earliestConnections[randomIndex];
            _frontier.Remove(chosenConnection);
            return chosenConnection;
        }
        else
        {
            int randomIndex = Random.Range(0, _frontier.Count);
            FrontierConnection chosenConnection = _frontier[randomIndex];
            _frontier.RemoveAt(randomIndex);
            return chosenConnection;
        }
    }

    private bool TryPlaceRoomAtConnection(FrontierConnection connection)
    {
        List<RoomTemplate> roomsToTry = new List<RoomTemplate>(standardRooms);
        roomsToTry.Shuffle();

        foreach (var roomPrefab in roomsToTry)
        {
            var newRoomConnections = new List<ConnectionPoint>(roomPrefab.Connections);
            newRoomConnections.Shuffle();

            foreach (var newRoomConnection in newRoomConnections)
            {
                if (newRoomConnection.Type != connection.Type) continue;

                Quaternion requiredRotation = Quaternion.LookRotation(-connection.WorldNormal, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(newRoomConnection.GetNormal(), Vector3.up));
                requiredRotation = Quaternion.Euler(0f, Mathf.Round(requiredRotation.eulerAngles.y / 90f) * 90f,0f);
                Vector3 idealPosition = connection.WorldPosition - requiredRotation * newRoomConnection.GetLocalPosition();

                Vector3Int finalPosition = Vector3Int.RoundToInt(idealPosition);

                if (CheckForCollision(roomPrefab, finalPosition, requiredRotation)) continue;


                connection.OwningRoom.CloseConnection(connection.ConnectionData);

                RoomTemplate newRoomInstance = PlaceOneRoom(roomPrefab, finalPosition, requiredRotation, connection.depth + 1);

                newRoomInstance.CloseConnection(newRoomConnection);

                return true;
            }
        }
        return false;
    }

    private bool CheckForCollision(RoomTemplate roomPrefab, Vector3Int gridPosition, Quaternion rotation)
    {
        BoundsInt roomGridBounds = CalculateRotatedBounds(gridPosition, roomPrefab, rotation);
        for (int x = roomGridBounds.xMin; x < roomGridBounds.xMax; x++)
        {
            for (int y = roomGridBounds.yMin; y < roomGridBounds.yMax; y++)
            {
                if (x < 0 || x >= gridSize.x || y < 0 || y >= gridSize.y) return true;
                if (_grid[x, y] != 0) return true;
            }
        }
        return false;
    }

    private BoundsInt CalculateRotatedBounds(Vector3Int gridPosition, RoomTemplate roomPrefab, Quaternion rotation)
    {
        Bounds localBounds = roomPrefab.RoomBounds;
        int width = Mathf.RoundToInt(localBounds.size.x);
        int depth = Mathf.RoundToInt(localBounds.size.z);

        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                Vector3 localPoint = new Vector3(x + 0.5f, 0, z + 0.5f);

                Vector3 worldPoint = gridPosition + (rotation * localPoint);

                Vector2Int gridCell = new Vector2Int(Mathf.FloorToInt(worldPoint.x), Mathf.FloorToInt(worldPoint.z));

                occupiedCells.Add(gridCell);
            }
        }

        if (occupiedCells.Count == 0) return new BoundsInt();

        int minX = occupiedCells.Min(c => c.x);
        int minY = occupiedCells.Min(c => c.y);
        int maxX = occupiedCells.Max(c => c.x);
        int maxY = occupiedCells.Max(c => c.y);

        return new BoundsInt(minX, minY, 0, (maxX - minX) + 1, (maxY - minY) + 1, 1);
    }

    private Vector2Int? FindValidPointForRoom(RoomToPlace roomToPlace, List<PlacedPoint> existingPoints)
    {
        int attempts = 30;

        for (int i = 0; i < attempts; i++)
        {
            Vector2Int candidate = new Vector2Int(Random.Range(0, gridSize.x), Random.Range(0, gridSize.y));
            bool isDistanceValid = true;

            foreach (var placedPoint in existingPoints)
            {
                float distance = Vector2Int.Distance(candidate, placedPoint.Position);

                float req1 = 0f, req2 = 0f;
                var reqFromNew = roomToPlace.Requirements.FirstOrDefault(r => r.againstType == placedPoint.Type);
                if (reqFromNew != null) req1 = reqFromNew.minDistance;
                var reqFromPlaced = placedPoint.Requirements.FirstOrDefault(r => r.againstType == roomToPlace.Type);
                if (reqFromPlaced != null) req2 = reqFromPlaced.minDistance;
                float designerDistance = Mathf.Max(req1, req2);
                float physicalDistance = roomToPlace.BoundingRadius + placedPoint.BoundingRadius;
                float requiredDistance = Mathf.Max(designerDistance, physicalDistance);

                if (distance < requiredDistance)
                {
                    //Debug.Log($"attempt {i + 1}/{attempts}  - {candidate} failed distance check against {placedPoint.Type} at {placedPoint.Position} (Actual: {distance:F1}, Required: {requiredDistance:F1})");
                    isDistanceValid = false;
                    break; 
                }
            }

            if (!isDistanceValid)
            {
                continue;
            }

          
            Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 4) * 90, 0);
            if (CheckForCollision(roomToPlace.Prefab, new Vector3Int(candidate.x, 0, candidate.y), randomRotation))
            {
                //Debug.Log($"Attempt {i + 1}/{attempts} Candidate {candidate} distance checks ok but failed physical collision check");
                continue;
            }

            //Debug.Log($" Found valid point {candidate} for {roomToPlace.Type} on attempt {i + 1}");
            return candidate;
        }

        //Debug.LogError($"failed to find any valid point for {roomToPlace.Type} after {attempts} attempts Check distance requirements and grid size");
        return null;
    }

    private RoomTemplate PlaceOneRoom(RoomTemplate roomPrefab, Vector3Int gridPosition, Quaternion rotation, int depth)
    {
        GameObject roomInstanceGO = Instantiate(roomPrefab.gameObject, gridPosition, rotation, this.transform);
        RoomTemplate roomInstance = roomInstanceGO.GetComponent<RoomTemplate>();
        roomInstance.InitializeRuntimeConnections();

        BoundsInt roomGridBounds = CalculateRotatedBounds(gridPosition, roomPrefab, rotation);

        for (int x = roomGridBounds.xMin; x < roomGridBounds.xMax; x++)
        {
            for (int y = roomGridBounds.yMin; y < roomGridBounds.yMax; y++)
            {
                if (x >= 0 && x < gridSize.x && y >= 0 && y < gridSize.y)
                {
                    _grid[x, y] = 1;
                }
            }
        }
        _placedRooms.Add(new PlacedRoomInstance
        {
            template = roomPrefab,
            gridPosition = new Vector2Int(Mathf.RoundToInt(gridPosition.x), Mathf.RoundToInt(gridPosition.z)),
            gridBounds = roomGridBounds,
            roomObject = roomInstanceGO,
            depth = depth
        });
        foreach (var conn in roomInstance.runtimeConnections)
        {
            if (conn.IsOpen)
            {
                _frontier.Add(new FrontierConnection
                {
                    WorldPosition = roomInstance.transform.TransformPoint(conn.templateData.GetLocalPosition()),
                    WorldNormal = roomInstance.transform.TransformDirection(conn.templateData.GetNormal()),
                    Type = conn.templateData.Type,
                    OwningRoom = roomInstance,
                    ConnectionData = conn.templateData,
                    depth = depth
                });
            }
        }
        return roomInstance;
    }

    private bool TryPlaceCorridor(FrontierConnection startConnection, float maxLength, List<FrontierConnection> targetConnectionList, int gridFillWith)
    {
        if (corridorConfig == null || corridorConfig.validConnectionTypes.Count == 0) return false;

        foreach (var endConnection in targetConnectionList)
        {
            if (endConnection == startConnection) continue;

            if (endConnection.OwningRoom == startConnection.OwningRoom) continue;

            if (!corridorConfig.validConnectionTypes.Contains(startConnection.Type) || !corridorConfig.validConnectionTypes.Contains(endConnection.Type)) continue;

            float distance = Vector3.Distance(startConnection.WorldPosition, endConnection.WorldPosition);

            if (distance > maxLength) continue; ///////////////////////////////// not sure if i should leave this in

            if (Vector3.Dot(startConnection.WorldNormal, endConnection.WorldNormal) > -corridorAlignmentTolerance) continue;

            //Debug.Log($"Potential Corridor Found between {startConnection.OwningRoom.name} and {endConnection.OwningRoom.name} attempting A*");

            int offset = Mathf.CeilToInt(corridorConfig.width / 2.0f);

            Vector3Int startGridPos = Vector3Int.RoundToInt(startConnection.WorldPosition) + Vector3Int.RoundToInt(startConnection.WorldNormal * offset);
            Vector3Int endGridPos = Vector3Int.RoundToInt(endConnection.WorldPosition) + Vector3Int.RoundToInt(endConnection.WorldNormal * offset);

            int directDistance = GetMDistance(new Vector2Int(startGridPos.x, startGridPos.z), new Vector2Int(endGridPos.x, endGridPos.z));
            int maxAllowedPathLength = Mathf.RoundToInt(directDistance * pathfindingLengthMultiplier);

            List<PathNode> path = FindPath(new Vector2Int(startGridPos.x, startGridPos.z), new Vector2Int(endGridPos.x, endGridPos.z), corridorConfig.width, maxAllowedPathLength);

            if (path != null) // valid path
            {
                Debug.Log($"<color=green>Found valid path of length {path.Count}</color>");
      

                foreach (var pathNode in path)
                {
                    int startOffset = -corridorConfig.width / 2;
                    int endOffset = (corridorConfig.width - 1) / 2;

                    for (int x = startOffset; x <= endOffset; x++)
                    {
                        for (int y = startOffset; y <= endOffset; y++)
                        {
                            Vector2Int tile = pathNode.position + new Vector2Int(x, y);
                            if (tile.x >= 0 && tile.x < gridSize.x && tile.y >= 0 && tile.y < gridSize.y)
                            {
                                _grid[tile.x, tile.y] = gridFillWith; 
                            }
                        }
                    }
                }

                startConnection.OwningRoom.CloseConnection(startConnection.ConnectionData);
                endConnection.OwningRoom.CloseConnection(endConnection.ConnectionData);
                if (targetConnectionList == _frontier)
                {
                    _frontier.Remove(endConnection);
                }
                else
                {
                    _deadEndConnections.Remove(endConnection);
                }

                return true;
            }else
        {
            //Debug.LogWarning($"<color=orange>failed could not find a path between {startGridPos} and {endGridPos}</color>");
        }
        }
        return false;
    }

    private bool TryPlaceCorridorToCorridor(FrontierConnection startConnection, float maxLength, int gridFillWith)
    {
        if (corridorConfig == null || corridorConfig.validConnectionTypes.Count == 0) return false;
        if (!corridorConfig.validConnectionTypes.Contains(startConnection.Type)) return false;

        Vector2Int? closestCorridorCell = null;
        float minDistance = float.MaxValue;

        Vector3Int startPosInt = Vector3Int.RoundToInt(startConnection.WorldPosition);

        for (int x = 0; x < gridSize.x; x++)
        {
            for (int y = 0; y < gridSize.y; y++)
            {
                if (_grid[x, y] == 2)
                {
                    float distance = Vector2Int.Distance(new Vector2Int(startPosInt.x, startPosInt.z), new Vector2Int(x, y));
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestCorridorCell = new Vector2Int(x, y);
                    }
                }
            }
        }

        if (closestCorridorCell.HasValue && minDistance <= maxLength)
        {
            Debug.Log($"Potential corridor connectin -  Target corridor cell: {closestCorridorCell.Value} Attempting pathfinding");

            int offset = Mathf.CeilToInt(corridorConfig.width / 2.0f);
            Vector3Int startGridPos = startPosInt + Vector3Int.RoundToInt(startConnection.WorldNormal * offset);
            Vector2Int endGridPos = closestCorridorCell.Value;

            int directDistance = GetMDistance(new Vector2Int(startGridPos.x, startGridPos.z), endGridPos);
            int maxAllowedPathLength = Mathf.RoundToInt(directDistance * pathfindingLengthMultiplier);

            List<PathNode> path = FindPath(new Vector2Int(startGridPos.x, startGridPos.z), endGridPos, corridorConfig.width, maxAllowedPathLength);

            if (path != null)
            {


                foreach (var pathNode in path)
                {
                    int startOffset = -corridorConfig.width / 2;
                    int endOffset = (corridorConfig.width - 1) / 2;

                    for (int x = startOffset; x <= endOffset; x++)
                    {
                        for (int y = startOffset; y <= endOffset; y++)
                        {
                            Vector2Int tile = pathNode.position + new Vector2Int(x, y);
                            if (tile.x >= 0 && tile.x < gridSize.x && tile.y >= 0 && tile.y < gridSize.y)
                            {
                                _grid[tile.x, tile.y] = gridFillWith; 
                            }
                        }
                    }
                }

                startConnection.OwningRoom.CloseConnection(startConnection.ConnectionData);
                return true;
            }
            else
            {
                //Debug.LogWarning($"<color=orange>failed could not find a corridr path to {endGridPos}</color>");
            }
        }

        return false;
    }

    private class PathNode
    {
        public Vector2Int position;
        public int gCost; 
        public int hCost;
        public int fCost { get { return gCost + hCost; } }

        public PathNode parent;

        public PathNode(Vector2Int position)
        {
            this.position = position;
        }
    }

    private List<PathNode> FindPath(Vector2Int start, Vector2Int end, int corridorWidth, int maxPathLength)
    {
        PathNode startNode = new PathNode(start);
        PathNode endNode = new PathNode(end);

        List<PathNode> openList = new List<PathNode> { startNode };
        HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();

        startNode.gCost = 0;
        startNode.hCost = GetMDistance(start, end);

        while (openList.Count > 0)
        {
            PathNode currentNode = openList.OrderBy(n => n.fCost).First();

            if (currentNode.position == endNode.position)
            {
                return ReconstructPath(currentNode);
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode.position);

            foreach (Vector2Int neighbourPos in GetNeighbours(currentNode.position))
            {
                if (closedList.Contains(neighbourPos)) continue;

                if (!IsRegionClear(neighbourPos, corridorWidth))
                {
                    closedList.Add(neighbourPos); 
                    continue;
                }

                int tentativeGCost = currentNode.gCost + GetMDistance(currentNode.position, neighbourPos);

                if (tentativeGCost > maxPathLength)
                {
                    continue; 
                }

                PathNode neighbourNode = openList.FirstOrDefault(n => n.position == neighbourPos);
                if (neighbourNode == null || tentativeGCost < neighbourNode.gCost)
                {
                    if (neighbourNode == null)
                    {
                        neighbourNode = new PathNode(neighbourPos);
                        openList.Add(neighbourNode);
                    }

                    neighbourNode.parent = currentNode;
                    neighbourNode.gCost = tentativeGCost;
                    neighbourNode.hCost = GetMDistance(neighbourPos, end);
                }
            }
        }
        if (closedList.Count > 0)
        {
            PathNode closestNode = null;
            int smallestHCost = int.MaxValue;
            foreach (var pos in closedList)
            {
                int hCost = GetMDistance(pos, end);
                if (hCost < smallestHCost)
                {
                    smallestHCost = hCost;
                    
                }
            }

            //Debug.LogWarning($"<color=orange>failed - pathfinder could not find a path,  got {closedList.Count} nodes and got within a distance  of {smallestHCost} tiles from target</color>");
        }
        else
        {
            Debug.LogError("<color=red>Pathfinding failed INSTANTLY, is the start point inside a wall or out of bounds?</color>");
        }

        return null; // No path found
    }

    private bool IsRegionClear(Vector2Int center, int width)
    {
        int startOffset = -width / 2;
        int endOffset = (width - 1) / 2;

        for (int x = startOffset; x <= endOffset; x++)
        {
            for (int y = startOffset; y <= endOffset; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                if (checkPos.x < 0 || checkPos.x >= gridSize.x || checkPos.y < 0 || checkPos.y >= gridSize.y) return false;

                if (_grid[checkPos.x, checkPos.y] != 0)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private List<PathNode> ReconstructPath(PathNode endNode)
    {
        List<PathNode> path = new List<PathNode>();
        PathNode currentNode = endNode;
        while (currentNode != null)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
        }
        path.Reverse();
        return path;
    }

    private List<Vector2Int> GetNeighbours(Vector2Int pos)
    {
        return new List<Vector2Int> {
        pos + Vector2Int.up,
        pos + Vector2Int.down,
        pos + Vector2Int.left,
        pos + Vector2Int.right
    };
    }

    private int GetMDistance(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}

[System.Serializable]
public class CorridorConfiguration
{
    [Tooltip("width of the corridor")]
    public int width = 4;

    public List<ConnectionType> validConnectionTypes;
}

[System.Serializable]
public class KeyRoomConfiguration
{
    public string name;
    public RoomType roomType; 
    public List<RoomTemplate> prefabs;
    public int targetCount;
    public List<DistanceRequirement> distanceRequirements;
}
[System.Serializable]
public class DistanceRequirement
{
    public RoomType againstType; 
    public float minDistance;
}


public class PlacedRoomInstance
{
    public RoomTemplate template;
    public Vector2Int gridPosition;
    public BoundsInt gridBounds; 
    public GameObject roomObject; 
    public int depth;
}

public class FrontierConnection
{
    public Vector3 WorldPosition;
    public Vector3 WorldNormal;
    public ConnectionType Type;
    public RoomTemplate OwningRoom;     
    public ConnectionPoint ConnectionData;
    public int depth;
}

public enum RoomType
{
    Standard,
    Start,
    Lair,
    Capture,
    Treasure
}

public class WaitForKeyPress : CustomYieldInstruction
{
    private Key m_Key;

    public WaitForKeyPress(KeyCode keyCode)
    {
        if (!Key.TryParse(keyCode.ToString(), out m_Key))
        {
            m_Key = Key.Space;
        }
    }
    public override bool keepWaiting
    {
        get
        {
            if (Keyboard.current != null && Keyboard.current[m_Key].wasPressedThisFrame)
            {
                return false; 
            }
            return true; 
        }
    }
}
