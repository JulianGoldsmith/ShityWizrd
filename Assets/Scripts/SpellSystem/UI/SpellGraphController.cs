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

    public EntryPointNode entryPointTemplate;
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

        byte nodeIndex = socketToExpose.ParentRune.NodeIndex;
        byte socketIndex = (byte)socketToExpose.ParentRune.Sockets.IndexOf(socketToExpose);

        if (_tempExposedSockets.Any(s => s.InternalNodeIndex == nodeIndex && s.InternalSocketIndex == socketIndex))
        {
            Debug.Log("Socket is already exposed.");
            return;
        }

        var socketInfo = new ExposedSocketInfo
        {
            ExposedName = socketToExpose.SocketData.Name,
            InternalNodeIndex = nodeIndex,
            InternalSocketIndex = socketIndex,
            Direction = socketToExpose.SocketData.Direction,
            Type = socketToExpose.SocketData.Type,
            Tag = socketToExpose.SocketData.Tag
        };

        _tempExposedSockets.Add(socketInfo);
        RefreshExposurePanel();
    }

    public void UpdateExposedSocketName(byte internalNodeIndex, byte internalSocketIndex, string newName)
    {
        var index = _tempExposedSockets.FindIndex(s => s.InternalNodeIndex == internalNodeIndex && s.InternalSocketIndex == internalSocketIndex);
        if (index != -1)
        {
            var info = _tempExposedSockets[index];
            info.ExposedName = newName;
            _tempExposedSockets[index] = info;
            Debug.Log($"Updated socket name to '{newName}'");
        }
    }

    public void UnexposeSocket(byte internalNodeIndex, byte internalSocketIndex)
    {
        _tempExposedSockets.RemoveAll(s => s.InternalNodeIndex == internalNodeIndex && s.InternalSocketIndex == internalSocketIndex);
        RefreshExposurePanel();
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
        newSubgraphAsset.category = _subgraphRootNodeUI.ReadOnlyTemplate.category;

        // Notice how clean this is now! We just copy the math array over!
        newSubgraphAsset.InternalGraph = currentGraph.Data;
        newSubgraphAsset.RootNodeIndex = _subgraphRootNodeUI.NodeIndex;
        newSubgraphAsset.ExposedSockets = new List<ExposedSocketInfo>(_tempExposedSockets);

#if UNITY_EDITOR
        string directoryPath = "Assets/Scripts/SpellSystem/Spells_ScriptableObjects/Nodes/SubGraphNodes";
        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
        
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
    public void DeleteSelectedObject()
    {
        if (_selectedObject == null) return;
        Debug.Log($"Deleting {_selectedObject.name}");

        if (_selectedObject.TryGetComponent(out RuneUI runeToDelete))
        {
            byte index = runeToDelete.NodeIndex;

            // 1. Tombstone the Node
            currentGraph.Data.Nodes[index].TemplateID = 0;

            // 2. Tombstone all attached Wires
            for (int i = 0; i < currentGraph.Data.Wires.Length; i++)
            {
                if (currentGraph.Data.Wires[i].FromSocketIndex != 255 &&
                   (currentGraph.Data.Wires[i].FromNodeIndex == index || currentGraph.Data.Wires[i].ToNodeIndex == index))
                {
                    currentGraph.Data.Wires[i].FromSocketIndex = 255;
                }
            }

            // 3. Clean up UI visuals
            var connsToDelete = _permanentConnections.Where(c => c.startSocket?.ParentRune == runeToDelete || c.endSocket?.ParentRune == runeToDelete).ToList();
            foreach (var conn in connsToDelete)
            {
                _permanentConnections.Remove(conn);
                Destroy(conn.gameObject);
            }

            currentGraph.runeUIsByGuid.Remove(index.ToString());
            Destroy(runeToDelete.gameObject);
            _selectedObject = null;
        }
        else if (_selectedObject.TryGetComponent(out ConnectionControllerUI connectionToDelete))
        {
            byte fromNode = connectionToDelete.startSocket.ParentRune.NodeIndex;
            byte toNode = connectionToDelete.endSocket.ParentRune.NodeIndex;

            // 1. Tombstone the specific Wire
            for (int i = 0; i < currentGraph.Data.Wires.Length; i++)
            {
                if (currentGraph.Data.Wires[i].FromNodeIndex == fromNode && currentGraph.Data.Wires[i].ToNodeIndex == toNode)
                {
                    currentGraph.Data.Wires[i].FromSocketIndex = 255;
                    break;
                }
            }

            _permanentConnections.Remove(connectionToDelete);
            Destroy(connectionToDelete.gameObject);
            _selectedObject = null;
        }
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
                // Instantly update the master network array!
                currentGraph.Data.Nodes[runeUI.NodeIndex].Position = _nodeBeingDragged.localPosition;
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
        if (!_isConnecting || _connectionStartSocket == null || endSocket == null) { CancelConnection(); return; }

        SocketUI outputSocket = (_connectionStartSocket.SocketData.Direction == SocketDirection.Output) ? _connectionStartSocket : endSocket;
        SocketUI inputSocket = (_connectionStartSocket.SocketData.Direction == SocketDirection.Input) ? _connectionStartSocket : endSocket;

        if (!IsConnectionValid(outputSocket, inputSocket)) { CancelConnection(); return; }

        int wireIndex = GetFreeWireIndex();
        if (wireIndex == -1) { Debug.LogError("Max wires reached!"); CancelConnection(); return; }

        // Grab the array coordinates!
        byte fromNodeIndex = outputSocket.ParentRune.NodeIndex;
        byte fromPinIndex = (byte)outputSocket.ParentRune.Sockets.IndexOf(outputSocket);

        byte toNodeIndex = inputSocket.ParentRune.NodeIndex;
        byte toPinIndex = (byte)inputSocket.ParentRune.Sockets.IndexOf(inputSocket);

        // Write the connection to the Master Array!
        currentGraph.Data.Wires[wireIndex] = new WireData
        {
            FromNodeIndex = fromNodeIndex,
            FromSocketIndex = fromPinIndex,
            ToNodeIndex = toNodeIndex,
            ToSocketIndex = toPinIndex
        };

        if (wireIndex > currentGraph.Data.MaxWireIndex) currentGraph.Data.MaxWireIndex = (byte)wireIndex;

        CreateVisualConnection(outputSocket, inputSocket);
        CancelConnection();
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
            // NEW: Only check the Tag! 
            // We removed the 'DataType' match because wrapper classes (RuntimeFloatProperty) 
            // will always fail a strict type check against pure floats.
            if (start.SocketData.Tag != end.SocketData.Tag)
            {
                return false;
            }
        }
        //?i would put a single input rule here if i go with the customisable rune sockets idea later.

        return true;
    }

    #endregion

    public byte GetFreeNodeIndex()
    {
        for (byte i = 0; i < currentGraph.Data.Nodes.Length; i++)
        {
            if (currentGraph.Data.Nodes[i].TemplateID == 0) return i;
        }
        return 255;
    }

    public int GetFreeWireIndex()
    {
        for (int i = 0; i < currentGraph.Data.Wires.Length; i++)
        {
            if (currentGraph.Data.Wires[i].FromSocketIndex == 255) return i;
        }
        return -1;
    }

    private RuneUI CreateRune(SpellNode nodeTemplate, Vector3 position, int forcedIndex = -1)
    {
        byte index = forcedIndex != -1 ? (byte)forcedIndex : GetFreeNodeIndex();
        if (index == 255) { Debug.LogError("Graph is full!"); return null; }

        // 1. Write the math to the network array!
        currentGraph.Data.Nodes[index] = new NetworkNodeData
        {
            TemplateID = nodeTemplate.NetworkNodeID,
            Position = position
        };

        if (index > currentGraph.Data.MaxNodeIndex) currentGraph.Data.MaxNodeIndex = index;

        // 2. Generate the visual UI (No cloning!)
        GameObject runeObject = Instantiate(runePrefab, position, transform.rotation, ActiveGraphParent);
        runeObject.transform.localPosition = position;
        runeObject.name = $"Rune_{nodeTemplate.nodeName}_{index}";

        var meshFilter = runeObject.GetComponent<MeshFilter>();
        var meshRenderer = runeObject.GetComponent<MeshRenderer>();
        _appearanceMap.TryGetValue(nodeTemplate.category, out RuneAppearance appearance);

        if (nodeTemplate.overrideMesh != null) meshFilter.mesh = nodeTemplate.overrideMesh;
        else if (appearance?.defaultMesh != null) meshFilter.mesh = appearance.defaultMesh;

        if (nodeTemplate.overrideMaterial != null) meshRenderer.material = nodeTemplate.overrideMaterial;
        else if (appearance?.defaultMaterial != null) meshRenderer.material = appearance.defaultMaterial;

        runeObject.transform.localScale = Vector3.one * ((nodeTemplate.ovverideVisualScale != 1) ? nodeTemplate.ovverideVisualScale : (appearance?.defaultScale ?? 1f)) * graphVisualScale;

        RuneUI runeUI = runeObject.AddComponent<RuneUI>();
        runeUI.Initialize(index, nodeTemplate); // UI now only knows its Index and Read-Only Template!

        currentGraph.runeUIsByGuid[index.ToString()] = runeUI; // Temp hack to keep other scripts happy for one more step

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




    /// /////////////////////////////////////////////////load/ Save///////////////////////////



    #region Save/load


    public SpellGraph LoadSpellData(string savePath)
    {
        if (!System.IO.File.Exists(savePath))
        {
            Debug.LogError($"Save file not found: {savePath}");
            return null;
        }

        string json = System.IO.File.ReadAllText(savePath);

        SpellGraph spell_graph = ScriptableObject.CreateInstance<SpellGraph>();
        JsonUtility.FromJsonOverwrite(json, spell_graph);

        return spell_graph;
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

    public void CreateNewGraph()
    {
        ClearGraphView();
        currentGraph = ScriptableObject.CreateInstance<SpellGraph>();
        currentGraph.InitializeEmptyGraph();
        for (int i = 0; i < currentGraph.Data.Wires.Length; i++) currentGraph.Data.Wires[i].FromSocketIndex = 255; // Mark all wires as tombstones

        // Spawn entry point at index 0! (Laying the groundwork for your Weapon system!)
        CreateRune(entryPointTemplate, Vector3.zero, 0);
    }

    public void ClearAndCreateNewSpellOnActiveItem()
    {
        CreateNewGraph();
        EquipableItem item = inventory.activeItem.GetComponent<EquipableItem>();
        item.EquipSpellToPrimary(currentGraph);
    }

    public void SaveCurrentGraph(string savePath)
    {
        // Notice how insanely simple saving is now. We just serialize the pure data struct!
        string json = JsonUtility.ToJson(currentGraph, true);
        System.IO.File.WriteAllText(savePath, json);
        Debug.Log($"Spell saved to: {savePath}");
    }

    public void BuildGraphView(SpellGraph graphToBuild)
    {
        if (graphToBuild == null) { CreateNewGraph(); return; }
        ClearGraphView();
        this.currentGraph = graphToBuild;

        // 1. Rebuild Nodes from Array
        for (byte i = 0; i <= currentGraph.Data.MaxNodeIndex; i++)
        {
            var nodeData = currentGraph.Data.Nodes[i];
            if (nodeData.TemplateID != 0)
            {
                SpellNode template = availableNodeTemplates.FirstOrDefault(t => t.NetworkNodeID == nodeData.TemplateID);
                if (template != null) CreateRune(template, nodeData.Position, i);
            }
        }

        // 2. Rebuild Wires from Array
        for (int i = 0; i <= currentGraph.Data.MaxWireIndex; i++)
        {
            var wire = currentGraph.Data.Wires[i];
            if (wire.FromSocketIndex != 255)
            {
                currentGraph.runeUIsByGuid.TryGetValue(wire.FromNodeIndex.ToString(), out RuneUI fromRune);
                currentGraph.runeUIsByGuid.TryGetValue(wire.ToNodeIndex.ToString(), out RuneUI toRune);

                if (fromRune != null && toRune != null)
                {
                    SocketUI fromSocket = fromRune.Sockets[wire.FromSocketIndex];
                    SocketUI toSocket = toRune.Sockets[wire.ToSocketIndex];
                    CreateVisualConnection(fromSocket, toSocket);
                }
            }
        }
    }



    #endregion
}
