using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class TerrainManager : MonoBehaviour
{
    public WorldGenerationProfile worldProfile;

    private Vector3Int boundsSize = new Vector3Int(100, 30, 100);
    private Vector3Int gridSize = new Vector3Int(100, 30, 100);


    [HideInInspector]
    public List<Vector3> roomPositions;
    private List<Connection> allConnections;


    private List<Connection> pathNetwork;

    [Header("Chunking")]
    public Vector3Int chunkSize = new Vector3Int(32, 32, 32);
    private Dictionary<Vector3Int, Chunk> chunks = new Dictionary<Vector3Int, Chunk>();



    private float[,,] sdfGrid;

    [Header("terrain variables")]
    public Material terrainMaterial;
    public bool smoothTerrain = false, customNormals = true;
    private GameObject terrainObject;

    

    [HideInInspector]
    private List<GeneratedRoom> roomInstances;

    [Header("SDF variables")]
    public float SDFCap = 10f;

    [Header("Debug gizmos")]
    public bool drawRoomAndConnectionGizmos = false;
    public bool drawChunkOutlines = true;
    public bool drawVoxelGridOnRaycast = true;
    public int voxelGridRadius = 1; 

    [HideInInspector]
    public float isoLevel = 0f;

    void Start()
    {
        boundsSize = worldProfile.boundsSize;
        gridSize = worldProfile.gridSize;
        chunkSize = new Vector3Int(chunkSize.x, boundsSize.y, chunkSize.z);
    }

    void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartCoroutine(GenerateWorldCoroutine());
        }
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            drawChunkOutlines = !drawChunkOutlines;
        }

        if (Keyboard.current.vKey.wasPressedThisFrame)
        {
            drawVoxelGridOnRaycast = !drawVoxelGridOnRaycast;
        }
        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            drawRoomAndConnectionGizmos = !drawRoomAndConnectionGizmos;
        }
    }

  

    private IEnumerator GenerateWorldCoroutine()
    {
        boundsSize = worldProfile.boundsSize;
        gridSize = worldProfile.gridSize;
        chunkSize = new Vector3Int(chunkSize.x, chunkSize.y, chunkSize.z);
        Debug.Log("Starting world gen");

        foreach (var chunk in chunks.Values) { if (chunk.gameObject != null) Destroy(chunk.gameObject); }
        chunks.Clear();


        GeneratePoints();
        if (roomInstances == null || roomInstances.Count < 3)
        {
            Debug.LogError("Failed to generate points, youve probably set the roomRadius too large for the bounds/ minDistance");
            yield break;
        }
        TriangulateRooms();
        BuildPathNetwork();

        Vector3Int numChunks = new Vector3Int(
            Mathf.CeilToInt((float)gridSize.x / chunkSize.x),
            Mathf.CeilToInt((float)gridSize.y / chunkSize.y),
            Mathf.CeilToInt((float)gridSize.z / chunkSize.z)
        );


        for (int x = 0; x < numChunks.x; x++) { 
            for (int y = 0; y < numChunks.y; y++) { 
                for (int z = 0; z < numChunks.z; z++)
                {
                    Vector3Int chunkCoord = new Vector3Int(x, y, z);
                    Chunk newChunk = new Chunk(chunkCoord, chunkSize, terrainMaterial, this.transform, SDFCap, worldProfile.defaultMaterial);
                    chunks.Add(chunkCoord, newChunk);
                }
            }
        }

        EnvironmentGenerator.CalculateMacroEnvironment(chunks, worldProfile, chunkSize, gridSize, boundsSize);

        foreach (var kv in chunks)
        {
            SDFGenerator.GenerateChunk(kv.Value, chunkSize, roomInstances, pathNetwork, boundsSize, gridSize, worldProfile.tunnelRadius);
            yield return null;
        }

        EnvironmentGenerator.CalculateMicroEnvironment(chunks, chunkSize);
        

        EnvironmentGenerator.SelectBiomes(chunks, worldProfile);

        foreach (var kv in chunks)
        {
            var ch = kv.Value;

            MaterialFields.ChunkMaterialVolumeBuilder.BuildAndAssignMaterialVolume(ch);
            
            Mesh mesh = new Mesh();
            MarchingCubes.CreateMesh(ch.grid, mesh, isoLevel, smoothTerrain, customNormals);
            //MarchingTetrahedra.CreateMesh(ch.grid, mesh, isoLevel);
            ch.meshFilter.mesh = mesh;
            ch.meshCollider.sharedMesh = mesh;
            yield return null;
        }

        Debug.Log("World generation complete!");
        yield return new WaitForFixedUpdate();
        //GameController.Instance.mainCameraController.characterMovementController.gameObject.transform.position = roomInstances[0].pos + Vector3.up * 4;
    }


    private void OnDrawGizmos()
    {
        Vector3 worldCenter = transform.position + (Vector3)boundsSize / 2f;
        Gizmos.color = Color.cyan;



        if (drawRoomAndConnectionGizmos)
        {
            Gizmos.DrawWireCube(worldCenter, new Vector3(boundsSize.x, boundsSize.y, boundsSize.z));
            if (roomPositions != null)
            {
                Gizmos.color = Color.red;
                foreach (Vector3 pos in roomPositions)
                {
                    Gizmos.DrawWireSphere(transform.position + pos, worldProfile.roomRadius);
                }
            }
            if (pathNetwork != null)
            {
                Gizmos.color = Color.green; 
                foreach (var connection in pathNetwork)
                {
                    Gizmos.DrawLine(transform.position + connection.p1, transform.position + connection.p2);
                }
            }
            else if (allConnections != null) 
            {
                Gizmos.color = Color.white;
                foreach (var connection in allConnections)
                {
                    Gizmos.DrawLine(transform.position + connection.p1, transform.position + connection.p2);
                }
            }
        }
        bool didRaycastHitChunk = false;
        if (Application.isPlaying && (drawVoxelGridOnRaycast || drawChunkOutlines) && chunks != null && chunks.Count > 0 && Camera.main != null)
        {
            Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                Chunk hitChunk = null;
                foreach (var chunk in chunks.Values)
                {
                    if (chunk.gameObject == hit.collider.gameObject)
                    {
                        hitChunk = chunk;
                        break;
                    }
                }

                if (hitChunk != null)
                {
                    didRaycastHitChunk = true;

                    if (drawChunkOutlines)
                    {
                        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.1f); 
                        Vector3 chunkWorldSize = new Vector3(chunkSize.x, chunkSize.y, chunkSize.z);
                        Vector3 chunkCenter = hitChunk.gameObject.transform.position + (chunkWorldSize / 2f);
                        Gizmos.DrawCube(chunkCenter, chunkWorldSize); 
                    }

                    if (drawVoxelGridOnRaycast)
                    {
                        Vector3 voxelScale = new Vector3(
                            (float)boundsSize.x / gridSize.x,
                            (float)boundsSize.y / gridSize.y,
                            (float)boundsSize.z / gridSize.z
                        );
                        Vector3 chunkOrigin = hitChunk.gameObject.transform.position;
                        Vector3 localHitPoint = hit.point - chunkOrigin;
                        Vector3Int hitVoxelCoord = new Vector3Int(
                            Mathf.FloorToInt(localHitPoint.x / voxelScale.x),
                            Mathf.FloorToInt(localHitPoint.y / voxelScale.y),
                            Mathf.FloorToInt(localHitPoint.z / voxelScale.z)
                        );

                        for (int x = -voxelGridRadius; x <= voxelGridRadius; x++)
                        {
                            for (int y = -voxelGridRadius; y <= voxelGridRadius; y++)
                            {
                                for (int z = -voxelGridRadius; z <= voxelGridRadius; z++)
                                {
                                    Vector3Int currentVoxelCoord = hitVoxelCoord + new Vector3Int(x, y, z);
                                    if (currentVoxelCoord.x >= 0 && currentVoxelCoord.x < hitChunk.grid.GetLength(0) &&
                                        currentVoxelCoord.y >= 0 && currentVoxelCoord.y < hitChunk.grid.GetLength(1) &&
                                        currentVoxelCoord.z >= 0 && currentVoxelCoord.z < hitChunk.grid.GetLength(2))
                                    {
                                        float density = hitChunk.grid[currentVoxelCoord.x, currentVoxelCoord.y, currentVoxelCoord.z].density;

                                        Gizmos.color = (density > isoLevel) ? new Color(0, 1, 0, 0.15f) : new Color(1, 0, 0, 0.05f);

                                        Vector3 voxelCenter = chunkOrigin + new Vector3(
                                            currentVoxelCoord.x * voxelScale.x + voxelScale.x * 0.5f,
                                            currentVoxelCoord.y * voxelScale.y + voxelScale.y * 0.5f,
                                            currentVoxelCoord.z * voxelScale.z + voxelScale.z * 0.5f
                                        );
                                        Gizmos.DrawCube(voxelCenter, voxelScale * 0.9f);
                                    }
                                }
                            }
                        }
                        Gizmos.color = Color.magenta;
                        Gizmos.DrawSphere(hit.point, voxelScale.magnitude * 0.1f);
                    }
                }
            }
        }

        if (drawChunkOutlines && !didRaycastHitChunk && chunks != null && chunks.Count > 0)
        {
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.05f);
            Vector3 chunkWorldSize = new Vector3(chunkSize.x, chunkSize.y, chunkSize.z);
            foreach (var chunk in chunks.Values)
            {
                Vector3 chunkCenter = chunk.gameObject.transform.position + (chunkWorldSize / 2f);
                Gizmos.DrawCube(chunkCenter, chunkWorldSize); 
            }
        }
    }

    int recursaionCheck = 0;
    //uses poisson disc sampling - example https://www.youtube.com/watch?v=7WcmyxyFO7o
    public void GeneratePoints()
    {
        Vector3 startRoomPosition = new Vector3(boundsSize.x / 2f, worldProfile.startRoom.maxRadius, worldProfile.startRoom.maxRadius
    );

        roomPositions = RandomDistributionPointSampler.GeneratePoints(worldProfile.roomRadius, worldProfile.minRoomDistance, boundsSize, worldProfile.rejectionSamples, startRoomPosition);

        if (roomPositions.Count < 3 && recursaionCheck <= 10)
        {
            recursaionCheck++;
            GeneratePoints();
        }
        else if (recursaionCheck > 10)
        {
                Debug.Log("Got stuck making rooms, reccursion >10");
        }
        else
        {
            Debug.Log($"Generated {roomPositions.Count} room positions.");
        }


        roomInstances = new List<GeneratedRoom>();
        foreach (var pos in roomPositions)
        {
            RoomGenerationProfile profileToUse;
            if (pos == startRoomPosition)
            {
                profileToUse = worldProfile.startRoom;
            }
            else
            {
                profileToUse = worldProfile.defaultRoom;
            }

            roomInstances.Add(new GeneratedRoom(pos, worldProfile.defaultRoom.maxRadius, worldProfile.defaultRoom, SDFCap));
        }

    }

    //Uses delunary triangulation to connect points (in a 2d plane) 
    public void TriangulateRooms()
    {
        if (roomPositions == null || roomPositions.Count < 3) return;

        Dictionary<Vector2, Vector3> pointMap = new Dictionary<Vector2, Vector3>();
        List<Vector2> points2D = new List<Vector2>();

        foreach (var pos3D in roomPositions)
        {
            var pos2D = new Vector2(pos3D.x, pos3D.z);
            points2D.Add(pos2D);
            if (!pointMap.ContainsKey(pos2D))
            {
                pointMap.Add(pos2D, pos3D);
            }
        }

        List<Triangle> triangles = DelaunayTriangulator.Triangulate(points2D);

        allConnections = new List<Connection>();
        foreach (var tri in triangles)
        {
            Vector3 p1 = pointMap[tri.p1];
            Vector3 p2 = pointMap[tri.p2];
            Vector3 p3 = pointMap[tri.p3];

            allConnections.Add(new Connection(p1, p2));
            allConnections.Add(new Connection(p2, p3));
            allConnections.Add(new Connection(p3, p1));
        }
        Debug.Log($"Generated {allConnections.Count} possible connections.");
    }

    //uses MST to find the path and adds back in some extra connections
    public void BuildPathNetwork()
    {
        if (roomPositions == null || roomPositions.Count == 0) return;

        pathNetwork = new List<Connection>();
        var visitedRooms = new HashSet<Vector3>();

        visitedRooms.Add(roomPositions[0]);

        while (visitedRooms.Count < roomPositions.Count)
        {
            Connection bestConnection = new Connection();
            float shortestDistance = float.MaxValue;

            foreach (var connection in allConnections)
            {
                bool p1Visited = visitedRooms.Contains(connection.p1);
                bool p2Visited = visitedRooms.Contains(connection.p2);

                if (p1Visited && !p2Visited || !p1Visited && p2Visited)
                {
                    if (connection.distance < shortestDistance)
                    {
                        shortestDistance = connection.distance;
                        bestConnection = connection;
                    }
                }
            }

            pathNetwork.Add(bestConnection);

            if (!visitedRooms.Contains(bestConnection.p1))
                visitedRooms.Add(bestConnection.p1);
            else
                visitedRooms.Add(bestConnection.p2);
        }

        var leftoverConnections = allConnections.Except(pathNetwork).OrderBy(c => c.distance).ToList();
        int extraConnectionsToAdd = Mathf.FloorToInt(leftoverConnections.Count * worldProfile.extraConnectionPercentage);

        for (int i = 0; i < extraConnectionsToAdd; i++)
        {
            pathNetwork.Add(leftoverConnections[i]);
        }

        Debug.Log($"Path network built with {pathNetwork.Count} connections.");
    }

}

