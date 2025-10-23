
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class RoomTemplate : MonoBehaviour
{
    public Bounds RoomBounds;
    public List<ConnectionPoint> Connections = new List<ConnectionPoint>();
    public List<RuntimeConnectionState> runtimeConnections;
    public Transform spawnPointInRoom; //not all rooms need this assigning - probably just the start room


    [Header("Footprint Data")]
    public float footprintCellSize = 1.0f;
    public LayerMask footprintLayerMask;
    [HideInInspector] public FootprintRow[] FootprintGrid;

    public int FootprintWidth
    {
        get
        {
            if (FootprintGrid == null || FootprintGrid.Length == 0 || FootprintGrid[0].cells == null)
                return 0;
            return FootprintGrid[0].cells.Length;
        }
    }
    public int FootprintHeight
    {
        get
        {
            if (FootprintGrid == null)
                return 0;
            return FootprintGrid.Length;
        }
    }


    private void OnDrawGizmos()
    {

        Gizmos.color = new Color(0, 1, 0, 0.3f); 
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one); 
        Gizmos.matrix = rotationMatrix;

        Gizmos.DrawCube(RoomBounds.center, RoomBounds.size);
        Gizmos.matrix = Matrix4x4.identity;

        if (FootprintGrid != null && FootprintGrid.Length > 0) 
        {
            Gizmos.matrix = transform.localToWorldMatrix; 
            Vector3 cellExtents = new Vector3(footprintCellSize * 0.9f, 0.1f, footprintCellSize * 0.9f);

            // Use new properties for loop bounds
            for (int z = 0; z < FootprintHeight; z++)
            {
                for (int x = 0; x < FootprintWidth; x++)
                {
                    if (GetFootprintAt(x, z)) 
                    {
                        Gizmos.color = new Color(1, 0, 1, 0.4f); 
                        Vector3 cellCenter = new Vector3(
                            (x + 0.5f) * footprintCellSize,
                            0,
                            (z + 0.5f) * footprintCellSize
                        );
                        Gizmos.DrawCube(cellCenter, cellExtents);
                    }
                }
            }
            Gizmos.matrix = Matrix4x4.identity; 
        }

        if (runtimeConnections == null || runtimeConnections.Count == 0)
        {
            foreach (var connection in Connections)
            {
                DrawConnectionGizmo(connection, Color.white);
            }
        }
        else
        {
            foreach (var connectionState in runtimeConnections)
            {
                Color gizmoColor = connectionState.IsOpenForSearch ? GetColorForType(connectionState.templateData.Type) : (connectionState.IsConnected ? Color.green : Color.red);
                DrawConnectionGizmo(connectionState.templateData, gizmoColor);
            }
        }
    }

    private void DrawConnectionGizmo(ConnectionPoint connection, Color color)
    {
        Vector3 worldPos = transform.TransformPoint(connection.GetLocalPosition());
        Vector3 worldDir = transform.TransformDirection(connection.GetNormal());
        Gizmos.color = color;
        Gizmos.DrawSphere(worldPos, 0.3f);
        Gizmos.DrawLine(worldPos, worldPos + worldDir * 1.5f);
    }

    public void InitializeRuntimeConnections()
    {
        runtimeConnections = new List<RuntimeConnectionState>();
        foreach (var connectionTemplate in Connections)
        {
            runtimeConnections.Add(new RuntimeConnectionState { templateData = connectionTemplate });
        }
    }

    public void CloseConnectionFromSearch(ConnectionPoint connectionToClose, bool sucsessfullConnection)
    {
        var connectionState = runtimeConnections.FirstOrDefault(rc => rc.templateData == connectionToClose);
        if (connectionState != null)
        {
            connectionState.IsOpenForSearch = false;

            connectionState.IsConnected = sucsessfullConnection;
        }
    }








    public bool GetFootprintAt(int x, int z)
    {
        if (FootprintGrid == null || FootprintGrid.Length == 0) return false;

        if (z < 0 || z >= FootprintGrid.Length) return false;

        FootprintRow row = FootprintGrid[z];
        if (row == null || row.cells == null || row.cells.Length == 0) return false;

        if (x < 0 || x >= row.cells.Length) return false;

        return row.cells[x];
    }


    [ContextMenu("Bake Room Data (Bounds + Footprint)")]
    public void BakeRoomData()
    {
        Vector3 originalPosition = transform.position;
        Quaternion originalRotation = transform.rotation;
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;

        Collider[] childColliders = GetComponentsInChildren<Collider>();
        if (childColliders.Length == 0)
        {
            Debug.LogError($"Could not bake data for {name}: No colliders found. Add colliders to your room geometry.", this);
            transform.position = originalPosition;
            transform.rotation = originalRotation;
            return;
        }

        Bounds combinedBounds = childColliders[0].bounds;
        for (int i = 1; i < childColliders.Length; i++)
        {
            combinedBounds.Encapsulate(childColliders[i].bounds);
        }

        if (footprintLayerMask.value == 0)
        {
            Debug.LogError($"Footprint Layer Mask is not set for {name}. Please set this in the prefab inspector. Baking cancelled.", this);
            transform.position = originalPosition;
            transform.rotation = originalRotation;
            return;
        }

        Vector3 rawSize = combinedBounds.size;

        Vector2Int footprintGridSize = new Vector2Int(
            Mathf.RoundToInt(rawSize.x / footprintCellSize), 
            Mathf.RoundToInt(rawSize.z / footprintCellSize)  
        );



        if (footprintGridSize.x == 0 || footprintGridSize.y == 0)
        {
            Debug.LogError($"Could not bake data for {name}: Calculated footprint size is zero. Check colliders and scale.", this);
            transform.position = originalPosition;
            transform.rotation = originalRotation;
            return;
        }

        FootprintGrid = new FootprintRow[footprintGridSize.y]; 
        for (int z = 0; z < footprintGridSize.y; z++)
        {
            FootprintGrid[z] = new FootprintRow(footprintGridSize.x); 
        }

        float checkHeight = rawSize.y > 0 ? rawSize.y : 50f; 
        float checkYCenter = combinedBounds.min.y + (checkHeight / 2f);
        Vector3 boxHalfExtents = new Vector3(footprintCellSize * 0.45f, checkHeight * 0.5f, footprintCellSize * 0.45f);

        for (int z = 0; z < footprintGridSize.y; z++)
        {
            for (int x = 0; x < footprintGridSize.x; x++)
            {

                Vector3 cellCenter = new Vector3(
                    (x + 0.5f) * footprintCellSize,
                    checkYCenter, 
                    (z + 0.5f) * footprintCellSize
                );

                // Check for colliders at this cell
                if (Physics.OverlapBox(cellCenter, boxHalfExtents, Quaternion.identity, footprintLayerMask, QueryTriggerInteraction.Ignore).Length > 0)
                {
                    FootprintGrid[z].cells[x] = true; 
                }
                else
                {
                    FootprintGrid[z].cells[x] = false; 
                }
            }
        }


        transform.position = originalPosition;
        transform.rotation = originalRotation;


        Vector3 finalSize = new Vector3(footprintGridSize.x * footprintCellSize, 0, footprintGridSize.y * footprintCellSize);
        Vector3 finalCenter = new Vector3(finalSize.x / 2f, 0, finalSize.z / 2f);
        RoomBounds = new Bounds(finalCenter, finalSize);

        Debug.Log($"<color=green>Data for {name} successfully baked. Bounds: {RoomBounds.size}, Footprint: {footprintGridSize.x}x{footprintGridSize.y}</color>", this);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }



    [ContextMenu("Calculate Bounds From Floor")]

    public void CalculateBoundsFromFloor()
    {
        Transform floorTransform = transform.Find("Floor");
        Vector3 rawSize;

        if (floorTransform != null)
        {
            Debug.Log($"Found floor for '{name} - bounds from its scale.");
            rawSize = new Vector3(floorTransform.localScale.x, 0, floorTransform.localScale.y);
        }
        else
        {
            Vector3 originalPosition = transform.position;
            Quaternion originalRotation = transform.rotation;
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;

            MeshRenderer[] childRenderers = GetComponentsInChildren<MeshRenderer>();
            if (childRenderers.Length == 0)
            {
                Debug.LogError($"Could not calculate bounds for {name} no child meshes found", this);
                transform.position = originalPosition;
                transform.rotation = originalRotation;
                return;
            }

            Bounds combinedBounds = childRenderers[0].bounds;
            for (int i = 1; i < childRenderers.Length; i++)
            {
                combinedBounds.Encapsulate(childRenderers[i].bounds);
            }

            transform.position = originalPosition;
            transform.rotation = originalRotation;

            rawSize = combinedBounds.size;
        }

        Vector3Int intSize = new Vector3Int(Mathf.RoundToInt(rawSize.x),0, Mathf.RoundToInt(rawSize.z));

        Vector3 center = new Vector3(intSize.x / 2f, 0, intSize.z / 2f);

        RoomBounds = new Bounds(center, intSize);

        Debug.Log($"<color=green>bounds for {name} successfully calculated new size = {RoomBounds.size}</color>", this);
    }


    public float GetBoundingRadius()
    {
        return RoomBounds.extents.magnitude;
    }


    [ContextMenu("Find Connections from Children")]
    public void FindConnections()
    {
        Connections.Clear();
        foreach (Transform child in transform)
        {
            if (child.CompareTag("Connection")) 
            {
                Connections.Add(new ConnectionPoint
                {
                    LocalPosition = child.localPosition,
                    Normal = child.localRotation * Vector3.forward, 
                    Type = ConnectionType.Corridor 
                });
            }
        }
    }

    public Color GetColorForType(ConnectionType type)
    {
        switch (type)
        {
            case ConnectionType.SingleDoor: return Color.blue;
            case ConnectionType.DoubleDoor: return Color.yellow;
            case ConnectionType.Archway: return Color.magenta;
            case ConnectionType.Corridor: return Color.cyan;
            default: return Color.white;
        }
    }
}


