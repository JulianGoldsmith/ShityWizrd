
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class RoomTemplate : MonoBehaviour
{
    public Bounds RoomBounds;
    public List<ConnectionPoint> Connections = new List<ConnectionPoint>();
    public List<RuntimeConnectionState> runtimeConnections;
    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f); // Blue, semi-transparent
        Matrix4x4 rotationMatrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.matrix = rotationMatrix;

        Gizmos.matrix = Matrix4x4.identity;

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
                Color gizmoColor = connectionState.IsOpen ? GetColorForType(connectionState.templateData.Type) : Color.red;
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

    public void CloseConnection(ConnectionPoint connectionToClose)
    {
        var connectionState = runtimeConnections.FirstOrDefault(rc => rc.templateData == connectionToClose);
        if (connectionState != null)
        {
            connectionState.IsOpen = false;
        }
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
            case ConnectionType.SingleDoor: return Color.green;
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
    public int width = 1;

    public Vector3 GetLocalPosition()
    {
        if (connectionObject != null)
        {
            return connectionObject.transform.localPosition;
        }
        return LocalPosition;
    }

    public Vector3 GetNormal()
    {
        if (connectionObject != null)
        {
            return connectionObject.transform.localRotation * Vector3.forward;
        }
        return Normal;
    }
}

public enum ConnectionType
{
    SingleDoor,
    DoubleDoor,
    Archway,
    Corridor
}

public class RuntimeConnectionState
{
    public ConnectionPoint templateData; 
    public bool IsOpen = true;
}