using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Unity.AppUI.Core;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.VFX;
using System.IO;
using TMPro;

public class SpellGraphController : MonoBehaviour
{

    public static SpellGraphController Instance { get; private set; }

    //list of all scriptable object made nodes some of these may be inherited classes.
    public List<SpellNode> availableNodeTemplates { get; private set; } = new List<SpellNode>();
    public List<CasterNode> availableCasterNodes;
    public List<CoreNode> availableCoreNodes;
    public List<BehaviourNode> availableBehaviourNodes;
    public List<TriggerNode> availableTriggerNodes;
    public List<FilterNode> availableFilterNodes;
    public List<EffectNode> availableEffectNodes;
    public List<ValueNode> availableValueNodes;
    public List<SubgraphNode> availableSubgraphNodes;

    public EntryPointControlNode entryPointTemplate;

    //the prefab for all runes,  RUNE = UI,      NODE = scriptableObect.  

    public Transform editorWorldParent;

    private SpellGraph currentGraph;

    //prefabs
    public GameObject runePrefab;
    public GameObject socketPrefab;
    public GameObject connectionPrefab;


    [Header("Editor State")]
    public bool isEditingSubgraph = false;
    public Transform mainGraphParent; 
    public Transform subgraphEditorParent;
    private Transform ActiveGraphParent => isEditingSubgraph ? subgraphEditorParent : mainGraphParent;


    [System.Serializable]
    public class RuneAppearance
    {
        public RuneCategoryTag category;
        public Mesh defaultMesh;
        public Material defaultMaterial;
        public float defaultScale = 1f;
    }
    public float graphVisualScale = 0.13f;

    [Header("Rune Appearance Defaults")]
    public List<RuneAppearance> runeAppearances;
    private Dictionary<RuneCategoryTag, RuneAppearance> _appearanceMap;

    [ColorUsage(true, true)]
    public UnityEngine.Color executionLinkColor = UnityEngine.Color.white;

    [ColorUsage(true, true)]
    public UnityEngine.Color behaviourLinkColor = UnityEngine.Color.cyan;

    [ColorUsage(true, true)]
    public UnityEngine.Color filterLinkColor = UnityEngine.Color.green;

    [ColorUsage(true, true)]
    public UnityEngine.Color dataColor = new UnityEngine.Color(1.0f, 0.5f, 0.0f);

    //dragging vars
    private Transform _nodeBeingDragged;
    private Vector3 _dragOffset;
    private float _dragPlaneDistance = 0.25f;
    public Camera editorCamera;
    public LayerMask runeLayerMask;
    public LayerMask ConnectionLayerMask;

    //connection
    public bool _isConnecting = false;
    public SocketUI _connectionStartSocket;
    private ConnectionControllerUI _draftConnection;
    private Transform _dummyEndPoint;
    private List<ConnectionControllerUI> _permanentConnections = new List<ConnectionControllerUI>();

    private GameObject _selectedObject;
    private UnityEngine.Color _originalRuneColor; 
    private UnityEngine.Color _originalConnectionColor;
    private static MaterialPropertyBlock _propBlock;

    private RuneUI _focusedRune;
    public float focusProximityRadius = 150f; //distance in pixels

    public float socketAttractionRange = 2.0f;
    public float socketAttractionSpeed = 0.5f;
    public float socketOrbitRadius = 1.2f;

    public NetworkedInventoryManager inventory;

    [Header("Subgraph Editor State")]
    private RuneUI _subgraphRootNodeUI; 
    private MaterialPropertyBlock _propSubgraphBlock;
    public GameObject subgraphInterfacePanel; 
    public Transform socketListContentParent; 
    public GameObject exposedSocketUIPrefab; 
    private List<ExposedSocketInfo> _tempExposedSockets = new List<ExposedSocketInfo>();
    public TMP_InputField subgraphNameInputField;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        currentGraph = ScriptableObject.CreateInstance<SpellGraph>();
        editorCamera = Camera.main;

        _appearanceMap = new Dictionary<RuneCategoryTag, RuneAppearance>();
        foreach (var appearance in runeAppearances)
        {
            _appearanceMap[appearance.category] = appearance;
        }

