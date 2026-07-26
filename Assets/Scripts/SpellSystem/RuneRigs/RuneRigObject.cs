using System;
using Fusion;
using UnityEngine;

public class RuneRigObject : DraggableItem
{
    [Header("Rune Rig")]
    public Transform RuneContainer;
    public Transform VisualContainer;

    [Header("Attachment")]
    public float AttachmentDistance = 0.3f;
    public float AttachmentValidationTolerance = 0.1f;
    public bool LockNewConnections;

    [Networked] public byte NetworkNodeCount { get; set; }

    [Networked, Capacity(RuneRigLimits.MaxNodes)]
    public NetworkArray<RuneNodeData> NetworkNodes { get; }

    [Networked] public int RigDataHash { get; set; }
    [Networked] public NetworkBool IsConsumed { get; set; }

    private RuneRigData _rigData;
    private RuneObject[] _runeObjects;
    private GameObject _generatedRootObject;
    private GameObject _generatedVisualRootObject;
    private bool _hasRigData;
    private bool _wasConsumed;
    private bool _isSpawned;
    private int _lastBuiltHash = int.MinValue;

    public bool HasRigData => NetworkNodeCount > 0 && !IsConsumed;
    public int NodeCount => NetworkNodeCount;
    public RuneObject[] RuneObjects => _runeObjects;

    public override void Spawned()
    {
        base.Spawned();

        _isSpawned = true;
        ReadNetworkDataAndRebuild();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _isSpawned = false;
        ClearRuneObjects();
        base.Despawned(runner, hasState);
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (!_isSpawned)
            return;

        if (IsConsumed)
        {
            if (!_wasConsumed)
            {
                _wasConsumed = true;
                _hasRigData = false;
                ClearRuneObjects();
            }

            return;
        }

        if (_wasConsumed)
        {
            _wasConsumed = false;
            _lastBuiltHash = int.MinValue;
        }

        if (_lastBuiltHash != RigDataHash)
            ReadNetworkDataAndRebuild();
    }


    public bool TryWriteRigData(RuneRigData rigData, out string error)
    {
        if (Object == null || !Object.IsValid)
        {
            error = "RuneRigObject is not a valid network object.";
            return false;
        }

        RuneRigValidationResult validation = RuneRigValidator.Validate(rigData, RuneRigRootMode.LooseRig);

        if (!validation.IsValid)
        {
            error = validation.ToString();
            return false;
        }

        int previousCount = NetworkNodeCount;

        for (int i = 0; i < rigData.NodeCount; i++)
            NetworkNodes.Set(i, rigData.Nodes[i]);

        for (int i = rigData.NodeCount; i < previousCount; i++)
            NetworkNodes.Set(i, default);

        NetworkNodeCount = (byte)rigData.NodeCount;
        RigDataHash = CalculateRigDataHash(rigData);
        IsConsumed = false;

        error = null;
        return true;
    }

    public RuneRigData GetRigDataCopy()
    {
        int nodeCount = Mathf.Min(NetworkNodeCount, RuneRigLimits.MaxNodes);

        if (nodeCount == 0)
            return default;

        RuneNodeData[] nodes = new RuneNodeData[nodeCount];

        for (int i = 0; i < nodeCount; i++)
            nodes[i] = NetworkNodes[i];

        return new RuneRigData(nodes);
    }

    public RuneObject GetRuneObject(int nodeIndex)
    {
        if (_runeObjects == null || nodeIndex < 0 || nodeIndex >= _runeObjects.Length)
            return null;

        return _runeObjects[nodeIndex];
    }

    protected override Vector3 GetLocalManipulationAnchor(Vector3 handWorldPosition) {
        if (rb != null) return rb.centerOfMass;
        return base.GetLocalManipulationAnchor(handWorldPosition);
    }

