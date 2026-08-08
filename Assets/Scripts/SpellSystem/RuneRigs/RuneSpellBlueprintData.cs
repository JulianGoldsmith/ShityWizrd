using System;
using UnityEngine;

[Serializable]
public struct RuneSpellBlueprintData
{
    [SerializeField] private RuneNodeData[] _nodes;

    public int NodeCount => _nodes == null ? 0 : _nodes.Length;

    public RuneSpellBlueprintData(RuneNodeData[] nodes)
    {
        _nodes = CopyNodes(nodes);
    }

    public RuneNodeData GetNode(int nodeIndex)
    {
        return _nodes[nodeIndex];
    }

    public RuneNodeData[] CreateNodeCopy()
    {
        return CopyNodes(_nodes);
    }

    private static RuneNodeData[] CopyNodes(RuneNodeData[] source)
    {
        if (source == null)
            return null;

        RuneNodeData[] copy = new RuneNodeData[source.Length];
        Array.Copy(source, copy, source.Length);
        return copy;
    }
}

public static class RuneSpellBlueprintBuilder
{
    public static bool TryCreateBlueprint(RuneRigData looseRig, out RuneSpellBlueprintData blueprint, out string error)
    {
        blueprint = default;

        RuneRigValidationResult looseValidation = RuneRigValidator.Validate(looseRig, RuneRigRootMode.LooseRig);

        if (!looseValidation.IsValid)
        {
            error = $"Loose rig is invalid: {looseValidation}";
            return false;
        }

        RuneNodeData[] blueprintNodes = new RuneNodeData[looseRig.NodeCount];
        Array.Copy(looseRig.Nodes, blueprintNodes, looseRig.NodeCount);

        RuneNodeData root = blueprintNodes[0];
        root.ParentNodeIndex = RuneParent.Root;
        root.ParentBayConnection = 0;
        blueprintNodes[0] = root;

        RuneRigData blueprintRig = new RuneRigData(blueprintNodes);
        RuneRigValidationResult blueprintValidation = RuneRigValidator.Validate(blueprintRig, RuneRigRootMode.Blueprint);

        if (!blueprintValidation.IsValid)
        {
            error = $"Blueprint rig is invalid: {blueprintValidation}";
            return false;
        }

        blueprint = new RuneSpellBlueprintData(blueprintNodes);
        error = null;
        return true;
    }

    public static bool TryCreateLooseRig(RuneSpellBlueprintData blueprint, out RuneRigData looseRig, out string error)
    {
        looseRig = default;

        RuneNodeData[] looseNodes = blueprint.CreateNodeCopy();
        RuneRigData blueprintRig = new RuneRigData(looseNodes);
        RuneRigValidationResult blueprintValidation = RuneRigValidator.Validate(blueprintRig, RuneRigRootMode.Blueprint);

        if (!blueprintValidation.IsValid)
        {
            error = $"Blueprint rig is invalid: {blueprintValidation}";
            return false;
        }

        RuneNodeData root = looseNodes[0];
        root.ParentNodeIndex = RuneParent.None;
        root.ParentBayConnection = 0;
        looseNodes[0] = root;

        looseRig = new RuneRigData(looseNodes);
        RuneRigValidationResult looseValidation = RuneRigValidator.Validate(looseRig, RuneRigRootMode.LooseRig);

        if (!looseValidation.IsValid)
        {
            error = $"Reconstructed loose rig is invalid: {looseValidation}";
            looseRig = default;
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryGetEntryPointType(RuneSpellBlueprintData blueprint, out EntryPointType entryPointType, out string error)
    {
        entryPointType = default;

        if (blueprint.NodeCount <= 0)
        {
            error = "The rune blueprint has no root node.";
            return false;
        }

        RuneNodeData rootNode = blueprint.GetNode(0);

        if (!NodeRegistry.TryGetNodeTemplate(rootNode.RuneDefinitionId, out SpellNode rootTemplate))
        {
            error = $"Root rune definition {rootNode.RuneDefinitionId} is not registered.";
            return false;
        }

        switch (rootTemplate.GetRuneType())
        {
            case NodeType.Core:
                entryPointType = EntryPointType.SpawnCore;
                error = null;
                return true;

            case NodeType.Trigger:
                entryPointType = EntryPointType.Trigger;
                error = null;
                return true;

            case NodeType.Effect:
                entryPointType = EntryPointType.Effect;
                error = null;
                return true;

            default:
                error = $"A {rootTemplate.GetRuneType()} rune cannot be used as a spell entry point.";
                return false;
        }
    }
}