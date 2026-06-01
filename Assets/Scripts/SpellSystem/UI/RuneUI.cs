using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;

public class RuneUI : MonoBehaviour
{
    // The core of the new architecture: The UI just knows its Index!
    public byte NodeIndex { get; private set; }
    public SpellNode ReadOnlyTemplate { get; private set; }

    private List<SocketUI> _sockets = new List<SocketUI>();
    public List<SocketUI> Sockets => _sockets;
    public void Initialize(byte nodeIndex, SpellNode template)
    {
        this.NodeIndex = nodeIndex;
        this.ReadOnlyTemplate = template;

        var renderer = GetComponent<Renderer>();
        if (renderer != null && ReadOnlyTemplate.icon != null)
        {
            renderer.material.SetTexture("_GlyphTex", ReadOnlyTemplate.icon);
        }
        CreateSockets();
    }

    private void CreateSockets()
    {
        if (ReadOnlyTemplate == null) return;
        var controller = SpellGraphController.Instance;

        // We read the socket definitions directly from the global Template
        List<SocketDefinition> socketsToCreate = ReadOnlyTemplate.GetSockets();
        if (socketsToCreate.Count == 0) return;

        float parentScale = this.transform.localScale.x * 1 / controller.graphVisualScale;

        foreach (var socketData in socketsToCreate)
        {
            GameObject socketObj = Instantiate(controller.socketPrefab, this.transform);

            if (parentScale != 0)
            {
                socketObj.transform.localScale *= 1 / parentScale;
            }

            SocketUI socketUI = socketObj.AddComponent<SocketUI>();
            socketUI.Initialize(this, socketData);
            _sockets.Add(socketUI);
        }
    }

    void LateUpdate()
    {
        if (_sockets.Count == 0) return;

        var controller = SpellGraphController.Instance;
        var startSocket = controller._connectionStartSocket;

        if (startSocket != null && startSocket.ParentRune == this)
        {
            LayoutForStartingConnection(startSocket);
        }
        else if (controller.IsRuneValidTarget(this))
        {
            LayoutForBeingTargeted(startSocket);
        }
        else
        {
            LayoutForIdle();
        }
    }

    private void LayoutForIdle()
    {
        var lockedSockets = FindExistingLockedSockets();
        DistributeSockets(lockedSockets);
        UpdateAttractionLogic(_sockets);
    }

    private void LayoutForStartingConnection(SocketUI startSocket)
    {
        var lockedSockets = FindExistingLockedSockets();
        var controller = SpellGraphController.Instance;

        Vector3 mouseWorldPos = GetMouseWorldPosition();
        Vector3 dirToMouse = (mouseWorldPos - transform.position).normalized;

        Vector3 dir_in_place = Quaternion.Inverse(controller.transform.rotation) * dirToMouse;
        lockedSockets[startSocket] = Mathf.Atan2(dir_in_place.z, dir_in_place.x) * Mathf.Rad2Deg;

        DistributeSockets(lockedSockets);
        UpdateAttractionLogic(_sockets);
    }

    private void LayoutForBeingTargeted(SocketUI startSocket)
    {
        var lockedSockets = FindExistingLockedSockets();
        var validTargets = _sockets.Where(s => SpellGraphController.Instance.IsConnectionValid(startSocket, s)).ToList();

        Vector3 dirToNew = (startSocket.ParentRune.transform.position - transform.position).normalized;
        Vector3 dir_in_plane = Quaternion.Inverse(SpellGraphController.Instance.transform.rotation) * dirToNew;
        float incomingAngle = Mathf.Atan2(dir_in_plane.z, dir_in_plane.x) * Mathf.Rad2Deg;

        foreach (var targetSocket in validTargets)
        {
            if (lockedSockets.TryGetValue(targetSocket, out float originalAngle))
            {
                lockedSockets[targetSocket] = Mathf.LerpAngle(originalAngle, incomingAngle, 0.5f);
            }
            else
            {
                lockedSockets[targetSocket] = incomingAngle;
            }
        }

        DistributeSockets(lockedSockets);
        UpdateAttractionLogic(validTargets);
    }

    private void DistributeSockets(Dictionary<SocketUI, float> lockedSockets)
    {
        var freeSockets = _sockets.Where(s => !lockedSockets.ContainsKey(s)).ToList();

        if (lockedSockets.Count == 0)
        {
            float angleIncrement = 360f / _sockets.Count;
            for (int i = 0; i < _sockets.Count; i++)
            {
                _sockets[i].SetTargetLocalPosition(CalculatePositionFromAngle(angleIncrement * i));
            }
        }
        else
        {
            var sortedLockedAngles = lockedSockets.Values.OrderBy(a => a).ToList();
            float maxGap = 0, gapStartAngle = 0;

            for (int i = 0; i < sortedLockedAngles.Count; i++)
            {
                float nextAngle = (i == sortedLockedAngles.Count - 1) ? sortedLockedAngles[0] + 360 : sortedLockedAngles[i + 1];
                float gap = nextAngle - sortedLockedAngles[i];
                if (gap > maxGap) { maxGap = gap; gapStartAngle = sortedLockedAngles[i]; }
            }

            if (freeSockets.Any())
            {
                float angleIncrement = maxGap / (freeSockets.Count + 1);
                for (int i = 0; i < freeSockets.Count; i++)
                {
                    freeSockets[i].SetTargetLocalPosition(CalculatePositionFromAngle(gapStartAngle + angleIncrement * (i + 1)));
                }
            }

            foreach (var pair in lockedSockets)
            {
                pair.Key.SetTargetLocalPosition(CalculatePositionFromAngle(pair.Value));
            }
        }
    }

    private void UpdateAttractionLogic(List<SocketUI> candidateSockets)
    {
        if (candidateSockets == null || candidateSockets.Count == 0)
        {
            foreach (var s in _sockets) s.SetMouseAttraction(false);
            return;
        }

        Vector3 mouseLocalPos = transform.InverseTransformPoint(GetMouseWorldPosition());

        SocketUI closestCandidate = null;
        float minDistanceSq = float.MaxValue;

        foreach (var socket in candidateSockets)
        {
            float distSq = (socket.transform.localPosition - mouseLocalPos).sqrMagnitude;
            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                closestCandidate = socket;
            }
        }

        foreach (var socket in _sockets)
        {
            socket.SetMouseAttraction(socket == closestCandidate);
        }
    }

    // TEMP FIX: We return empty here just to let Unity compile. 
    // We will hook the wire bending back up to the Array in the Controller phase!
    private Dictionary<SocketUI, float> FindExistingLockedSockets()
    {
        return new Dictionary<SocketUI, float>();
    }

    private Vector3 GetMouseWorldPosition()
    {
        var controller = SpellGraphController.Instance;
        Ray ray = controller.editorCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(controller.editorCamera.transform.forward, transform.position);
        if (plane.Raycast(ray, out float enter)) { return ray.GetPoint(enter); }
        return transform.position;
    }

    private Vector3 CalculatePositionFromAngle(float angle)
    {
        var controller = SpellGraphController.Instance;
        float angleRad = angle * Mathf.Deg2Rad;
        Vector3 worldOffset = controller.socketOrbitRadius * (Vector3.right * Mathf.Cos(angleRad) + Vector3.forward * Mathf.Sin(angleRad));
        return worldOffset;
    }

    public SocketUI FindSocketByName(string name) => _sockets.FirstOrDefault(s => s.SocketData.Name == name || s.SocketData.TargetFieldName == name);
}