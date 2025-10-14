using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// We must use this directive to access Unity Editor functions like saving prefabs.
#if UNITY_EDITOR
using UnityEditor;
#endif

// This helper class defines the toggles for our door types.
// It's [System.Serializable] so it shows up in the Inspector.
[System.Serializable]
public class DoorTypeToggle
{
    public ConnectionType type;
    public bool isEnabled = true;
}

[System.Serializable]
public class DoorDefinition
{
    public ConnectionType type;
    [Min(1)] public int width = 1;
    public Material material;
}


public class RoomGenerator : MonoBehaviour
{
    [Header("Generation Settings")]
    [Tooltip("The folder to save generated prefabs, relative to the Assets folder.")]
    public string prefabSavePath = "Prefabs/Rooms";

    [Header("Room Dimensions")]
    public Vector2Int minRoomSize = new Vector2Int(5, 5);
    public Vector2Int maxRoomSize = new Vector2Int(20, 20);

    [Header("Door Configuration")]
    public int minDoors = 2;
    public int maxDoors = 5;
    public List<DoorTypeToggle> enabledDoorTypes = new List<DoorTypeToggle>();
    public List<DoorDefinition> doorDefinitions;

    [Header("Material Assignments")]
    public Material floorMaterial;
    public Material singleDoorMaterial;
    public Material doubleDoorMaterial;
    public Material archwayMaterial;
    public Material corridorMaterial;


