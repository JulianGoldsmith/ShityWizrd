using System;

public struct RuneRigMergeResult
{
    public bool Succeeded;
    public string Error;
    public RuneRigData CombinedRig;

    public static RuneRigMergeResult Success(RuneRigData combinedRig)
    {
        return new RuneRigMergeResult
        {
            Succeeded = true,
            Error = null,
            CombinedRig = combinedRig
        };
    }

    public static RuneRigMergeResult Failure(string error)
    {
        return new RuneRigMergeResult
        {
            Succeeded = false,
            Error = error,
            CombinedRig = default
        };
    }
}

public struct RuneRigSplitResult
{
    public bool Succeeded;
    public string Error;
    public RuneRigData RemainingRig;
    public RuneRigData DetachedRig;

    public static RuneRigSplitResult Success(RuneRigData remainingRig, RuneRigData detachedRig)
    {
        return new RuneRigSplitResult
        {
            Succeeded = true,
            Error = null,
            RemainingRig = remainingRig,
            DetachedRig = detachedRig
        };
    }

    public static RuneRigSplitResult Failure(string error)
    {
        return new RuneRigSplitResult
        {
            Succeeded = false,
            Error = error,
            RemainingRig = default,
            DetachedRig = default
        };
    }
}

public static class RuneRigOperations
{
    public static RuneRigMergeResult Merge(RuneRigData destinationRig, RuneRigData sourceRig, int destinationParentIndex, byte destinationBayIndex, bool lockNewConnection)
    {
        RuneRigValidationResult destinationValidation = RuneRigValidator.Validate(destinationRig, RuneRigRootMode.LooseRig);
        if (!destinationValidation.IsValid)
            return RuneRigMergeResult.Failure($"Destination rig is invalid: {destinationValidation}");

        RuneRigValidationResult sourceValidation = RuneRigValidator.Validate(sourceRig, RuneRigRootMode.LooseRig);
        if (!sourceValidation.IsValid)
            return RuneRigMergeResult.Failure($"Source rig is invalid: {sourceValidation}");

        if (destinationParentIndex < 0 || destinationParentIndex >= destinationRig.NodeCount)
            return RuneRigMergeResult.Failure("Destination parent index is outside the destination rig.");

        RuneNodeData destinationParent = destinationRig.Nodes[destinationParentIndex];

        if (destinationBayIndex >= destinationParent.BayCapacity)
            return RuneRigMergeResult.Failure($"Destination bay {destinationBayIndex} is outside capacity {destinationParent.BayCapacity}.");

        if (IsBayOccupied(destinationRig, destinationParentIndex, destinationBayIndex))
            return RuneRigMergeResult.Failure($"Destination bay {destinationBayIndex} is already occupied.");

        int combinedNodeCount = destinationRig.NodeCount + sourceRig.NodeCount;

        if (combinedNodeCount > RuneRigLimits.MaxNodes)
            return RuneRigMergeResult.Failure($"Combined rig would exceed the {RuneRigLimits.MaxNodes} node limit.");

        RuneNodeData[] combinedNodes = new RuneNodeData[combinedNodeCount];
        Array.Copy(destinationRig.Nodes, combinedNodes, destinationRig.NodeCount);

        int sourceIndexOffset = destinationRig.NodeCount;

        for (int sourceIndex = 0; sourceIndex < sourceRig.NodeCount; sourceIndex++)
        {
            RuneNodeData sourceNode = sourceRig.Nodes[sourceIndex];

            if (sourceIndex == 0)
            {
                sourceNode.ParentNodeIndex = (byte)destinationParentIndex;
                sourceNode.ParentBayConnection = RuneConnection.Pack(destinationBayIndex, lockNewConnection);
            }
            else
            {
                sourceNode.ParentNodeIndex = (byte)(sourceNode.ParentNodeIndex + sourceIndexOffset);
            }

            combinedNodes[sourceIndexOffset + sourceIndex] = sourceNode;
        }

        RuneRigData combinedRig = new RuneRigData(combinedNodes);
        RuneRigValidationResult combinedValidation = RuneRigValidator.Validate(combinedRig, RuneRigRootMode.LooseRig);

        if (!combinedValidation.IsValid)
            return RuneRigMergeResult.Failure($"Combined rig is invalid: {combinedValidation}");

        return RuneRigMergeResult.Success(combinedRig);
    }