public struct Connection
{
    public Vector3 p1, p2;
    public float distance;

    public Connection(Vector3 point1, Vector3 point2)
    {
        p1 = point1;
        p2 = point2;
        distance = Vector3.Distance(p1, p2);
    }
}

public class Chunk
{
    public GameObject gameObject;
    public MeshFilter meshFilter;
    public MeshRenderer meshRenderer;
    public MeshCollider meshCollider;
    public VoxelData[,,] grid;

    public Vector3Int coord;

    public Texture3D materialVolume;
    public Vector3 worldMin;
    public Vector3 worldSize;

    public float sdfCap;
    public Texture3D matIDs3D, density3D;

    public Chunk(Vector3Int coord, Vector3Int chunkSize, Material material, Transform parent, float initialDensity, VoxelMat defaultMat)
    {
        this.coord = coord;

        this.sdfCap = initialDensity;

        gameObject = new GameObject($"Chunk {coord.x}, {coord.y}, {coord.z}");
        gameObject.transform.SetParent(parent);

        meshFilter = gameObject.AddComponent<MeshFilter>();
        meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshCollider = gameObject.AddComponent<MeshCollider>();
        meshRenderer.material = material;

        int gridX = chunkSize.x + 1;
        int gridY = chunkSize.y + 1;
        int gridZ = chunkSize.z + 1;
        grid = new VoxelData[gridX, gridY, gridZ];

        gameObject.transform.position = new Vector3(coord.x * chunkSize.x, coord.y * chunkSize.y, coord.z * chunkSize.z);


        for (int i = 0; i < gridX; i++) {
            for (int j = 0; j < gridY; j++)
            {
                for (int k = 0; k < gridZ; k++)
                {
                    grid[i, j, k] = new VoxelData
                    {
                        density = initialDensity,
                        matId0 = (byte)defaultMat
                    };
                    

                }
            }
        }
    }
}



public struct VoxelData
{
    public float density;

    public byte humidity;
    public byte temperature;
    public byte slope;
    public byte verticality; // 0=floor 128=wall 255=ceiling

    public byte biomeID;


    public byte matId0, matId1, matId2, matId3; // 0-254 valid, 255=none/air
    public float matM0, matM1, matM2, matM3;  

    public static VoxelData Solid => new VoxelData
    {
        density = +float.MaxValue,

        matId0 = 255,
        matId1 = 255,
        matId2 = 255,
        matId3 = 255,
        matM0 = float.NegativeInfinity,
        matM1 = float.NegativeInfinity,
        matM2 = float.NegativeInfinity,
        matM3 = float.NegativeInfinity,
    };
    public static VoxelData Empty => new VoxelData { density = -float.MaxValue };
}

public enum VoxelMat
{
    Air = 255,
    Stone = 0, 
    Dirt = 1, 
    Moss = 2,
    Crystal = 3,
}