[System.Serializable]
public class ConnectionPoint
{
    public GameObject connectionObject;
    public Vector3 LocalPosition;
    public Vector3 Normal; 
    public ConnectionType Type;
    public GameObject closedConnectionPrefab;
    public int width = 1;
    public bool offsetByWidth = false;

    public Vector3 GetLocalPosition()
    {
        if (connectionObject != null)
        {
            if (offsetByWidth)
            {
                return connectionObject.transform.localPosition + (connectionObject.transform.localRotation * Vector3.up * ((width/2f) ));
            }
            return connectionObject.transform.localPosition ;
        }
        return LocalPosition;
    }

    public Vector3 GetNormal()
    {
        if (connectionObject != null)
        {
            if (offsetByWidth)
            {
                return connectionObject.transform.localRotation * Vector3.up;
            }
            return connectionObject.transform.localRotation * Vector3.forward;
        }
        return Normal;
    }

    public void ReplaceConnectionModel(bool open, Transform roomTransfrom)
    {
        if (!open)
        {
            GameObject closedConnection = GameObject.Instantiate(closedConnectionPrefab);

            closedConnection.transform.parent = roomTransfrom;

            closedConnection.transform.localPosition = connectionObject != null ? connectionObject.transform.localPosition : LocalPosition;

            closedConnection.transform.localRotation = connectionObject != null ? connectionObject.transform.localRotation : Quaternion.LookRotation(GetNormal(), Vector3.up);
        
            if(connectionObject != null)
            {
                GameObject.Destroy(connectionObject);
            }

            connectionObject = closedConnection;
        }
    }
}

public enum ConnectionType
{
    SingleDoor,
    DoubleDoor,
    TrippleDoor,
    Archway,
    Corridor
}

public class RuntimeConnectionState
{
    public ConnectionPoint templateData; 
    public bool IsOpenForSearch = true; //This means active in the list - ie not a write off 
    public bool IsConnected = false; //this means if its connected to a door or corridor
}

[System.Serializable]
public class FootprintRow //this has to be a jagged for unity to save a 2d array (need to use jagged array)
{
    public bool[] cells;

    public FootprintRow(int width)
    {
        cells = new bool[width];
    }
}