    public static RuneRigSplitResult Split(RuneRigData sourceRig, int subtreeRootIndex)
    {
        RuneRigValidationResult sourceValidation = RuneRigValidator.Validate(sourceRig, RuneRigRootMode.LooseRig);
        if (!sourceValidation.IsValid)
            return RuneRigSplitResult.Failure($"Source rig is invalid: {sourceValidation}");

        if (subtreeRootIndex <= 0 || subtreeRootIndex >= sourceRig.NodeCount)
            return RuneRigSplitResult.Failure("The selected node must be a non-root node inside the rig.");

        RuneNodeData subtreeRoot = sourceRig.Nodes[subtreeRootIndex];

        if (subtreeRoot.ConnectionIsLocked)
            return RuneRigSplitResult.Failure("The selected connection is locked.");

        bool[] detachedMask = FindSubtree(sourceRig, subtreeRootIndex);

        int detachedCount = 0;

        for (int i = 0; i < detachedMask.Length; i++)
        {
            if (detachedMask[i])
                detachedCount++;
        }

        int remainingCount = sourceRig.NodeCount - detachedCount;

        if (detachedCount == 0 || remainingCount == 0)
            return RuneRigSplitResult.Failure("Split did not produce two valid rigs.");

        RuneNodeData[] remainingNodes = new RuneNodeData[remainingCount];
        RuneNodeData[] detachedNodes = new RuneNodeData[detachedCount];

        int[] oldToRemaining = CreateEmptyRemap(sourceRig.NodeCount);
        int[] oldToDetached = CreateEmptyRemap(sourceRig.NodeCount);

        int remainingWriteIndex = 0;
        int detachedWriteIndex = 0;

        for (int oldIndex = 0; oldIndex < sourceRig.NodeCount; oldIndex++)
        {
            if (detachedMask[oldIndex])
            {
                oldToDetached[oldIndex] = detachedWriteIndex;
                detachedNodes[detachedWriteIndex] = sourceRig.Nodes[oldIndex];
                detachedWriteIndex++;
            }
            else
            {
                oldToRemaining[oldIndex] = remainingWriteIndex;
                remainingNodes[remainingWriteIndex] = sourceRig.Nodes[oldIndex];
                remainingWriteIndex++;
            }
        }

        for (int oldIndex = 0; oldIndex < sourceRig.NodeCount; oldIndex++)
        {
            RuneNodeData node = sourceRig.Nodes[oldIndex];

            if (detachedMask[oldIndex])
            {
                int newIndex = oldToDetached[oldIndex];

                if (oldIndex == subtreeRootIndex)
                {
                    node.ParentNodeIndex = RuneParent.None;
                    node.ParentBayConnection = 0;
                }
                else
                {
                    int remappedParent = oldToDetached[node.ParentNodeIndex];

                    if (remappedParent < 0)
                        return RuneRigSplitResult.Failure("A detached node references a parent outside its subtree.");

                    node.ParentNodeIndex = (byte)remappedParent;
                }

                detachedNodes[newIndex] = node;
            }
            else
            {
                int newIndex = oldToRemaining[oldIndex];

                if (oldIndex != 0)
                {
                    int remappedParent = oldToRemaining[node.ParentNodeIndex];

                    if (remappedParent < 0)
                        return RuneRigSplitResult.Failure("A remaining node references a detached parent.");

                    node.ParentNodeIndex = (byte)remappedParent;
                }

                remainingNodes[newIndex] = node;
            }
        }

        RuneRigData remainingRig = new RuneRigData(remainingNodes);
        RuneRigData detachedRig = new RuneRigData(detachedNodes);

        RuneRigValidationResult remainingValidation = RuneRigValidator.Validate(remainingRig, RuneRigRootMode.LooseRig);
        if (!remainingValidation.IsValid)
            return RuneRigSplitResult.Failure($"Remaining rig is invalid: {remainingValidation}");

        RuneRigValidationResult detachedValidation = RuneRigValidator.Validate(detachedRig, RuneRigRootMode.LooseRig);
        if (!detachedValidation.IsValid)
            return RuneRigSplitResult.Failure($"Detached rig is invalid: {detachedValidation}");

        return RuneRigSplitResult.Success(remainingRig, detachedRig);
    }

    private static bool[] FindSubtree(RuneRigData rig, int subtreeRootIndex)
    {
        bool[] subtreeMask = new bool[rig.NodeCount];
        subtreeMask[subtreeRootIndex] = true;

        for (int nodeIndex = subtreeRootIndex + 1; nodeIndex < rig.NodeCount; nodeIndex++)
        {
            byte parentIndex = rig.Nodes[nodeIndex].ParentNodeIndex;

            if (RuneParent.IsNodeIndex(parentIndex) && subtreeMask[parentIndex])
                subtreeMask[nodeIndex] = true;
        }

        return subtreeMask;
    }

    private static bool IsBayOccupied(RuneRigData rig, int parentNodeIndex, byte bayIndex)
    {
        for (int nodeIndex = 1; nodeIndex < rig.NodeCount; nodeIndex++)
        {
            RuneNodeData node = rig.Nodes[nodeIndex];

            if (node.ParentNodeIndex == parentNodeIndex && node.ParentBayIndex == bayIndex)
                return true;
        }

        return false;
    }

    private static int[] CreateEmptyRemap(int count)
    {
        int[] remap = new int[count];

        for (int i = 0; i < remap.Length; i++)
            remap[i] = -1;

        return remap;
    }
}