        _propBlock = new MaterialPropertyBlock();
    }

    private void Start()
    {
        PopulateAvailableTemplateNodesList();
        _dummyEndPoint = new GameObject("ConnectionDummyEnd").transform;
        CreateNewGraph();
        subgraphInterfacePanel.SetActive(false);
    }

    private void Update()
    {
        HandleInput();
        HandleRuneDragging();
        HandleRuneConnecting();
        HandleUIInput();
    }


    #region subGraphEditor

    public void ToggleEditorMode(bool enteringSubgraphMode)
    {
        isEditingSubgraph = enteringSubgraphMode;

        mainGraphParent.gameObject.SetActive(!isEditingSubgraph);
        subgraphEditorParent.gameObject.SetActive(isEditingSubgraph);
        subgraphInterfacePanel.SetActive(isEditingSubgraph);

        if (isEditingSubgraph)
        {
            StartNewSubgraph();
        }
        else
        {
            _tempExposedSockets.Clear();
            _subgraphRootNodeUI = null;
            subgraphNameInputField.text = "";
            RefreshExposurePanel();
        }
    }

    public void StartNewSubgraph()
    {
        foreach (Transform child in subgraphEditorParent)
        {
            Destroy(child.gameObject);
        }
        Debug.Log("Entered Subgraph Editor. Ready to create a new rune.");
    }

    public void SetSubgraphRootNode(RuneUI targetRune)
    {
        if (!isEditingSubgraph || targetRune == null)
        {
            return;
        }

        if (_subgraphRootNodeUI != null)
        {
            if (_subgraphRootNodeUI.TryGetComponent<Renderer>(out var oldRenderer))
            {
                oldRenderer.SetPropertyBlock(null);
            }
        }

        _subgraphRootNodeUI = targetRune;
        Debug.Log($"Set '{targetRune.name}' as the new subgraph root.");

        if (_subgraphRootNodeUI.TryGetComponent<Renderer>(out var newRenderer))
        {

            if (_propSubgraphBlock == null) _propSubgraphBlock = new MaterialPropertyBlock();

            newRenderer.GetPropertyBlock(_propSubgraphBlock);
            _propSubgraphBlock.SetColor("_Color", UnityEngine.Color.yellow * 2f);
            newRenderer.SetPropertyBlock(_propSubgraphBlock);
        }
    }

    public void ExposeSocket(SocketUI socketToExpose)
    {
        if (socketToExpose == null) return;

        if (_tempExposedSockets.Any(s => s.internalNodeGuid == socketToExpose.ParentRune.InstanceData.guid && s.internalSocketName == socketToExpose.SocketData.Name))
        {
            Debug.Log("Socket is already exposed.");
            return;
        }

        var socketInfo = new ExposedSocketInfo
        {
            exposedName = socketToExpose.SocketData.Name, 
            internalNodeGuid = socketToExpose.ParentRune.InstanceData.guid,
            internalSocketName = string.IsNullOrEmpty(socketToExpose.SocketData.TargetFieldName) ? socketToExpose.SocketData.Name : socketToExpose.SocketData.TargetFieldName,
            direction = socketToExpose.SocketData.Direction,
            type = socketToExpose.SocketData.Type,
            tag = socketToExpose.SocketData.Tag
        };

        _tempExposedSockets.Add(socketInfo);
        RefreshExposurePanel();
    }

    public void UpdateExposedSocketName(string internalGuid, string internalName, string newName)
    {
        var index = _tempExposedSockets.FindIndex(s => s.internalNodeGuid == internalGuid && s.internalSocketName == internalName);
        if (index != -1)
        {
            var info = _tempExposedSockets[index];
            info.exposedName = newName;
            _tempExposedSockets[index] = info;
            Debug.Log($"Updated socket name to '{newName}'");
        }
    }

    public void UnexposeSocket(string internalGuid, string internalName)
    {
        _tempExposedSockets.RemoveAll(s => s.internalNodeGuid == internalGuid && s.internalSocketName == internalName);
        RefreshExposurePanel();
    }

    private void RefreshExposurePanel()
    {

        foreach (Transform child in socketListContentParent)
        {
            Destroy(child.gameObject);
        }

        foreach (var info in _tempExposedSockets)
        {
            var uiInstance = Instantiate(exposedSocketUIPrefab, socketListContentParent);
            uiInstance.GetComponent<ExposedSocketUI>().Initialize(info, this);
        }
    }

    public void SaveCurrentSubgraph()
    {
        if (!isEditingSubgraph) return;

        string runeName = subgraphNameInputField.text;
        if (string.IsNullOrWhiteSpace(runeName))
        {
            Debug.LogError("Subgraph Rune Name cannot be empty.");
            return;
        }
        if (_subgraphRootNodeUI == null)
        {
            Debug.LogError("A root node must be selected before saving.");
            return;
        }

        SubgraphNode newSubgraphAsset = ScriptableObject.CreateInstance<SubgraphNode>();

        newSubgraphAsset.nodeName = runeName; 
        newSubgraphAsset.category = _subgraphRootNodeUI.NodeClone.category; 

        foreach (Transform childRune in subgraphEditorParent)
        {
            if (childRune.TryGetComponent<RuneUI>(out var runeUI))
            {
                newSubgraphAsset.internalNodes.Add(runeUI.InstanceData);
            }
        }

        newSubgraphAsset.rootNodeGuid = _subgraphRootNodeUI.InstanceData.guid;
        newSubgraphAsset.exposedSockets = new List<ExposedSocketInfo>(_tempExposedSockets);

#if UNITY_EDITOR
    string directoryPath = "Assets/Scripts/SpellSystem/Spells_ScriptableObjects/Nodes/SubGraphNodes";
    if (!Directory.Exists(directoryPath))
    {
        Directory.CreateDirectory(directoryPath);
    }
    
    string assetPath = Path.Combine(directoryPath, $"{runeName}.asset");
    assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

    AssetDatabase.CreateAsset(newSubgraphAsset, assetPath);
    AssetDatabase.SaveAssets();
    AssetDatabase.Refresh();

    Debug.Log($"Successfully saved Subgraph Rune to {assetPath}");
    
    availableNodeTemplates.Add(newSubgraphAsset);
#else
        Debug.LogWarning("Saving subgraphs is only supported in the Unity Editor.");
#endif

        ToggleEditorMode(false);
    }

    #endregion

    private void HandleUIInput(){
        if (Keyboard.current.enterKey.wasPressedThisFrame)
        {
            EquipSpellToActiveItem();
        }
        if (!isEditingSubgraph)
        {
            if (Keyboard.current.nKey.wasPressedThisFrame)
            {
                ClearAndCreateNewSpellOnActiveItem();
            }
            if (Keyboard.current.gKey.wasPressedThisFrame)
            {
                ToggleEditorMode(!isEditingSubgraph);
            }
        }
        if (Keyboard.current.deleteKey.wasPressedThisFrame)
        {
            DeleteSelectedObject();
        }
       
    }

    private void HandleInput()
    {

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = editorCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, runeLayerMask);

            if (hits.Length > 0)
            {
                hits = hits.OrderBy(h => h.distance).ToArray();

                //this sorts hit priority
                var runeHit = hits.FirstOrDefault(h => h.collider.GetComponent<RuneUI>() != null);
                if (runeHit.collider != null)
                {
                    SelectObject(runeHit.collider.gameObject);
                    _nodeBeingDragged = runeHit.transform;
                    _dragPlaneDistance = Vector3.Dot(editorCamera.transform.forward, runeHit.point - editorCamera.transform.position);
                    _dragOffset = _nodeBeingDragged.position - runeHit.point;
                    return;
                }

                var socketHit = hits.FirstOrDefault(h => h.collider.GetComponent<SocketUI>() != null);
                if (socketHit.collider != null)
                {
                    SelectObject(null);
                    StartConnection(socketHit.collider.GetComponent<SocketUI>());
                    return;
                }

                var connectionHit = hits.FirstOrDefault(h => h.collider.GetComponent<ConnectionControllerUI>() != null);
                if (connectionHit.collider != null)
                {
                    SelectObject(connectionHit.collider.gameObject);
                    return;
                }
            }
            else
            {
                SelectObject(null);
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            if (_nodeBeingDragged != null)
            {
                _nodeBeingDragged = null;
            }
            else if (_isConnecting)
            {
                Ray ray = editorCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
                RaycastHit[] hits = Physics.RaycastAll(ray, 100f, runeLayerMask);

                SocketUI endSocket = null;

                if (hits.Length > 0)
                {
                    endSocket = hits.OrderBy(h => h.distance)
                                    .Select(h => h.collider.GetComponent<SocketUI>())
                                    .FirstOrDefault(s => s != null && IsConnectionValid(_connectionStartSocket, s));
                }

                if (endSocket != null)
                {
                    EndConnection(endSocket);
                }
                else
                {
                    CancelConnection();
                }
            }
        }

        if (isEditingSubgraph && Mouse.current.rightButton.wasPressedThisFrame)
        {
            Ray ray = editorCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit, 100f, runeLayerMask))
            {
                SocketUI hitSocket = hit.collider.GetComponent<SocketUI>();
                if (hitSocket != null)
                {
                    ExposeSocket(hitSocket);
                    return;
                }

                RuneUI hitRune = hit.collider.GetComponent<RuneUI>();
                if (hitRune != null)
                {
                    SetSubgraphRootNode(hitRune);
                }
            }
        }
    }

    private void SelectObject(GameObject newSelection)
    {
        if (_selectedObject != null)
        {
            if (_selectedObject.TryGetComponent<Renderer>(out var oldRenderer))
            {
                oldRenderer.SetPropertyBlock(null);
            }
            else if (_selectedObject.TryGetComponent<VisualEffect>(out var oldVfx))
            {
                oldVfx.SetVector4("Color", _originalConnectionColor);
            }
        }

        _selectedObject = newSelection;

        if (_selectedObject != null)
        {
            if (_selectedObject.TryGetComponent<Renderer>(out var newRenderer))
            {
                _originalRuneColor = newRenderer.material.color;

                float brightcolor = 0.3f;

                UnityEngine.Color highlightColor = _originalRuneColor + new UnityEngine.Color(brightcolor, brightcolor, brightcolor, 0f);

                newRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_Color", highlightColor); 
                newRenderer.SetPropertyBlock(_propBlock);
            }
            else if (_selectedObject.TryGetComponent<VisualEffect>(out var newVfx))
            {
                _originalConnectionColor = newVfx.GetVector4("Color");

                UnityEngine.Color highlightColor = _originalConnectionColor * 2.5f;
                newVfx.SetVector4("Color", highlightColor);
            }
        }
    }

    #region Dragging runes

    #region delete
    public void DeleteSelectedObject()
    {
        if (_selectedObject == null) return;

        Debug.Log($"Deleting {_selectedObject.name}");

        if (_selectedObject.TryGetComponent(out RuneUI runeToDelete))
        {
            var connectionsToRemove = new List<(NodeInstanceData SourceNode, NodeConnection Connection)>();

            //outward connections to delete
            foreach (var connection in runeToDelete.InstanceData.connections)
            {
                connectionsToRemove.Add((runeToDelete.InstanceData, connection));
            }

            //inward connections to delete
            foreach (var otherRuneData in currentGraph.nodes)
            {
                if (otherRuneData.guid == runeToDelete.InstanceData.guid) continue;

                var connectionsToThisRune = otherRuneData.connections
                    .Where(c => c.targetNodeGUID == runeToDelete.InstanceData.guid);

                foreach (var connection in connectionsToThisRune)
                {
                    connectionsToRemove.Add((otherRuneData, connection));
                }
            }

            foreach (var entry in connectionsToRemove)
            {
                RemoveConnection(entry.SourceNode, entry.Connection);
            }

            currentGraph.liveNodeClonesByGuid.Remove(runeToDelete.InstanceData.guid);
            currentGraph.nodes.Remove(runeToDelete.InstanceData);
            Destroy(runeToDelete.gameObject);

            _selectedObject = null;
        }
        else if(_selectedObject.TryGetComponent(out ConnectionControllerUI connectionToDelete))
    {
            NodeInstanceData sourceNodeData = connectionToDelete.startSocket.ParentRune.InstanceData;

            NodeConnection connectionData = sourceNodeData.connections.FirstOrDefault(c =>
                c.fromOutputSocketName == connectionToDelete.startSocket.SocketData.Name &&
                c.targetNodeGUID == connectionToDelete.endSocket.ParentRune.InstanceData.guid);

            RemoveConnection(sourceNodeData, connectionData);

            _selectedObject = null;
        }
    }

    private static int ParseComboIndexFromSocketName(string socketName)
    {
        if (string.IsNullOrEmpty(socketName))
            return 0;

        var parts = socketName.Split(' ');
        if (parts.Length < 2)
            return 0;

        if (int.TryParse(parts[1], out int oneBased))
        {
            return Mathf.Max(0, oneBased - 1);
        }
        return 0;
    }

    private void RemoveConnection(NodeInstanceData sourceNodeData, NodeConnection connectionData)
    {

        currentGraph.liveNodeClonesByGuid.TryGetValue(sourceNodeData.guid, out SpellNode sourceClone);
        currentGraph.liveNodeClonesByGuid.TryGetValue(connectionData.targetNodeGUID, out SpellNode targetClone);

        if (sourceClone != null && targetClone != null)
        {
            var socketDef = sourceClone.GetSockets().FirstOrDefault(s => s.Name == connectionData.fromOutputSocketName);

            switch (socketDef.Type)
            {
                case SocketType.ExecutionLink:
                    if (sourceClone is EntryPointControlNode entry)
                    {
                        int comboIndex = ParseComboIndexFromSocketName(connectionData.fromOutputSocketName);
                        entry.EnsureComboCapacity();

                        if (comboIndex >= 0 && comboIndex < entry.comboRoots.Count)
                        {
                            entry.comboRoots[comboIndex].Remove(targetClone);
                        }
                    }
                    else if (sourceClone is CoreNode co && targetClone is TriggerNode trigger)
                    {
                        co.triggerNodes.Remove(trigger);
                    }
                    else if (sourceClone is TriggerNode tr)
                    {
                        tr.outcomeNodes.Remove(targetClone); // both Effect and Core nodes
                    }
                    break;

                case SocketType.BehaviourLink:
                    if (sourceClone is BehaviourNode behaviour && targetClone is CoreNode coreNode)
                    {
                        coreNode.behaviourNodes.Remove(behaviour);
                    }
                    break;

                case SocketType.FilterLink:
                    if (sourceClone is FilterNode filter && targetClone is TriggerNode triggerNode)
                    {
                        triggerNode.filterNodes.Remove(filter);
                    }
                    break;

                case SocketType.Data:

                    var inputDef = targetClone.GetSockets().FirstOrDefault(s =>
                        s.Direction == SocketDirection.Input &&
                        s.Type == SocketType.Data &&
                        s.Name == connectionData.toInputSocketName &&
                        (string.IsNullOrEmpty(connectionData.toInputOwnerGUID) || s.OwningNodeGUID == connectionData.toInputOwnerGUID));

                    if (inputDef.Name != null && sourceClone is ValueNode valueNode)
                    {
                        var binder = targetClone.valueContainers.FirstOrDefault(b =>
                            b.TargetFieldName == inputDef.TargetFieldName &&
                            b.OwningNodeGUID == inputDef.OwningNodeGUID);

                        if (binder != null) binder.ModifyingNodes.Remove(valueNode);
                    }
                    break;
            }
        }

        var connectionUI = _permanentConnections.FirstOrDefault(c =>
            c.startSocket?.ParentRune.InstanceData.guid == sourceNodeData.guid &&
            c.endSocket?.ParentRune.InstanceData.guid == connectionData.targetNodeGUID &&
            c.startSocket?.SocketData.Name == connectionData.fromOutputSocketName);

        if (connectionUI != null)
        {
            _permanentConnections.Remove(connectionUI);
            Destroy(connectionUI.gameObject);
        }

        sourceNodeData.connections.Remove(connectionData);
    }
    #endregion

    private void HandleRuneDragging()
    {
        if (_nodeBeingDragged == null) return;

        UpdateDraggedNodePosition(Mouse.current.position.ReadValue());
    }
    private void UpdateDraggedNodePosition(Vector2 screenPos)
    {
        Ray ray = editorCamera.ScreenPointToRay(screenPos);
        //Plane plane = new Plane(Vector3.up, Vector3.up * 0.3f);
        Plane plane = new Plane(editorCamera.transform.forward, transform.position);

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hitPointOnPlane = ray.GetPoint(enter);
            Vector3 newPosition = hitPointOnPlane + transform.rotation * _dragOffset;
            _nodeBeingDragged.position = newPosition;

            var runeUI = _nodeBeingDragged.GetComponent<RuneUI>();
            if (runeUI != null)
            {
                runeUI.InstanceData.position = _nodeBeingDragged.localPosition;
            }
        }
    }

    public void StartDraggingNewRuneFromLibrary(SpellNode template, Vector2 screenPos)
    {
        RuneUI newRuneUI = CreateRune(template, Vector3.zero);
        if (newRuneUI == null) return;

        _nodeBeingDragged = newRuneUI.transform;

        //Plane plane = new Plane(Vector3.up, editorWorldParent.position);
        Plane plane = new Plane(editorCamera.transform.forward, editorWorldParent.position);
        Ray ray = editorCamera.ScreenPointToRay(screenPos);
        if (plane.Raycast(ray, out float enter))
        {
            _dragPlaneDistance = Vector3.Dot(editorCamera.transform.forward, ray.GetPoint(enter) - editorCamera.transform.position);
        }
        _dragOffset = Vector3.zero;

        UpdateDraggedNodePosition(screenPos);
    }
    #endregion

    #region Connecting Runes

    private void HandleRuneConnecting()
    {
        if (!_isConnecting) return;

        Ray ray = editorCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        //Plane plane = new Plane(Vector3.up, _connectionStartSocket.transform.position);
        Plane plane = new Plane(editorCamera.transform.forward, _connectionStartSocket.transform.position);
        if (plane.Raycast(ray, out float enter))
        {
            _dummyEndPoint.position = ray.GetPoint(enter);
        }
    }

    private void StartConnection(SocketUI startSocket)
    {
        _isConnecting = true;
        _connectionStartSocket = startSocket;

        GameObject connection = Instantiate(connectionPrefab, Vector3.zero, Quaternion.identity, ActiveGraphParent);
        _draftConnection = connection.GetComponent<ConnectionControllerUI>();
        UnityEngine.Color connectionColor = GetColorForSocketType(startSocket.SocketData.Type);
        _draftConnection.Initialize(_connectionStartSocket.transform, _dummyEndPoint, connectionColor);
    }

    private void EndConnection(SocketUI endSocket)
    {
        if (!_isConnecting || _connectionStartSocket == null || endSocket == null)
        {
            CancelConnection();
            return;
        }

        SocketUI outputSocket = (_connectionStartSocket.SocketData.Direction == SocketDirection.Output) ? _connectionStartSocket : endSocket;
        SocketUI inputSocket = (_connectionStartSocket.SocketData.Direction == SocketDirection.Input) ? _connectionStartSocket : endSocket;

        if (!IsConnectionValid(outputSocket, inputSocket))
        {
            CancelConnection();
            return;
        }

        var newConnectionData = new NodeConnection
        {

            fromOutputSocketName = !string.IsNullOrEmpty(outputSocket.SocketData.TargetFieldName)
            ? outputSocket.SocketData.TargetFieldName
            : outputSocket.SocketData.Name,

            fromOutputOwnerGUID = outputSocket.SocketData.OwningNodeGUID,

            toInputSocketName = !string.IsNullOrEmpty(inputSocket.SocketData.TargetFieldName)
            ? inputSocket.SocketData.TargetFieldName
            : inputSocket.SocketData.Name,


            toInputOwnerGUID = inputSocket.SocketData.OwningNodeGUID,
        };

        var outSD = outputSocket.SocketData;
        var inSD = inputSocket.SocketData;

        bool outIsData = outSD.Type == SocketType.Data;
        bool inIsData = inSD.Type == SocketType.Data;

        newConnectionData.fromOutputSocketName = outIsData
            ? (string.IsNullOrEmpty(outSD.TargetFieldName) ? outSD.Name : outSD.TargetFieldName)
            : (string.IsNullOrEmpty(outSD.TargetFieldName) ? outSD.Name : outSD.TargetFieldName); // same rule

        newConnectionData.toInputSocketName = inIsData
            ? (string.IsNullOrEmpty(inSD.TargetFieldName) ? inSD.Name : inSD.TargetFieldName)
            : (string.IsNullOrEmpty(inSD.TargetFieldName) ? inSD.Name : inSD.TargetFieldName);

        newConnectionData.fromOutputOwnerGUID = outSD.OwningNodeGUID;
        newConnectionData.toInputOwnerGUID = inSD.OwningNodeGUID;


        newConnectionData.targetNodeGUID = inputSocket.ParentRune.InstanceData.guid;


        outputSocket.ParentRune.InstanceData.connections.Add(newConnectionData);

        var sourceData = outputSocket.ParentRune.InstanceData;
        var targetData = inputSocket.ParentRune.InstanceData;

        currentGraph.CreateNodeLink(sourceData, targetData, newConnectionData);

        CreateVisualConnection(outputSocket, inputSocket);
        CancelConnection();
    }

    private SpellNode ResolvePromotedOwner(SpellGraph graph, NodeInstanceData parentData, SocketDefinition socketDef)
    {
        var owner = socketDef.OwningNodeGUID;

        if (string.IsNullOrEmpty(owner))
            return null; 


        if (owner == parentData.guid)
            return null;


        if (parentData.childNodeGUIDs.Contains(owner) &&
            graph.liveNodeClonesByGuid.TryGetValue(owner, out var childClone))
            return childClone;


        Debug.LogWarning($"[SpellGraph] Owner '{owner}' not found under parent '{parentData.guid}' for socket '{socketDef.Name}");
        return null;
    }

    private void CancelConnection()
    {
        if (_draftConnection != null)
        {
            Destroy(_draftConnection.gameObject);
        }
        _isConnecting = false;
        _connectionStartSocket = null;
        _draftConnection = null;
    }

    public bool IsConnectionValid(SocketUI start, SocketUI end)
    {

        if (start.ParentRune == end.ParentRune) return false;
        if (start.SocketData.Direction == end.SocketData.Direction) return false;
        if (start.SocketData.Type != end.SocketData.Type) return false;
        if (start.SocketData.Type == SocketType.Data)
        {
            if (start.SocketData.DataType != end.SocketData.DataType ||
                start.SocketData.Tag != end.SocketData.Tag)
            {
                return false;
            }
        }
        //?i would put a single input rule here if i go with the customisable rune sockets idea later.

        return true;
    }

    #endregion

    public void CreateNewGraph()
    {
        foreach (var runeUI in currentGraph.runeUIsByGuid.Values)
        {
            if (runeUI != null) Destroy(runeUI.gameObject);
        }
        currentGraph.runeUIsByGuid.Clear();

        foreach (var conn in _permanentConnections)
        {
            if (conn != null) Destroy(conn.gameObject);
        }
        _permanentConnections.Clear();

        currentGraph = ScriptableObject.CreateInstance<SpellGraph>();

        //RuneUI entryPointRune = CreateRune(entryPointTemplate, editorWorldParent.position); 
        RuneUI entryPointRune = CreateRune(entryPointTemplate, Vector3.zero); 

        currentGraph.entryPointControllerNodeGuid = entryPointRune.InstanceData.guid;
        currentGraph.entryPointControllerNode = (EntryPointControlNode)entryPointRune.NodeClone;
        
    }

    private RuneUI CreateRune(SpellNode nodeTemplate, Vector3 position, NodeInstanceData existingData = null)
    {

        bool isNewNode = existingData == null;

        NodeInstanceData instanceData = existingData ?? new NodeInstanceData
        {
            guid = System.Guid.NewGuid().ToString(),
            nodeTemplateName = nodeTemplate.name,
        };
        instanceData.position = position;

        /////////////////////////////////////////////////////////////////////////////////////////////////////

        SpellNode nodeClone = nodeTemplate.CloneThisNode();
        nodeClone.InstanceGuid = instanceData.guid;


        currentGraph.liveNodeClonesByGuid[instanceData.guid] = nodeClone;
        if (isNewNode)
        {
            currentGraph.nodes.Add(instanceData);
        }


        if (nodeTemplate is SubgraphNode subTpl && nodeClone is SubgraphNode subClone)
        {
            var guidMap = new Dictionary<string, string>();
            var liveInternalNodes = new List<NodeInstanceData>();

            foreach (var internalData in subTpl.internalNodes)
            {
                var internalTemplate = FindTemplateByName(internalData.nodeTemplateName);
                if (internalTemplate == null) continue;

                var liveInst = new NodeInstanceData
                {
                    guid = System.Guid.NewGuid().ToString(),
                    nodeTemplateName = internalTemplate.name,
                    sourceTemplateNodeGuid = internalData.guid,   // keep template id
                    position = internalData.position,             // 
                };

                var liveClone = internalTemplate.CloneThisNode();
                liveClone.InstanceGuid = liveInst.guid;

                currentGraph.nodes.Add(liveInst);
                currentGraph.liveNodeClonesByGuid[liveInst.guid] = liveClone;

                instanceData.childNodeGUIDs.Add(liveInst.guid);
                liveInternalNodes.Add(liveInst);
                guidMap[internalData.guid] = liveInst.guid;
            }

            foreach (var internalData in subTpl.internalNodes)
            {
                if (!guidMap.TryGetValue(internalData.guid, out var liveGuid)) continue;
                var liveInst = GetNodeInstance(liveGuid);
                foreach (var conn in internalData.connections)
                {
                    var newConn = new NodeConnection
                    {
                        fromOutputSocketName = conn.fromOutputSocketName,
                        fromOutputOwnerGUID = guidMap[conn.fromOutputOwnerGUID],
                        targetNodeGUID = guidMap[conn.targetNodeGUID],
                        toInputSocketName = conn.toInputSocketName,
                        toInputOwnerGUID = guidMap[conn.toInputOwnerGUID],
                    };
                    liveInst.connections.Add(newConn);
                    currentGraph.CreateNodeLink(liveInst, GetNodeInstance(newConn.targetNodeGUID), newConn);
                }
            }

            subClone.internalNodes = liveInternalNodes;                
            subClone.rootNodeGuid = guidMap[subTpl.rootNodeGuid];     

            // rewrite exposed sockets to live owner guids
            for (int i = 0; i < subClone.exposedSockets.Count; i++)
            {
                var info = subClone.exposedSockets[i];
                if (guidMap.TryGetValue(info.internalNodeGuid, out var liveOwner))
                {
                    info.internalNodeGuid = liveOwner;
                    subClone.exposedSockets[i] = info;
                }
                else
                {
                    Debug.LogWarning($"[Subgraph] Exposed socket owner template guid '{info.internalNodeGuid}' not found in guidMap.");
                }
            }
        }
        else if (nodeClone is CoreNode coreClone)
        {
            foreach (var b in coreClone.defaultBehaviourNodes)
            {
                var behaviourInstanceData = new NodeInstanceData
                {
                    guid = System.Guid.NewGuid().ToString(),
                    nodeTemplateName = b.name
                };
                currentGraph.nodes.Add(behaviourInstanceData);
                instanceData.childNodeGUIDs.Add(behaviourInstanceData.guid);
                b.InstanceGuid = behaviourInstanceData.guid;
                currentGraph.liveNodeClonesByGuid[behaviourInstanceData.guid] = b;
            }

            foreach (var t in coreClone.defaultTriggerNodes)
            {
                var triggerInstanceData = new NodeInstanceData
                {
                    guid = System.Guid.NewGuid().ToString(),
                    nodeTemplateName = t.name
                };
                currentGraph.nodes.Add(triggerInstanceData);
                instanceData.childNodeGUIDs.Add(triggerInstanceData.guid);
                t.InstanceGuid = triggerInstanceData.guid;
                currentGraph.liveNodeClonesByGuid[triggerInstanceData.guid] = t;
            }

        }


        GameObject runeObject = Instantiate(runePrefab, 
            position, 
            transform.rotation, 
            ActiveGraphParent);
        runeObject.transform.localPosition = position;
        runeObject.name = $"Rune_{nodeTemplate.nodeName}";

        var meshFilter = runeObject.GetComponent<MeshFilter>();
        var meshRenderer = runeObject.GetComponent<MeshRenderer>();

        _appearanceMap.TryGetValue(nodeTemplate.category, out RuneAppearance appearance);

        if (nodeTemplate.overrideMesh != null)
        {
            meshFilter.mesh = nodeTemplate.overrideMesh;
        }
        else if (appearance?.defaultMesh != null)
        {
            meshFilter.mesh = appearance.defaultMesh;
        }

        if (nodeTemplate.overrideMaterial != null)
        {
            meshRenderer.material = nodeTemplate.overrideMaterial;
        }
        else if(appearance?.defaultMaterial != null)
        {
            meshRenderer.material = appearance.defaultMaterial;
        }
        if (meshRenderer.material.HasProperty("_Seed"))
        {
            meshRenderer.material.SetFloat("_Seed", UnityEngine.Random.Range(0, 1000));
        }

        runeObject.transform.localScale = Vector3.one * ((nodeTemplate.ovverideVisualScale != 1)
            ? nodeTemplate.ovverideVisualScale
            : (appearance?.defaultScale ?? 1f)) * graphVisualScale;

        RuneUI runeUI = runeObject.AddComponent<RuneUI>();
        runeUI.Initialize(instanceData, nodeClone);
        currentGraph.runeUIsByGuid[instanceData.guid] = runeUI;

        return runeUI;
    }

    private void PopulateAvailableTemplateNodesList()
    {
        availableNodeTemplates.Clear();
  
        availableNodeTemplates.AddRange(availableCasterNodes.Cast<SpellNode>());
        availableNodeTemplates.AddRange(availableCoreNodes.Cast<SpellNode>());
        availableNodeTemplates.AddRange(availableBehaviourNodes.Cast<SpellNode>());
        availableNodeTemplates.AddRange(availableTriggerNodes.Cast<SpellNode>());
        availableNodeTemplates.AddRange(availableFilterNodes.Cast<SpellNode>());
        availableNodeTemplates.AddRange(availableEffectNodes.Cast<SpellNode>());
        availableNodeTemplates.AddRange(availableValueNodes.Cast<SpellNode>());
        availableNodeTemplates.AddRange(availableSubgraphNodes.Cast<SpellNode>());
        availableNodeTemplates.Add(entryPointTemplate);
    }

    //helpers
    public SpellNode FindTemplateByName(string templateName)
    {
        return availableNodeTemplates.FirstOrDefault(n => n.name == templateName);
    }

    private UnityEngine.Color GetColorForSocketType(SocketType type)
    {
        switch (type)
        {
            case SocketType.ExecutionLink: return executionLinkColor;
            case SocketType.BehaviourLink: return behaviourLinkColor;
            case SocketType.FilterLink: return filterLinkColor;
            case SocketType.Data: return dataColor;
            default: return UnityEngine.Color.magenta;
        }
    }
    public bool IsRuneValidTarget(RuneUI rune)
    {
        if (!_isConnecting || rune == _connectionStartSocket.ParentRune)
        {
            return false;
        }

        return rune.GetComponentsInChildren<SocketUI>(true)
                   .Any(socket => IsConnectionValid(_connectionStartSocket, socket));
    }

    public RuneUI FindRuneByGuid(string guid)
    {
        currentGraph.runeUIsByGuid.TryGetValue(guid, out RuneUI rune);
        return rune;
    }

    public IEnumerable<RuneUI> GetAllRunes()
    {
        return currentGraph.runeUIsByGuid.Values;
    }


    public NodeInstanceData GetNodeInstance(string guid)
    {
        return currentGraph.nodes.FirstOrDefault(n => n.guid == guid);
    }

    /// /////////////////////////////////////////////////load/ Save///////////////////////////



    #region Save/load
    public void SaveCurrentGraph(string savePath)
    {
        SpellGraph graphToSave = ScriptableObject.CreateInstance<SpellGraph>();
        graphToSave.entryPointControllerNodeGuid = this.currentGraph.entryPointControllerNodeGuid;

        foreach (var nodeData in currentGraph.nodes)
        {
            if (currentGraph.runeUIsByGuid.TryGetValue(nodeData.guid, out RuneUI runeUI))
            {
                nodeData.position = runeUI.transform.localPosition;
            }

            graphToSave.nodes.Add(nodeData);
        }

        // --- NEW: TEST THE TRANSLATOR ---
        SpellNetworkData testPayload = graphToSave.CompileToNetworkData();
        Debug.Log($"<color=cyan>[Translator Test]</color> Compiled {testPayload.NodeCount} Nodes and {testPayload.WireCount} Wires cleanly!");

        for (int i = 0; i < testPayload.WireCount; i++)
        {
            WireData wire = testPayload.Wires[i];
            Debug.Log($"<color=green>[Translator Test]</color> Wire {i}: Node {wire.FromNodeIndex} (Pin {wire.FromSocketIndex}) ---> Node {wire.ToNodeIndex} (Pin {wire.ToSocketIndex})");
        }
        // --------------------------------

        string json = JsonUtility.ToJson(graphToSave, true);
        System.IO.File.WriteAllText(savePath, json);
        Debug.Log($"Spell saved to: {savePath}");

        Destroy(graphToSave);
    }

    public SpellGraph LoadSpellData(string savePath)
    {
        if (!System.IO.File.Exists(savePath))
        {
            Debug.LogError($"Save file not found: {savePath}");
            return null;
        }

        string json = System.IO.File.ReadAllText(savePath);

        SpellGraph spell_graph = SpellGraph.FromJson(json);
        return spell_graph;
    }

    public void BuildGraphView(SpellGraph graphToBuild)
    {
        if (graphToBuild == null)
        {
            Debug.LogError("No spell Graph to build");
            CreateNewGraph();
            return;
        }

        ClearGraphView();

        this.currentGraph = graphToBuild;

        var childNodeGuids = new HashSet<string>();
        foreach (var nodeData in currentGraph.nodes)
        {
            foreach (var childGuid in nodeData.childNodeGUIDs)
            {
                childNodeGuids.Add(childGuid);
            }
        }
        childNodeGuids = new HashSet<string>();
        foreach (var nodeData in currentGraph.nodes)
        {
            var template = FindTemplateByName(nodeData.nodeTemplateName);
            if (template is SubgraphNode)
            {
                foreach (var childGuid in nodeData.childNodeGUIDs)
                {
                    childNodeGuids.Add(childGuid);
                }
            }
        }


        foreach (var nodeData in currentGraph.nodes)
        {
            if (!childNodeGuids.Contains(nodeData.guid))
            {
                if (currentGraph.liveNodeClonesByGuid.TryGetValue(nodeData.guid, out SpellNode nodeClone))
                {
                    CreateRuneVisuals(nodeClone, nodeData);
                }
            }
        }

        foreach (var nodeData in currentGraph.nodes)
        {
            foreach (var connectionData in nodeData.connections)
            {
                currentGraph.runeUIsByGuid.TryGetValue(nodeData.guid, out RuneUI fromRune);
                currentGraph.runeUIsByGuid.TryGetValue(connectionData.targetNodeGUID, out RuneUI toRune);

                if (fromRune != null && toRune != null)
                {
                    SocketUI fromSocket = fromRune
                        .GetComponentsInChildren<SocketUI>(true)
                        .FirstOrDefault(s =>
                            s.SocketData.Direction == SocketDirection.Output &&
                            (s.SocketData.Name == connectionData.fromOutputSocketName ||
                            s.SocketData.TargetFieldName == connectionData.fromOutputSocketName) &&
                            (string.IsNullOrEmpty(connectionData.fromOutputOwnerGUID) ||
                            s.SocketData.OwningNodeGUID == connectionData.fromOutputOwnerGUID));

                    SocketUI toSocket = toRune
                        .GetComponentsInChildren<SocketUI>(true)
                        .FirstOrDefault(s =>
                            s.SocketData.Direction == SocketDirection.Input &&
                            (s.SocketData.Name == connectionData.toInputSocketName ||
                            s.SocketData.TargetFieldName == connectionData.toInputSocketName) &&
                            (string.IsNullOrEmpty(connectionData.toInputOwnerGUID) ||
                            s.SocketData.OwningNodeGUID == connectionData.toInputOwnerGUID));

                    if (fromSocket != null && toSocket != null)
                    {
                        CreateVisualConnection(fromSocket, toSocket);
                    }
                }
            }
        }
    }

    private void CreateRuneVisuals(SpellNode nodeClone, NodeInstanceData instanceData)
    {
        GameObject runeObject = Instantiate(
            runePrefab,
            transform.rotation * instanceData.position + transform.position, 
            transform.rotation, 
            ActiveGraphParent);
        runeObject.transform.localPosition = instanceData.position;
        runeObject.name = $"Rune_{nodeClone.nodeName}";

        var meshFilter = runeObject.GetComponent<MeshFilter>();
        var meshRenderer = runeObject.GetComponent<MeshRenderer>();

        _appearanceMap.TryGetValue(nodeClone.category, out RuneAppearance appearance);

        if (nodeClone.overrideMesh != null)
        {
            meshFilter.mesh = nodeClone.overrideMesh;
        }
        else if (appearance?.defaultMesh != null)
        {
            meshFilter.mesh = appearance.defaultMesh;
        }

        if (nodeClone.overrideMaterial != null)
        {
            meshRenderer.material = nodeClone.overrideMaterial;
        }
        else if (appearance?.defaultMaterial != null)
        {
            meshRenderer.material = appearance.defaultMaterial;
        }
        if (meshRenderer.material.HasProperty("_Seed"))
        {
            meshRenderer.material.SetFloat("_Seed", UnityEngine.Random.Range(0,1000));
        }

        runeObject.transform.localScale = Vector3.one * ((nodeClone.ovverideVisualScale != 1)
            ? nodeClone.ovverideVisualScale
            : (appearance?.defaultScale ?? 1f)) * graphVisualScale;

        RuneUI runeUI = runeObject.AddComponent<RuneUI>();
        runeUI.Initialize(instanceData, nodeClone);
        currentGraph.runeUIsByGuid[instanceData.guid] = runeUI;
    }

    private void CreateVisualConnection(SocketUI fromSocket, SocketUI toSocket)
    {
        GameObject connectionObj = Instantiate(connectionPrefab, Vector3.zero, Quaternion.identity, ActiveGraphParent);
        ConnectionControllerUI connectionController = connectionObj.GetComponent<ConnectionControllerUI>();
        UnityEngine.Color connectionColor = GetColorForSocketType(fromSocket.SocketData.Type);
        connectionController.Initialize(fromSocket.transform, toSocket.transform, connectionColor);
        _permanentConnections.Add(connectionController);
    }



    //Called from editor button
    public void SaveSpellToAssets(string spellName)
    {
        #if UNITY_EDITOR

            string directoryPath = System.IO.Path.Combine(Application.dataPath, "Scripts/SpellSystem/SavedSpells");

            if (!System.IO.Directory.Exists(directoryPath))
            {
                System.IO.Directory.CreateDirectory(directoryPath);
            }

            string path = System.IO.Path.Combine(directoryPath, spellName + ".json");
    
            SaveCurrentGraph(path);

            AssetDatabase.Refresh();
        #else
                //need to add other method for player saved spells 
                Debug.Log("only implemented for use in Unity Editor.");
        #endif
    }

    public SpellGraph GetSpellFromAssestsByName(string spellName)
    {
        if (string.IsNullOrEmpty(spellName)) return null;

        string path = System.IO.Path.Combine(Application.dataPath, "Scripts/SpellSystem/SavedSpells", spellName + ".json");
        return LoadSpellData(path);
    }

    //called from editor button
    public void LoadSpellByNameToCurrentItem(string spellName)
    {
        string path = GetSpellPathByName(spellName);

        SpellGraph loadedGraph = LoadSpellData(path);

        EquipSpellToActiveItem(loadedGraph);
    }

    public string GetSpellPathByName(string spellName, bool fromAssets = true)
    {
        if (fromAssets)
        {
            return System.IO.Path.Combine(Application.dataPath, "Scripts/SpellSystem/SavedSpells", spellName + ".json");
        }
        else
        {
            Debug.LogWarning("Saving to other location not implemented yet, using Assets to find spell");
            return System.IO.Path.Combine(Application.dataPath, "Scripts/SpellSystem/SavedSpells", spellName + ".json");
        }
        
    }

    //equip spell in to current item
    public void EquipSpellToActiveItem()
    {
        EquipSpellToActiveItem(currentGraph);
    }
    public void EquipSpellToActiveItem(SpellGraph graphToEquip)
    {
        if (inventory != null && inventory.activeItem != null)
        {
            EquipableItem itemComponent = inventory.activeItem.GetComponent<EquipableItem>();
            if (itemComponent != null)
            {
                itemComponent.EquipSpellToPrimary(currentGraph);
                Debug.Log($"SUCCESS: Equipped '{graphToEquip.name}' to '{itemComponent.name}' on '{inventory.gameObject.name}");
            }
        }
        else
        {
            Debug.LogWarning("Could not find an active item to equip the spell to!");
        }
    }

    public void EditSpellFromActiveItem(Vector3 posToPlaceEditor)
    {
        // TODO:
        // check if this works properly.
        // Needs to make sure you communicate.
        this.transform.position = posToPlaceEditor;
        this.transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(-90f,0,0);

        SpellGraph spellToEdit = null;

        if (inventory != null && inventory.activeItem != null)
        {
            EquipableItem itemComponent = inventory.activeItem.GetComponent<EquipableItem>();

            if (itemComponent != null)
            {
                spellToEdit = itemComponent.primaryActionSpell;
            }
        }

        if (spellToEdit != null)
        {
            Debug.Log($"Loading spell '{spellToEdit.name}' into editor.");
            BuildGraphView(spellToEdit);
        }
        else
        {
            // If no item or no spell, create a fresh, blank graph
            Debug.Log("No active spell found. Creating a new graph.");
            CreateNewGraph();
        }


    }

    public void ClearGraphView()
    {
        if (currentGraph != null)
        {
            foreach (var runeUI in currentGraph.runeUIsByGuid.Values)
            {
                if (runeUI != null)
                    Destroy(runeUI.gameObject);
            }
            currentGraph.runeUIsByGuid.Clear();
        }
        foreach (var conn in _permanentConnections)
        {
            if (conn != null)
                Destroy(conn.gameObject);
        }
        _permanentConnections.Clear();
    }

    public void ClearAndCreateNewSpellOnActiveItem()
    {
        SpellGraph newGraph = ScriptableObject.CreateInstance<SpellGraph>();
        newGraph.name = "New Blank Spell";

        NodeInstanceData entryPointData = new NodeInstanceData
        {
            guid = System.Guid.NewGuid().ToString(),
            nodeTemplateName = entryPointTemplate.name,
            position = Vector3.zero
        };
        newGraph.nodes.Add(entryPointData);

        SpellNode entryPointClone = entryPointTemplate.CloneThisNode();
        entryPointClone.InstanceGuid = entryPointData.guid;
        newGraph.liveNodeClonesByGuid[entryPointData.guid] = entryPointClone;

        newGraph.entryPointControllerNodeGuid = entryPointData.guid;
        newGraph.entryPointControllerNode = (EntryPointControlNode)entryPointClone;

        EquipableItem item = inventory.activeItem.GetComponent<EquipableItem>();
        item.EquipSpellToPrimary(newGraph);
        Debug.Log($"Created new blank spell assigned to '{item.name}'.");

        BuildGraphView(newGraph);
    }



    #endregion
}
