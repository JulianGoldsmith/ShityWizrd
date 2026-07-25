using System;
using System.Collections.Generic;

[Serializable]
public struct RuneRigValidationResult
{
    public bool IsValid;
    public int NodeIndex;
    public string Message;

    public static RuneRigValidationResult Success()
    {
        return new RuneRigValidationResult
        {
            IsValid = true,
            NodeIndex = -1,
            Message = "Rune rig is valid."
        };
    }

    public static RuneRigValidationResult Failure(int nodeIndex, string message)
    {
        return new RuneRigValidationResult
        {
            IsValid = false,
            NodeIndex = nodeIndex,
            Message = message
        };
    }

    public override string ToString()
    {
        return IsValid ? Message : $"Node {NodeIndex}: {Message}";
    }
}

public static class RuneRigValidator
{
    public static RuneRigValidationResult Validate(RuneRigData rig, RuneRigRootMode rootMode)
    {
        RuneRigValidationResult structureResult = ValidateStructure(rig, rootMode);
        if (!structureResult.IsValid)
            return structureResult;

        return ValidateDefinitions(rig);
    }

    public static RuneRigValidationResult ValidateStructure(RuneRigData rig, RuneRigRootMode rootMode)
    {
        if (rig.Nodes == null)
            return RuneRigValidationResult.Failure(-1, "Node array is null.");

        if (rig.Nodes.Length == 0)
            return RuneRigValidationResult.Failure(-1, "A rig must contain at least one rune.");

        if (rig.Nodes.Length > RuneRigLimits.MaxNodes)
            return RuneRigValidationResult.Failure(-1, $"A rig cannot contain more than {RuneRigLimits.MaxNodes} runes.");

        byte requiredRootParent = rootMode == RuneRigRootMode.LooseRig ? RuneParent.None : RuneParent.Root;
        RuneNodeData root = rig.Nodes[0];

        if (root.ParentNodeIndex != requiredRootParent)
            return RuneRigValidationResult.Failure(0, $"Root parent must be {requiredRootParent}.");

        if (root.ParentBayConnection != 0)
            return RuneRigValidationResult.Failure(0, "The root cannot have a parent bay or connection flags.");

        HashSet<int> occupiedBays = new HashSet<int>();

        for (int i = 0; i < rig.Nodes.Length; i++)
        {
            RuneNodeData node = rig.Nodes[i];

            if (node.RuneDefinitionId == 0)
                return RuneRigValidationResult.Failure(i, "Rune definition ID zero is reserved.");

            if (node.BayCapacity > RuneRigLimits.MaxBayCapacity)
                return RuneRigValidationResult.Failure(i, $"Bay capacity cannot exceed {RuneRigLimits.MaxBayCapacity}.");

            if (RuneConnection.HasReservedFlag(node.ParentBayConnection))
                return RuneRigValidationResult.Failure(i, "The reserved connection flag is set.");

            if (i == 0)
                continue;

            if (!RuneParent.IsNodeIndex(node.ParentNodeIndex))
                return RuneRigValidationResult.Failure(i, "Only the rig root may use a parent sentinel.");

            if (node.ParentNodeIndex >= i)
                return RuneRigValidationResult.Failure(i, "Parent nodes must appear before their children.");

            RuneNodeData parent = rig.Nodes[node.ParentNodeIndex];

            if (node.ParentBayIndex >= parent.BayCapacity)
                return RuneRigValidationResult.Failure(i, $"Parent bay {node.ParentBayIndex} is outside parent capacity {parent.BayCapacity}.");

            int occupiedBayKey = (node.ParentNodeIndex << 8) | node.ParentBayIndex;

            if (!occupiedBays.Add(occupiedBayKey))
                return RuneRigValidationResult.Failure(i, $"Parent bay {node.ParentBayIndex} is already occupied.");
        }

        return RuneRigValidationResult.Success();
    }

    public static RuneRigValidationResult ValidateDefinitions(RuneRigData rig)
    {
        if (!NodeRegistry.IsInitialized)
            return RuneRigValidationResult.Failure(-1, "NodeRegistry has not been initialized.");

        SpellNode[] definitions = new SpellNode[rig.Nodes.Length];

        for (int i = 0; i < rig.Nodes.Length; i++)
        {
            RuneNodeData node = rig.Nodes[i];

            if (!NodeRegistry.TryGetNodeTemplate(node.RuneDefinitionId, out SpellNode definition))
                return RuneRigValidationResult.Failure(i, $"Rune definition {node.RuneDefinitionId} is not registered.");

            if (definition.PhysicalRune == null)
                return RuneRigValidationResult.Failure(i, $"Rune definition '{definition.nodeName}' has no physical settings.");

            if (!definition.PhysicalRune.IsCapacityAllowed(node.BayCapacity))
                return RuneRigValidationResult.Failure(i, $"Capacity {node.BayCapacity} is not allowed by '{definition.nodeName}'.");

            definitions[i] = definition;
        }

        for (int i = 1; i < rig.Nodes.Length; i++)
        {
            RuneNodeData childNode = rig.Nodes[i];
            SpellNode childDefinition = definitions[i];
            SpellNode parentDefinition = definitions[childNode.ParentNodeIndex];

            if (!parentDefinition.PhysicalRune.AcceptsChild(childDefinition.GetRuneType()))
                return RuneRigValidationResult.Failure(i, $"'{parentDefinition.nodeName}' does not accept {childDefinition.GetRuneType()} runes.");
        }

        return RuneRigValidationResult.Success();
    }
}