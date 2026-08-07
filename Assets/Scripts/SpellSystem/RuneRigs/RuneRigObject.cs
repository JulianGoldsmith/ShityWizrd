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
    private GameObject _generatedPhysicsRootObject;
    private GameObject _generatedVisualRootObject;
    private bool _wasConsumed;
    private bool _isSpawned;
    private int _lastBuiltHash = int.MinValue;

    public bool HasRigData => IsLocallyAwake && NetworkNodeCount > 0 && !IsConsumed;
    public int NodeCount => NetworkNodeCount;
    public RuneObject[] RuneObjects => _runeObjects;
    public Transform GeneratedVisualRoot => _generatedVisualRootObject != null ? _generatedVisualRootObject.transform : null;



    [Header("Levitation Position")]
    [Min(0f)] public float LevitationPositionStrength = 20f;
    [Min(0f)] public float LevitationPositionDamping = 8f;
    [Min(0f)] public float MaximumLevitationAcceleration = 30f;

    [Header("Levitation Rotation")]
    [Min(0f)] public float LevitationRotationStrength = 15f;
    [Min(0f)] public float LevitationRotationDamping = 6f;
    [Min(0f)] public float MaximumLevitationAngularAcceleration = 25f;

    [SerializeField] private float levitationBobSpeed = 0.01f;
    [SerializeField] private float levitationBobAmount = 0.2f;

    [Networked] public NetworkBool IsLevitating { get; set; }
    [Networked] public Vector3 LevitationTargetPosition { get; set; }
    [Networked] public Quaternion LevitationTargetRotation { get; set; }

    public override void Spawned()
    {
        base.Spawned();

        _isSpawned = true;
        if (IsLocallyAwake) ReadNetworkDataAndRebuild();
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

        if (!_isSpawned || !IsLocallyAwake)
            return;

        if (IsConsumed)
        {
            if (!_wasConsumed)
            {
                _wasConsumed = true;
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

        ApplyLevitationDrive();
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

    public bool InitializeFromBuffer(RuneRigData rigData, Vector3 position, Quaternion rotation, Vector3 linearVelocity, Vector3 angularVelocity, out string error)
    {
        if (BufferedObject == null)
        {
            error = "RuneRigObject has no BufferedObject.";
            return false;
        }

        if (BufferedObject.IsAwake)
        {
            error = "The RuneRig buffer returned an already-awake object.";
            return false;
        }

        IsLevitating = false;
        if (rb != null) rb.useGravity = true;

        if (!TryWriteRigData(rigData, out error))
            return false;

        if (networkedRB != null)
            networkedRB.Teleport(position, rotation);
        else
            transform.SetPositionAndRotation(position, rotation);

        BufferedObject.BeginWakeInitialization();
        ReadNetworkDataAndRebuild();
        

        if (rb != null)
        {
            rb.linearVelocity = linearVelocity;
            rb.angularVelocity = angularVelocity;
        }

        BufferedObject.CompleteWakeInitialization();
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

        if (sourceRoot == null || sourceRoot.RootConnectionTransform == null || !NodeRegistry.TryGetNodeTemplate(sourceData.Nodes[0].RuneDefinitionId, out SpellNode sourceDefinition))
            return false;

        Vector3 sourcePlugPosition = sourceRoot.RootConnectionTransform.position;

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

            if (sourceRoot == null || sourceRoot.RootConnectionTransform == null || targetBay == null || targetBay.BayTransform == null)
                return false;

            float distance = Vector3.Distance(sourceRoot.RootConnectionTransform.position, targetBay.BayTransform.position);

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

    public bool TryDetachRune(int nodeIndex, PlayerRef bufferOwner, Vector3 detachedPosition, Quaternion detachedRotation, out NetworkObject detachedObject)
    {
        detachedObject = null;
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

        detachedObject = runeRigBuffer.GetBufferedObject(out _);

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
        if (!detachedRig.InitializeFromBuffer(splitResult.DetachedRig, detachedPosition, detachedRotation, detachedVelocity, detachedAngularVelocity, out string detachedError))
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

        ReadNetworkDataAndRebuild();

        Debug.Log($"[RuneRigObject] Detached node {nodeIndex} using player {bufferOwner}'s RuneRig buffer.", this);
        return true;
    }


    private void ReadNetworkDataAndRebuild()
    {
        Debug.Log("Reading Net Data and Rebuild on RuneRig");
        int nodeCount = Mathf.Min(NetworkNodeCount, RuneRigLimits.MaxNodes);

        if (nodeCount == 0)
        {
            _rigData = default;
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

        _generatedPhysicsRootObject = new GameObject("GeneratedRunePhysics");
        _generatedPhysicsRootObject.transform.SetParent(rootParent, false);

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

            RuneBay parentBay = null;

            if (nodeIndex > 0)
            {
                RuneObject parentRune = _runeObjects[nodeData.ParentNodeIndex];
                parentBay = parentRune.GetBay(nodeData.ParentBayIndex);

                if (parentBay == null || parentBay.BayTransform == null)
                    return StopBuild($"Node {nodeData.ParentNodeIndex} has no valid bay {nodeData.ParentBayIndex}.", out error);
            }

            GameObject runeGameObject = Instantiate(definition.PhysicalRune.PhysicalPrefab, _generatedPhysicsRootObject.transform, false);
            runeGameObject.name = $"Rune_{nodeIndex}_{definition.nodeName}";
            runeGameObject.transform.localPosition = Vector3.zero;
            runeGameObject.transform.localRotation = Quaternion.identity;

            if (!runeGameObject.TryGetComponent(out RuneObject runeObject))
                return StopBuild($"The physical prefab for '{definition.nodeName}' needs RuneObject on its root.", out error);

            runeGameObject.transform.localScale = Vector3.one * runeObject.Size;

            if (runeObject.RootConnectionTransform == null)
                return StopBuild($"The physical prefab for '{definition.nodeName}' needs a RootConnectionTransform.", out error);

            if (nodeIndex > 0)
            {
                Transform targetConnection = parentBay.BayTransform;
                Transform sourceConnection = runeObject.RootConnectionTransform;

                Quaternion rotationDelta = targetConnection.rotation * Quaternion.Inverse(sourceConnection.rotation);
                runeGameObject.transform.rotation = rotationDelta * runeGameObject.transform.rotation;
                runeGameObject.transform.position += targetConnection.position - sourceConnection.position;
            }

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
            Physics.SyncTransforms();
            rb.ResetCenterOfMass();
            rb.ResetInertiaTensor();
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
        if (_generatedPhysicsRootObject != null)
        {
            _generatedPhysicsRootObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(_generatedPhysicsRootObject);
            else
                DestroyImmediate(_generatedPhysicsRootObject);
        }

        if (_generatedVisualRootObject != null)
        {
            _generatedVisualRootObject.SetActive(false);

            if (Application.isPlaying)
                Destroy(_generatedVisualRootObject);
            else
                DestroyImmediate(_generatedVisualRootObject);
        }

        _generatedPhysicsRootObject = null;
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

    #region Levataion

    public override void PickUpItem(NetworkObject playerObject)
    {
        if (!IsLocallyAwake) return;
        StopLevitation();
        base.PickUpItem(playerObject);
    }

    public void BeginLevitation()
    {
        if (!IsLocallyAwake || rb == null)
            return;

        LevitationTargetPosition = rb.position;
        LevitationTargetRotation = rb.rotation;
        IsLevitating = true;

        rb.useGravity = false;
        rb.WakeUp();
    }

    public void StopLevitation()
    {
        IsLevitating = false;

        if (rb == null)
            return;

        rb.useGravity = true;
        if (!rb.isKinematic) rb.WakeUp();
    }

    private void ApplyLevitationDrive()
    {
        if (rb == null)
            return;

        rb.useGravity = !IsLevitating;

        if (!IsLevitating)
            return;

        Vector3 levitationBobTarget = LevitationTargetPosition + (Vector3.up * levitationBobAmount * Mathf.Sin(Runner.Tick * levitationBobSpeed));

        Vector3 positionError = levitationBobTarget - rb.position;
        Vector3 positionAcceleration = positionError * LevitationPositionStrength - rb.linearVelocity * LevitationPositionDamping;
        positionAcceleration = Vector3.ClampMagnitude(positionAcceleration, MaximumLevitationAcceleration);

        rb.AddForce(positionAcceleration, ForceMode.Acceleration);

        Quaternion rotationError = Quaternion.Normalize(LevitationTargetRotation * Quaternion.Inverse(rb.rotation));

        if (rotationError.w < 0f)
            rotationError = new Quaternion(-rotationError.x, -rotationError.y, -rotationError.z, -rotationError.w);

        rotationError.ToAngleAxis(out float angleDegrees, out Vector3 rotationAxis);

        Vector3 angularAcceleration = rotationAxis * angleDegrees * Mathf.Deg2Rad * LevitationRotationStrength - rb.angularVelocity * LevitationRotationDamping;
        angularAcceleration = Vector3.ClampMagnitude(angularAcceleration, MaximumLevitationAngularAcceleration);

        rb.AddTorque(angularAcceleration, ForceMode.Acceleration);
    }

    public override void OnBufferedWake(int wakeTick, bool isActivationTick)
    {
        base.OnBufferedWake(wakeTick, isActivationTick);
        _wasConsumed = IsConsumed;

        if (!IsConsumed && (_lastBuiltHash != RigDataHash || _runeObjects == null))
            ReadNetworkDataAndRebuild();
    }

    public override void OnBufferedSleep(int sleepTick)
    {
        base.OnBufferedSleep(sleepTick);
        _wasConsumed = false;
       // _lastBuiltHash = int.MinValue;
       // ClearRuneObjects();
    }
    #endregion
}
