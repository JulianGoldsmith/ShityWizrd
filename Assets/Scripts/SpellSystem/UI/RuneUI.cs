using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.InputSystem;
public class RuneUI : MonoBehaviour
{
    public NodeInstanceData InstanceData { get; private set; }
    public SpellNode NodeClone { get; private set; }
    private List<SocketUI> _sockets = new List<SocketUI>();

    public void Initialize(NodeInstanceData instanceData, SpellNode nodeClone)
    {
        this.InstanceData = instanceData;
        this.NodeClone = nodeClone;
        var renderer = GetComponent<Renderer>();
        if (renderer != null && NodeClone.icon != null)
        {
 
            renderer.material.SetTexture("_GlyphTex", NodeClone.icon);
        }
        CreateSockets();
    }

    private void CreateSockets()
    {
        if (NodeClone == null) return;
        var controller = SpellGraphController.Instance;
        List<SocketDefinition> socketsToCreate = NodeClone.GetSockets();
        if (socketsToCreate.Count == 0) return;

        float parentScale = this.transform.localScale.x * 1/controller.graphVisualScale;

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

        Vector3 mouseWorldPos = GetMouseWorldPosition();
        Vector3 dirToMouse = (mouseWorldPos - transform.position).normalized;
        lockedSockets[startSocket] = Mathf.Atan2(dirToMouse.z, dirToMouse.x) * Mathf.Rad2Deg;

        DistributeSockets(lockedSockets);
        UpdateAttractionLogic(_sockets);
    }

    private void LayoutForBeingTargeted(SocketUI startSocket)
    {
        var lockedSockets = FindExistingLockedSockets();
        var validTargets = _sockets.Where(s => SpellGraphController.Instance.IsConnectionValid(startSocket, s)).ToList();

        Vector3 dirToNew = (startSocket.ParentRune.transform.position - transform.position).normalized;
        float incomingAngle = Mathf.Atan2(dirToNew.z, dirToNew.x) * Mathf.Rad2Deg;

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
        }

        foreach (var pair in lockedSockets)
        {
            pair.Key.SetTargetLocalPosition(CalculatePositionFromAngle(pair.Value));
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
    private Dictionary<SocketUI, float> FindExistingLockedSockets()
    {
        var locked = new Dictionary<SocketUI, float>();
        var controller = SpellGraphController.Instance;

        foreach (var c in InstanceData.connections)
        {
            SocketUI local = FindSocketByName(c.fromOutputSocketName);
            RuneUI target = controller.FindRuneByGuid(c.targetNodeGUID);
            if (local != null && target != null)
            {
                Vector3 dir = (target.transform.position - transform.position).normalized;
                locked[local] = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
            }
        }
        foreach (var otherRune in controller.GetAllRunes())
        {
            if (otherRune == this) continue;
            foreach (var c in otherRune.InstanceData.connections)
            {
                if (c.targetNodeGUID == this.InstanceData.guid)
                {
                    SocketUI local = FindSocketByName(c.toInputSocketName);
                    if (local != null)
                    {
                        Vector3 dir = (otherRune.transform.position - transform.position).normalized;
                        locked[local] = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
                    }
                }
            }
        }
        return locked;
    }

    private Vector3 GetMouseWorldPosition()
    {
        var controller = SpellGraphController.Instance;
        Ray ray = controller.editorCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane plane = new Plane(-controller.editorCamera.transform.forward, transform.position);
        if (plane.Raycast(ray, out float enter)) { return ray.GetPoint(enter); }
        return transform.position;
    }

    private Vector3 CalculatePositionFromAngle(float angle)
    {

        var controller = SpellGraphController.Instance;
        Transform editorPlane = controller.editorCamera.transform;
        float angleRad = angle * Mathf.Deg2Rad;

        Vector3 worldOffset = controller.socketOrbitRadius * (editorPlane.right * Mathf.Cos(angleRad) + editorPlane.up * Mathf.Sin(angleRad));

        return transform.InverseTransformDirection(worldOffset);
    }

    public SocketUI FindSocketByName(string name) => _sockets.FirstOrDefault(s => s.SocketData.Name == name || s.SocketData.TargetFieldName == name);
}