    public bool TryFindClosestAttachment(out NetworkId targetRigId, out byte targetNodeIndex, out byte targetBayIndex)
    {
        targetRigId = default;
        targetNodeIndex = 0;
        targetBayIndex = 0;

        if (!HasRigData || _runeObjects == null || _runeObjects.Length == 0)
            return false;

        RuneRigData sourceData = GetRigDataCopy();
        RuneObject sourceRoot = _runeObjects[0];

        if (sourceRoot == null || !NodeRegistry.TryGetNodeTemplate(sourceData.Nodes[0].RuneDefinitionId, out SpellNode sourceDefinition))
            return false;

        Vector3 sourcePlugPosition = sourceRoot.transform.position;
        RuneRigObject bestTarget = null;
        int bestTargetNodeIndex = -1;
        byte bestTargetBayIndex = 0;
        float bestDistance = AttachmentDistance;

        RuneRigObject[] allRigs = FindObjectsByType<RuneRigObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (RuneRigObject targetRig in allRigs)
        {
            if (targetRig == null || targetRig == this || !targetRig.HasRigData)
                continue;

            RuneRigData targetData = targetRig.GetRigDataCopy();

            for (int nodeIndex = 0; nodeIndex < targetData.NodeCount; nodeIndex++)
            {
                RuneObject targetRune = targetRig.GetRuneObject(nodeIndex);

                if (targetRune == null || targetRune.Bays == null)
                    continue;

                if (!NodeRegistry.TryGetNodeTemplate(targetData.Nodes[nodeIndex].RuneDefinitionId, out SpellNode targetDefinition))
                    continue;

                if (targetDefinition.PhysicalRune == null || !targetDefinition.PhysicalRune.AcceptsChild(sourceDefinition.GetRuneType()))
                    continue;

                foreach (RuneBay bay in targetRune.Bays)
                {
                    if (bay == null || bay.BayTransform == null)
                        continue;

                    if (bay.BayIndex >= targetData.Nodes[nodeIndex].BayCapacity)
                        continue;

                    bool occupied = false;

                    for (int childIndex = 1; childIndex < targetData.NodeCount; childIndex++)
                    {
                        RuneNodeData child = targetData.Nodes[childIndex];

                        if (child.ParentNodeIndex == nodeIndex && child.ParentBayIndex == bay.BayIndex)
                        {
                            occupied = true;
                            break;
                        }
                    }

                    if (occupied)
                        continue;

                    float distance = Vector3.Distance(sourcePlugPosition, bay.BayTransform.position);

                    if (distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    bestTarget = targetRig;
                    bestTargetNodeIndex = nodeIndex;
                    bestTargetBayIndex = bay.BayIndex;
                }
            }
        }

        if (bestTarget == null || bestTarget.Object == null || !bestTarget.Object.IsValid)
            return false;

        targetRigId = bestTarget.Object.Id;
        targetNodeIndex = (byte)bestTargetNodeIndex;
        targetBayIndex = bestTargetBayIndex;
        return true;
    }

    public bool TryAttachToBay(NetworkId targetRigId, byte targetNodeIndex, byte targetBayIndex)
    {
        if (!HasRigData || !targetRigId.IsValid)
            return false;

        if (!Runner.TryFindObject(targetRigId, out NetworkObject targetObject))
            return false;

        if (!targetObject.TryGetComponent(out RuneRigObject targetRig))
            return false;

        if (targetRig == this || !targetRig.HasRigData)
            return false;

        RuneRigData sourceData = GetRigDataCopy();
        RuneRigData targetData = targetRig.GetRigDataCopy();

        if (sourceData.NodeCount == 0 || targetNodeIndex >= targetData.NodeCount)
            return false;

        RuneNodeData targetNode = targetData.Nodes[targetNodeIndex];

        if (targetBayIndex >= targetNode.BayCapacity)
            return false;

        for (int childIndex = 1; childIndex < targetData.NodeCount; childIndex++)
        {
            RuneNodeData child = targetData.Nodes[childIndex];

            if (child.ParentNodeIndex == targetNodeIndex && child.ParentBayIndex == targetBayIndex)
                return false;
        }

        if (!NodeRegistry.TryGetNodeTemplate(sourceData.Nodes[0].RuneDefinitionId, out SpellNode sourceDefinition))
            return false;

        if (!NodeRegistry.TryGetNodeTemplate(targetNode.RuneDefinitionId, out SpellNode targetDefinition))
            return false;

        if (targetDefinition.PhysicalRune == null || !targetDefinition.PhysicalRune.AcceptsChild(sourceDefinition.GetRuneType()))
            return false;

        if (HasStateAuthority)
        {
            RuneObject sourceRoot = GetRuneObject(0);
            RuneObject targetRune = targetRig.GetRuneObject(targetNodeIndex);
            RuneBay targetBay = targetRune != null ? targetRune.GetBay(targetBayIndex) : null;

            if (sourceRoot == null || targetBay == null || targetBay.BayTransform == null)
                return false;

            float distance = Vector3.Distance(sourceRoot.transform.position, targetBay.BayTransform.position);

            if (distance > AttachmentDistance + AttachmentValidationTolerance)
            {
                Debug.LogWarning($"[RuneRigObject] Host rejected attachment distance {distance:F3}.", this);
                return false;
            }
        }

        RuneRigMergeResult mergeResult = RuneRigOperations.Merge(targetData, sourceData, targetNodeIndex, targetBayIndex, LockNewConnections);

        if (!mergeResult.Succeeded)
        {
            Debug.LogWarning($"[RuneRigObject] Attachment rejected: {mergeResult.Error}", this);
            return false;
        }

        if (!targetRig.TryWriteRigData(mergeResult.CombinedRig, out string error))
        {
            Debug.LogWarning($"[RuneRigObject] Destination rig update failed: {error}", this);
            return false;
        }

        IsConsumed = true;

        Debug.Log($"[RuneRigObject] Attached '{name}' to '{targetRig.name}', node {targetNodeIndex}, bay {targetBayIndex}.", this);

        if (HasStateAuthority)
            Runner.Despawn(Object);

        return true;
    }

    public bool TryDetachRune(int nodeIndex, PlayerRef bufferOwner, Vector3 detachedPosition, Quaternion detachedRotation)
    {
        RuneRigData sourceData = GetRigDataCopy();

        if (sourceData.NodeCount == 0)
            return false;

        if (nodeIndex <= 0 || nodeIndex >= sourceData.NodeCount)
            return false;

        RuneRigSplitResult splitResult = RuneRigOperations.Split(sourceData, nodeIndex);

        if (!splitResult.Succeeded)
        {
            Debug.LogWarning($"[RuneRigObject] Detachment rejected: {splitResult.Error}", this);
            return false;
        }

        if (ObjectBufferAllocator.Instance == null)
        {
            Debug.LogWarning("[RuneRigObject] No ObjectBufferAllocator exists.", this);
            return false;
        }

        ObjectBuffer runeRigBuffer = ObjectBufferAllocator.Instance.GetRuneRigBuffer(bufferOwner);

        if (runeRigBuffer == null)
        {
            Debug.LogWarning($"[RuneRigObject] No RuneRig buffer exists for player {bufferOwner}.", this);
            return false;
        }

        Vector3 detachedVelocity = rb != null ? rb.GetPointVelocity(detachedPosition) : Vector3.zero;
        Vector3 detachedAngularVelocity = rb != null ? rb.angularVelocity : Vector3.zero;

        NetworkObject detachedObject = runeRigBuffer.GetBufferedObject(detachedPosition, detachedRotation, out _);

        if (detachedObject == null)
        {
            Debug.LogWarning($"[RuneRigObject] Player {bufferOwner}'s RuneRig buffer returned no object.", this);
            return false;
        }

        if (!detachedObject.TryGetComponent(out RuneRigObject detachedRig))
        {
            Debug.LogError("[RuneRigObject] Buffered object has no RuneRigObject.", detachedObject);

            if (detachedObject.HasStateAuthority)
                Runner.Despawn(detachedObject);

            return false;
        }

        if (!detachedRig.TryWriteRigData(splitResult.DetachedRig, out string detachedError))
        {
            Debug.LogWarning($"[RuneRigObject] Detached rig could not be initialized: {detachedError}", this);

            if (detachedObject.HasStateAuthority)
                Runner.Despawn(detachedObject);

            return false;
        }

        if (!TryWriteRigData(splitResult.RemainingRig, out string remainingError))
        {
            Debug.LogWarning($"[RuneRigObject] Remaining rig could not be updated: {remainingError}", this);

            if (detachedObject.HasStateAuthority)
                Runner.Despawn(detachedObject);

            return false;
        }

        if (detachedRig.rb != null)
        {
            detachedRig.rb.linearVelocity = detachedVelocity;
            detachedRig.rb.angularVelocity = detachedAngularVelocity;
            detachedRig.rb.WakeUp();
        }

        Debug.Log($"[RuneRigObject] Detached node {nodeIndex} using player {bufferOwner}'s RuneRig buffer.", this);
        return true;
    }


    private void ReadNetworkDataAndRebuild()
    {
        int nodeCount = Mathf.Min(NetworkNodeCount, RuneRigLimits.MaxNodes);

        if (nodeCount == 0)
        {
            _rigData = default;
            _hasRigData = false;
            _lastBuiltHash = RigDataHash;
            ClearRuneObjects();
            return;
        }

        RuneNodeData[] nodes = new RuneNodeData[nodeCount];

        for (int i = 0; i < nodeCount; i++)
            nodes[i] = NetworkNodes[i];

        _rigData = new RuneRigData
        {
            Nodes = nodes
        };

        _hasRigData = true;
        _lastBuiltHash = RigDataHash;

        if (!TryRebuild(out string error))
            Debug.LogError($"[RuneRigObject] Reconstruction failed: {error}", this);
    }

    private bool TryRebuild(out string error)
    {
        ClearRuneObjects();

        if (VisualContainer == null)
            return StopBuild("RuneRigObject has no VisualContainer assigned.", out error);

        Transform rootParent = RuneContainer != null ? RuneContainer : transform;

        _generatedVisualRootObject = new GameObject("GeneratedRuneVisuals");
        _generatedVisualRootObject.transform.SetParent(VisualContainer, false);

        _runeObjects = new RuneObject[_rigData.NodeCount];

        for (int nodeIndex = 0; nodeIndex < _rigData.NodeCount; nodeIndex++)
        {
            RuneNodeData nodeData = _rigData.Nodes[nodeIndex];

            if (!NodeRegistry.TryGetNodeTemplate(nodeData.RuneDefinitionId, out SpellNode definition))
                return StopBuild($"Rune definition {nodeData.RuneDefinitionId} could not be resolved.", out error);

            if (definition.PhysicalRune == null || definition.PhysicalRune.PhysicalPrefab == null)
                return StopBuild($"'{definition.nodeName}' has no physical prefab.", out error);

            Transform parentTransform = rootParent;

            if (nodeIndex > 0)
            {
                RuneObject parentRune = _runeObjects[nodeData.ParentNodeIndex];
                RuneBay parentBay = parentRune.GetBay(nodeData.ParentBayIndex);

                if (parentBay == null || parentBay.BayTransform == null)
                    return StopBuild($"Node {nodeData.ParentNodeIndex} has no valid bay {nodeData.ParentBayIndex}.", out error);

                parentTransform = parentBay.BayTransform;
            }

            GameObject runeGameObject = Instantiate(definition.PhysicalRune.PhysicalPrefab, parentTransform, false);
            runeGameObject.name = $"Rune_{nodeIndex}_{definition.nodeName}";
            runeGameObject.transform.localPosition = Vector3.zero;
            runeGameObject.transform.localRotation = Quaternion.identity;
            runeGameObject.transform.localScale = Vector3.one;

            if (nodeIndex == 0)
                _generatedRootObject = runeGameObject;

            if (!runeGameObject.TryGetComponent(out RuneObject runeObject))
                return StopBuild($"The physical prefab for '{definition.nodeName}' needs RuneObject on its root.", out error);

            if (runeObject.VisualRoot == null || runeObject.VisualRoot == runeObject.transform)
                return StopBuild($"The physical prefab for '{definition.nodeName}' needs a child assigned as its VisualRoot.", out error);

            runeObject.OwningRig = this;
            runeObject.NodeIndex = nodeIndex;
            _runeObjects[nodeIndex] = runeObject;

            Transform runeVisual = runeObject.VisualRoot;
            runeVisual.name = $"Visual_{nodeIndex}_{definition.nodeName}";
            runeVisual.SetParent(_generatedVisualRootObject.transform, true);
        }

        if (rb != null)
        {
            rb.ResetCenterOfMass();
            rb.ResetInertiaTensor();
            rb.WakeUp();
        }

        error = null;
        return true;
    }

    private bool StopBuild(string message, out string error)
    {
        error = message;
        ClearRuneObjects();
        return false;
    }

    private void ClearRuneObjects()
    {
        if (_generatedRootObject != null)
        {
            _generatedRootObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(_generatedRootObject);
            else
                DestroyImmediate(_generatedRootObject);
        }

        if (_generatedVisualRootObject != null)
        {
            _generatedVisualRootObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(_generatedVisualRootObject);
            else
                DestroyImmediate(_generatedVisualRootObject);
        }

        _generatedRootObject = null;
        _generatedVisualRootObject = null;
        _runeObjects = null;
    }

    private static RuneRigData CopyRigData(RuneRigData source)
    {
        if (source.Nodes == null)
            return default;

        RuneNodeData[] copiedNodes = new RuneNodeData[source.Nodes.Length];
        Array.Copy(source.Nodes, copiedNodes, source.Nodes.Length);

        return new RuneRigData
        {
            Nodes = copiedNodes
        };
    }

    private static int CalculateRigDataHash(RuneRigData rigData)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + rigData.NodeCount;

            for (int i = 0; i < rigData.NodeCount; i++)
            {
                RuneNodeData node = rigData.Nodes[i];
                hash = hash * 31 + node.RuneDefinitionId;
                hash = hash * 31 + node.ParentNodeIndex;
                hash = hash * 31 + node.ParentBayConnection;
                hash = hash * 31 + node.BayCapacity;
            }

            return hash;
        }
    }
}