    /// <summary>
    /// This is the main function. Right-click the component's header in the Inspector
    /// and select "Generate Random Room Prefab" to run this.
    /// </summary>
    [ContextMenu("Generate Random Room Prefab")]
    public void GeneratePrefab()
    {
#if UNITY_EDITOR
        

        RoomTemplate[] existingRooms = FindObjectsOfType<RoomTemplate>();
        if (existingRooms.Length > 0)
        {
            Debug.Log($"Clearing {existingRooms.Length} previously generated room(s) from the scene.");
            foreach (RoomTemplate room in existingRooms)
            {
                // In the editor, we must use DestroyImmediate() instead of Destroy().
                // This removes the object instantly.
                DestroyImmediate(room.gameObject);
            }
        }

        if (doorDefinitions == null || doorDefinitions.Count == 0)
        {
            Debug.LogError("Door Definitions list is empty! Please add at least one door type.", this);
            return;
        }
        if (doorDefinitions.Any(d => d.material == null))
        {
            Debug.LogError("One or more Door Definitions is missing a material!", this);
            return;
        }


        int roomWidth = Random.Range(minRoomSize.x, maxRoomSize.x + 1);
    int roomDepth = Random.Range(minRoomSize.y, maxRoomSize.y + 1);
    string roomName = $"Room_{roomWidth}x{roomDepth}_{System.DateTime.Now.Ticks}";

    GameObject roomGO = new GameObject(roomName);
    RoomTemplate roomTemplate = roomGO.AddComponent<RoomTemplate>();
    GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
    floor.name = "Floor";
    floor.transform.SetParent(roomGO.transform);
    floor.transform.localPosition = new Vector3(roomWidth / 2f, 0, roomDepth / 2f);
    floor.transform.localRotation = Quaternion.Euler(90, 0, 0);
    floor.transform.localScale = new Vector3(roomWidth, roomDepth, 1);
    floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

    // --- 3. DOOR PLACEMENT ---
     int numberOfDoors = Random.Range(minDoors, maxDoors + 1);
    const float doorHeight = 1f;
    const float doorThickness = 0.1f;
    
    List<int> availableSides = new List<int>();
    List<int>[] occupiedEdgeCells = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

    for (int i = 0; i < numberOfDoors; i++)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (availableSides.Count == 0)
            {
                availableSides.AddRange(new int[] { 0, 1, 2, 3 });
                availableSides.Shuffle();
            }
            int edge = availableSides[0];

            DoorDefinition doorDef = doorDefinitions[Random.Range(0, doorDefinitions.Count)];
            int doorWidth = doorDef.width;

            Vector3 connectionCenterPos = Vector3.zero;
            Quaternion doorRotation = Quaternion.identity;
            Vector3 doorNormal = Vector3.zero;

            if (edge == 0 || edge == 1) // Left (x=0) or Right (x=width)
            {
                // Ensure there's space for the door (at least 1 cell on each side for walls)
                if (doorWidth >= roomDepth - 1) { availableSides.RemoveAt(0); continue; }
                
                // Find a valid starting cell for the door
                int startCell = Random.Range(1, roomDepth - doorWidth);
                
                bool isOccupied = false;
                for(int w = 0; w < doorWidth; w++) { if(occupiedEdgeCells[edge].Contains(startCell + w)) isOccupied = true; }
                if(isOccupied) continue;

                for(int w = 0; w < doorWidth; w++) occupiedEdgeCells[edge].Add(startCell + w);
                
                // --- *** THE CRITICAL FIX *** ---
                // The center is the starting cell plus half the width.
                // Using 2.0f ensures floating-point division.
                float centerZ = startCell + (doorWidth / 2.0f);
                connectionCenterPos = new Vector3((edge == 0) ? 0 : roomWidth, 0, centerZ);
                
                doorRotation = Quaternion.Euler(0, (edge == 0) ? -90 : 90, 0);
                doorNormal = (edge == 0) ? Vector3.left : Vector3.right;
            }
            else // Bottom (z=0) or Top (z=depth)
            {
                if (doorWidth >= roomWidth - 1) { availableSides.RemoveAt(0); continue; }
                
                int startCell = Random.Range(1, roomWidth - doorWidth);

                bool isOccupied = false;
                for(int w = 0; w < doorWidth; w++) { if(occupiedEdgeCells[edge].Contains(startCell + w)) isOccupied = true; }
                if(isOccupied) continue;
                
                for(int w = 0; w < doorWidth; w++) occupiedEdgeCells[edge].Add(startCell + w);

                // --- *** THE CRITICAL FIX *** ---
                float centerX = startCell + (doorWidth / 2.0f);
                connectionCenterPos = new Vector3(centerX, 0, (edge == 2) ? 0 : roomDepth);

                doorRotation = Quaternion.Euler(0, (edge == 2) ? 180 : 0, 0);
                doorNormal = (edge == 2) ? Vector3.back : Vector3.forward;
            }
            
            availableSides.RemoveAt(0);

            // --- The rest of the logic is unchanged and now works correctly ---
            GameObject doorGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            doorGO.name = $"Door_{doorDef.type}_{i}";
            doorGO.transform.SetParent(roomGO.transform);
            doorGO.GetComponent<Renderer>().sharedMaterial = doorDef.material;
            doorGO.tag = "Connection";
            
            doorGO.transform.localPosition = connectionCenterPos - (doorNormal * (doorThickness / 2f));
            doorGO.transform.localRotation = doorRotation;
            doorGO.transform.localScale = new Vector3(doorWidth, doorHeight, doorThickness);

            roomTemplate.Connections.Add(new ConnectionPoint {
                LocalPosition = connectionCenterPos,
                Normal = doorNormal,
                Type = doorDef.type,
                width = doorWidth
            });
            break; 
        }
    }
    
    roomTemplate.CalculateBoundsFromFloor();

        // --- 3. PREFAB SAVING (No changes) ---
        // ... (saving code remains the same) ...
        string fullPath = $"Assets/{prefabSavePath}/{roomName}.prefab";
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
        PrefabUtility.SaveAsPrefabAssetAndConnect(roomGO, fullPath, InteractionMode.UserAction);
        Debug.Log($"Generated and saved new room prefab at: {fullPath}", roomGO);
        
#else
        Debug.LogWarning("Room generation is an Editor-only function and cannot be run in a build.");
#endif
    }
}



public static class ListExtensions
{
    private static System.Random rng = new System.Random();

    // This is an extension method for any List type.
    // It shuffles the list in-place using the Fisher-Yates algorithm.
    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}