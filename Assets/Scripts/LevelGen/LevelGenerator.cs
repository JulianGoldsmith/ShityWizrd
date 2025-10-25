using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine.InputSystem;
using System;

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

    [Header("Seeded RNG")]
    public bool useSeed = true;
    public int seed = 12345;
    private DeterministicRng _rng;


    public int maxKeyRoomPlacementAttempts = 25;


    [Header("SpawnPoint stuffs")]
    public bool IsLevelGenerated { get; private set; } = false;
    public Transform StartRoomSpawnPoint { get; private set; }
    public event Action OnLevelReady;
    PlacedRoomInstance startRoom;


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
                    case 0:
                        gizmoColor = new Color(1, 1, 1, 0.2f);
                        break;
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


    public void StartGeneration(int _seed = -1)
    {
        StopAllCoroutines();
        StartCoroutine(GenerateLevelCoroutine(_seed));
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

    private IEnumerator GenerateLevelCoroutine(int _seed = -1)
    {
        this.seed = _seed;
        _rng = new DeterministicRng(seed);
        Debug.Log($"Level seed = {seed} (rng initialized)");

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

        BrickUpEntrances();
        Debug.Log("Corridor visuals built");


        IsGenerating = false;

        if (startRoom != null)
        {
            StartRoomSpawnPoint = startRoom.roomObject.GetComponent<RoomTemplate>().spawnPointInRoom;
            Debug.Log(StartRoomSpawnPoint.position);
            IsLevelGenerated = true;
            OnLevelReady?.Invoke();
        }
        else
        {
            Debug.LogError("Level generation complete but no Start Room was found!");
        }

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
        for (int attemptIndex = 0; attemptIndex < maxKeyRoomPlacementAttempts; attemptIndex++) 
        {
           
            _rng = new DeterministicRng(seed + attemptIndex);


            _grid = new int[gridSize.x, gridSize.y];                                        
            _placedRooms.Clear();                                                           
            _frontier.Clear();                                                               
            _deadEndConnections.Clear();

            List<GameObject> attemptSpawnedRooms = new List<GameObject>();


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


            bool allPlacedThisAttempt = true;


            List<PlacedPoint> placedPoints = new List<PlacedPoint>();
            foreach (var roomToPlace in roomsToPlaceQueue)
            {
                Vector2Int? potentialPosition = FindValidPointForRoom(roomToPlace, placedPoints);

                if (potentialPosition.HasValue)
                {
                    Vector3Int gridPosition = new Vector3Int(potentialPosition.Value.x, 0, potentialPosition.Value.y);
                    bool wasPlaced = false;


                    List<int> rotations = new List<int> { 0, 90, 180, 270 };
                    rotations.Shuffle(_rng);



                    foreach (var angle in rotations)
                    {
                        Quaternion initialRotation = Quaternion.Euler(0, angle, 0);

                        if (!CheckForCollision(roomToPlace.Prefab, gridPosition, initialRotation))
                        {
                            //Debug.Log($"Placing {roomToPlace.Type} at {gridPosition} with rotation {angle}");
                            RoomTemplate instance = PlaceOneRoom(roomToPlace.Prefab, gridPosition, initialRotation, 0); 
                            attemptSpawnedRooms.Add(instance.gameObject);

                            placedPoints.Add(new PlacedPoint
                            {
                                Position = potentialPosition.Value,
                                Type = roomToPlace.Type,
                                Requirements = roomToPlace.Requirements,
                                BoundingRadius = roomToPlace.BoundingRadius
                            });

                            wasPlaced = true;

                          

                            if (manualStepMode)
                                yield return new WaitForKeyPress(stepKey);
                            else
                                yield return new WaitForSeconds(stepPauseDuration);

                            break;
                        }
                    }

                    if (!wasPlaced)
                    {
                        Debug.LogWarning($"failed Point {gridPosition} for {roomToPlace.Type} was valid for distance but all 4 rotations collided");
                        allPlacedThisAttempt = false;                                     
                        break;
                    }
                    else
                    {
                        if (roomToPlace.Type == RoomType.Start)
                        {
                            startRoom = _placedRooms.Last();
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"Failed to find a valid position for room {roomToPlace.Prefab.name} - {roomToPlace.Type} - check keyrooms values");
                    allPlacedThisAttempt = false; 
                    break;
                }
            }
            if (allPlacedThisAttempt)                                                   
            {
                Debug.Log($"<color=green>All key rooms placed on attempt {attemptIndex + 1}</color>"); 
                yield break; // success; exit coroutine                                       
            }
            Debug.LogWarning($"Attempt {attemptIndex + 1}/{maxKeyRoomPlacementAttempts} failed. Rolling back and retrying...");

            foreach (var go in attemptSpawnedRooms)                                      
            {
                if (go != null) Destroy(go);                                              
            }

            _grid = new int[gridSize.x, gridSize.y];                                    
            _placedRooms.Clear();                                                        
            _frontier.Clear();                                                            
            _deadEndConnections.Clear();                                                  

            if (!manualStepMode) yield return new WaitForSeconds(stepPauseDuration);
        }
        Debug.LogError($"<color=red>Failed to place all key rooms after {maxKeyRoomPlacementAttempts} attempts.</color>"); // <<< NEW

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
                connection.OwningRoom.CloseConnectionFromSearch(connection.ConnectionData, false);
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

        if (_rng.NextFloat01() < earlyConnectionBias)
        {
            int minDepth = _frontier.Min(c => c.depth);
            var earliestConnections = _frontier.Where(c => c.depth == minDepth).ToList();
            int randomIndex = _rng.NextInt(0, earliestConnections.Count);
            FrontierConnection chosenConnection = earliestConnections[randomIndex];
            _frontier.Remove(chosenConnection);
            return chosenConnection;
        }
        else
        {
            int randomIndex = _rng.NextInt(0, _frontier.Count);
            FrontierConnection chosenConnection = _frontier[randomIndex];
            _frontier.RemoveAt(randomIndex);
            return chosenConnection;
        }
    }

    private bool TryPlaceRoomAtConnection(FrontierConnection connection)
    {
        List<RoomTemplate> roomsToTry = new List<RoomTemplate>(standardRooms);
        roomsToTry.Shuffle(_rng);

        foreach (var roomPrefab in roomsToTry)
        {
            //var newRoomConnections = new List<ConnectionPoint>(roomPrefab.Connections);
            //newRoomConnections.Shuffle();

            var indices = Enumerable.Range(0, roomPrefab.Connections.Count).ToList();
            indices.Shuffle(_rng);

            //for (int i = 0; i < newRoomConnections.Count; i++)
            //{
            //    var newRoomConnection = newRoomConnections[i];
            //    if (newRoomConnection.Type != connection.Type) continue;

            for (int prefabIndexIter = 0; prefabIndexIter < indices.Count; prefabIndexIter++)
            {
                int prefabIndex = indices[prefabIndexIter];
                var newRoomConnection = roomPrefab.Connections[prefabIndex];
                if (newRoomConnection.Type != connection.Type) continue;


                Quaternion requiredRotation = Quaternion.LookRotation(-connection.WorldNormal, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(newRoomConnection.GetNormal(), Vector3.up));
                requiredRotation = Quaternion.Euler(0f, Mathf.Round(requiredRotation.eulerAngles.y / 90f) * 90f,0f);
                Vector3 idealPosition = connection.WorldPosition - requiredRotation * newRoomConnection.GetLocalPosition();

                Vector3Int finalPosition = Vector3Int.RoundToInt(idealPosition);

                if (CheckForCollision(roomPrefab, finalPosition, requiredRotation)) continue;


                connection.OwningRoom.CloseConnectionFromSearch(connection.ConnectionData, true);

                RoomTemplate newRoomInstance = PlaceOneRoom(roomPrefab, finalPosition, requiredRotation, connection.depth + 1, prefabIndex);

                //int idx = roomPrefab.Connections.IndexOf(newRoomConnection);

                //ConnectionPoint instanceConn = newRoomInstance.Connections[idx];

                //newRoomInstance.CloseConnectionFromSearch(instanceConn, true);

                return true;
            }
        }
        return false;
    }








    private HashSet<Vector2Int> GetRotatedFootprintCells(RoomTemplate roomPrefab, Vector3Int gridPosition, Quaternion rotation)
    {
        HashSet<Vector2Int> occupiedCells = new HashSet<Vector2Int>();

        if (roomPrefab.filled == null || roomPrefab.filled.Length == 0)
        {
            Debug.LogWarning($"Room prefab {roomPrefab.name} has no 'filled' footprint data.", roomPrefab);
            return occupiedCells;
        }

        // MODIFIED: Get the min corner of the room's bounds
        Vector3 min = roomPrefab.RoomBounds.min;
        int width = roomPrefab.filled.Length;

        for (int x = 0; x < width; x++)
        {
            if (roomPrefab.filled[x] == null || roomPrefab.filled[x].row == null) continue;

            int depth = roomPrefab.filled[x].row.Length;
            for (int z = 0; z < depth; z++)
            {
                if (roomPrefab.filled[x].row[z])
                {
                    // MODIFIED: Create the local point using the *same offset* as the baking function
                    Vector3 localPoint = new Vector3(min.x + x + 0.5f, 0, min.z + z + 0.5f);

                    // (The rest is the same)
                    Vector3 worldPoint = gridPosition + (rotation * localPoint);
                    Vector2Int gridCell = new Vector2Int(Mathf.FloorToInt(worldPoint.x), Mathf.FloorToInt(worldPoint.z));
                    occupiedCells.Add(gridCell);
                }
            }
        }

        return occupiedCells;
    }




    private bool CheckForCollision(RoomTemplate roomPrefab, Vector3Int gridPosition, Quaternion rotation)
    {
        // Get the precise list of cells this room would occupy
        HashSet<Vector2Int> footprintCells = GetRotatedFootprintCells(roomPrefab, gridPosition, rotation);

        foreach (Vector2Int cell in footprintCells)
        {
            // Check 1: Is this cell outside the level's boundaries?
            if (cell.x < 0 || cell.x >= gridSize.x || cell.y < 0 || cell.y >= gridSize.y)
            {
                return true; // Collision (out of bounds)
            }

            // Check 2: Is this grid cell already occupied by another room or corridor?
            if (_grid[cell.x, cell.y] != 0)
            {
                return true; // Collision (occupied)
            }
        }

        // If we checked all cells and found no problems, it's clear!
        return false;
    }




    //private bool CheckForCollision(RoomTemplate roomPrefab, Vector3Int gridPosition, Quaternion rotation)
    //{
    //    BoundsInt roomGridBounds = CalculateRotatedBounds(gridPosition, roomPrefab, rotation);
    //    for (int x = roomGridBounds.xMin; x < roomGridBounds.xMax; x++)
    //    {
    //        for (int y = roomGridBounds.yMin; y < roomGridBounds.yMax; y++)
    //        {
    //            if (x < 0 || x >= gridSize.x || y < 0 || y >= gridSize.y) return true;
    //            if (_grid[x, y] != 0) return true;
    //        }
    //    }
    //    return false;
    //}


    private RoomTemplate PlaceOneRoom(RoomTemplate roomPrefab, Vector3Int gridPosition, Quaternion rotation, int depth, int prefabConnToAttach = -1)
    {
        GameObject roomInstanceGO = Instantiate(roomPrefab.gameObject, gridPosition, rotation, this.transform);
        RoomTemplate roomInstance = roomInstanceGO.GetComponent<RoomTemplate>();
        roomInstance.InitializeRuntimeConnections();

        // MODIFIED: Get the footprint cells just like we did in the collision check
        HashSet<Vector2Int> footprintCells = GetRotatedFootprintCells(roomPrefab, gridPosition, rotation);

        // MODIFIED: Stamp the footprint onto the grid
        foreach (Vector2Int cell in footprintCells)
        {
            // Check bounds before stamping
            if (cell.x >= 0 && cell.x < gridSize.x && cell.y >= 0 && cell.y < gridSize.y)
            {
                _grid[cell.x, cell.y] = 1; // 1 = Room
            }
        }

        // MODIFIED: We still need bounds for the PlacedRoomInstance, 
        // so we calculate it from the footprint cells we just found.
        BoundsInt roomGridBounds;
        if (footprintCells.Count == 0)
        {
            // Handle empty footprint case
            roomGridBounds = new BoundsInt(gridPosition.x, gridPosition.z, 0, 0, 0, 0);
        }
        else
        {
            // Find the min/max X and Y from the footprint to create a bounding box
            int minX = footprintCells.Min(c => c.x);
            int minY = footprintCells.Min(c => c.y);
            int maxX = footprintCells.Max(c => c.x);
            int maxY = footprintCells.Max(c => c.y);

            // Create the BoundsInt from the min/max values
            roomGridBounds = new BoundsInt(minX, minY, 0, (maxX - minX) + 1, (maxY - minY) + 1, 1);
        }

        // Add to our list of placed rooms, now with the correct bounds
        _placedRooms.Add(new PlacedRoomInstance
        {
            template = roomPrefab,
            gridPosition = new Vector2Int(Mathf.RoundToInt(gridPosition.x), Mathf.RoundToInt(gridPosition.z)),
            gridBounds = roomGridBounds, // Using our new, accurately calculated bounds
            roomObject = roomInstanceGO,
            depth = depth
        });


        // (This part for handling connections remains unchanged)
        for (int i = 0; i < roomInstance.runtimeConnections.Count; i++)
        {
            var conn = roomInstance.runtimeConnections[i];
            if (i == prefabConnToAttach)
            {
                roomInstance.CloseConnectionFromSearch(conn.templateData, true);
                continue;
            }

            if (conn.IsOpenForSearch)
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
            Vector2Int candidate = new Vector2Int(_rng.NextInt(0, gridSize.x), _rng.NextInt(0, gridSize.y));
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


            int[] rotChoices = { 0, 90, 180, 270 };
            int rotIndex = _rng.NextInt(0, rotChoices.Length); 
            Quaternion randomRotation = Quaternion.Euler(0, rotChoices[rotIndex], 0);

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

    //private RoomTemplate PlaceOneRoom(RoomTemplate roomPrefab, Vector3Int gridPosition, Quaternion rotation, int depth, int prefabConnToAttach = -1)
    //{
    //    GameObject roomInstanceGO = Instantiate(roomPrefab.gameObject, gridPosition, rotation, this.transform);
    //    RoomTemplate roomInstance = roomInstanceGO.GetComponent<RoomTemplate>();
    //    roomInstance.InitializeRuntimeConnections();

    //    BoundsInt roomGridBounds = CalculateRotatedBounds(gridPosition, roomPrefab, rotation);

    //    for (int x = roomGridBounds.xMin; x < roomGridBounds.xMax; x++)
    //    {
    //        for (int y = roomGridBounds.yMin; y < roomGridBounds.yMax; y++)
    //        {
    //            if (x >= 0 && x < gridSize.x && y >= 0 && y < gridSize.y)
    //            {
    //                _grid[x, y] = 1;
    //            }
    //        }
    //    }
    //    _placedRooms.Add(new PlacedRoomInstance
    //    {
    //        template = roomPrefab,
    //        gridPosition = new Vector2Int(Mathf.RoundToInt(gridPosition.x), Mathf.RoundToInt(gridPosition.z)),
    //        gridBounds = roomGridBounds,
    //        roomObject = roomInstanceGO,
    //        depth = depth
    //    });


    //    for (int i = 0; i < roomInstance.runtimeConnections.Count; i++)
    //    {
    //        var conn = roomInstance.runtimeConnections[i];
    //        if (i == prefabConnToAttach)
    //        {
    //            roomInstance.CloseConnectionFromSearch(conn.templateData, true);
    //            continue;
    //        }
            
    //        if (conn.IsOpenForSearch)
    //        {
    //            _frontier.Add(new FrontierConnection
    //            {
    //                WorldPosition = roomInstance.transform.TransformPoint(conn.templateData.GetLocalPosition()),
    //                WorldNormal = roomInstance.transform.TransformDirection(conn.templateData.GetNormal()),
    //                Type = conn.templateData.Type,
    //                OwningRoom = roomInstance,
    //                ConnectionData = conn.templateData,
    //                depth = depth
    //            });
    //        }
    //    }
    //    return roomInstance;
    //}

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





            int half = (corridorConfig.width - 1) / 2;
            int stepFromDoor = half;

            //int offset = Mathf.CeilToInt(corridorConfig.width / 2.0f);

            //Vector3Int startGridPos = Vector3Int.RoundToInt(startConnection.WorldPosition) + Vector3Int.RoundToInt(startConnection.WorldNormal * offset);
            //Vector3Int endGridPos = Vector3Int.RoundToInt(endConnection.WorldPosition) + Vector3Int.RoundToInt(endConnection.WorldNormal * offset);


            Vector2Int stepOutDoor = (NormalToGridDelta(startConnection.WorldNormal) * stepFromDoor);
            Vector2Int startGridPos = WorldToGrid(startConnection.WorldPosition) + stepOutDoor;

            Vector2Int stepOutDoorEnd = (NormalToGridDelta(endConnection.WorldNormal) * stepFromDoor);
            Vector2Int endGridPos = WorldToGrid(endConnection.WorldPosition) + stepOutDoorEnd;


            int directDistance = GetMDistance(startGridPos, endGridPos);
            int maxAllowedPathLength = Mathf.RoundToInt(directDistance * pathfindingLengthMultiplier);


            List<PathNode> path = FindPath(startGridPos, endGridPos, corridorConfig.width, maxAllowedPathLength);

            if (path != null) // valid path
            {
                Debug.Log($"<color=green>Found valid path of length {path.Count}</color>");
      

                foreach (var pathNode in path)
                {
                    int brushOffset = (corridorConfig.width - 1) / 2;

                    for (int x = -brushOffset; x <= brushOffset; x++)
                    {
                        for (int y = -brushOffset; y <= brushOffset; y++)
                        {
                            Vector2Int tile = pathNode.position + new Vector2Int(x, y);
                            if (tile.x >= 0 && tile.x < gridSize.x && tile.y >= 0 && tile.y < gridSize.y)
                            {
                                _grid[tile.x, tile.y] = gridFillWith; 
                            }
                        }
                    }
                }

                startConnection.OwningRoom.CloseConnectionFromSearch(startConnection.ConnectionData, true);
                endConnection.OwningRoom.CloseConnectionFromSearch(endConnection.ConnectionData, true);
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
                if (_grid[x, y] >= 2)
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
            //Debug.Log($"Potential corridor connectin -  Target corridor cell: {closestCorridorCell.Value} Attempting pathfinding");

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
                    int brushOffset = (corridorConfig.width - 1) / 2;

                    for (int x = -brushOffset; x <= brushOffset; x++)
                    {
                        for (int y = -brushOffset; y <= brushOffset; y++)
                        {
                            Vector2Int tile = pathNode.position + new Vector2Int(x, y);
                            if (tile.x >= 0 && tile.x < gridSize.x && tile.y >= 0 && tile.y < gridSize.y)
                            {
                                _grid[tile.x, tile.y] = gridFillWith; 
                            }
                        }
                    }
                }

                startConnection.OwningRoom.CloseConnectionFromSearch(startConnection.ConnectionData, true);
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

    private bool IsRegionClear(Vector2Int center, int width, Vector2Int? goal = null)
    {
        int brushOffset = (width - 1) / 2;

        for (int x = -brushOffset; x <= brushOffset; x++)
        {
            for (int y = -brushOffset; y <= brushOffset; y++)
            {
                Vector2Int checkPos = center + new Vector2Int(x, y);
                if (checkPos.x < 0 || checkPos.x >= gridSize.x || checkPos.y < 0 || checkPos.y >= gridSize.y) return false;

                if (goal.HasValue && checkPos == goal.Value) continue;

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

    private static Vector2Int WorldToGrid(Vector3 world)
    {
        return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.z));
    }
    private static Vector2Int NormalToGridDelta(Vector3 n)
    {
        // pick the dominant axis and return ±1 step on that axis
        if (Mathf.Abs(n.x) > Mathf.Abs(n.z))
            return new Vector2Int(n.x >= 0 ? 1 : -1, 0);
        else
            return new Vector2Int(0, n.z >= 0 ? 1 : -1);
    }

    private void BrickUpEntrances()
    {
        Debug.Log($"--- Bricking up {_deadEndConnections.Count} dead-end connections ---");

        foreach (var placedRoom in _placedRooms)
        {
            if (placedRoom.roomObject == null) continue;

            RoomTemplate roomInstance = placedRoom.roomObject.GetComponent<RoomTemplate>();
            if (roomInstance == null || roomInstance.runtimeConnections == null) continue;

            foreach (var connectionState in roomInstance.runtimeConnections)
            {
                if (!connectionState.IsConnected)
                {
                    connectionState.templateData.ReplaceConnectionModel(false, roomInstance.transform);
                }
            }
        }
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


public sealed class DeterministicRng
{
    private System.Random _rng;
    public DeterministicRng(int seed) => _rng = new System.Random(seed);

    public int NextInt(int minInclusive, int maxExclusive)
        => _rng.Next(minInclusive, maxExclusive); // [min, max)

    public float NextFloat01()
        => (float)_rng.NextDouble(); // [0,1)

    public T Choice<T>(IList<T> list)
        => list.Count == 0 ? default : list[_rng.Next(0, list.Count)];
}

public static class RngExtensions
{
    public static void Shuffle<T>(this IList<T> list, DeterministicRng rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.NextInt(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}