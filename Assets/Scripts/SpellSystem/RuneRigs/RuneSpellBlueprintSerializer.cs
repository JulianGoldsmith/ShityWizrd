using System;
using System.Collections.Generic;
using System.IO;

public static class RuneSpellHydrator
{
    public static IRuntimeNode[] Hydrate(RuneSpellBlueprintData blueprint)
    {
        RuneRigData blueprintRig = new RuneRigData(blueprint.CreateNodeCopy());
        RuneRigValidationResult validation = RuneRigValidator.Validate(blueprintRig, RuneRigRootMode.Blueprint);

        if (!validation.IsValid) throw new InvalidOperationException($"Cannot hydrate invalid rune spell: {validation}");

        int nodeCount = blueprint.NodeCount;
        SpellNode[] definitions = new SpellNode[nodeCount];
        IRuntimeNode[] runtimeNodes = new IRuntimeNode[nodeCount];
        List<SpellNode>[] downstreamDefinitions = new List<SpellNode>[nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            if (!NodeRegistry.TryGetNodeTemplate(blueprint.GetNode(i).RuneDefinitionId, out definitions[i])) throw new InvalidOperationException($"Rune definition {blueprint.GetNode(i).RuneDefinitionId} is not registered.");
            downstreamDefinitions[i] = new List<SpellNode>();
        }

        for (int i = 1; i < nodeCount; i++)
        {
            RuneNodeData node = blueprint.GetNode(i);
            downstreamDefinitions[node.ParentNodeIndex].Add(definitions[i]);
        }

        SpellCompilationContext context = new SpellCompilationContext { DownstreamNodeDefinitions = downstreamDefinitions };

        for (int i = 0; i < nodeCount; i++)
        {
            context.CurrentNodeIndex = i;
            runtimeNodes[i] = definitions[i].CompileNode(context);

            if (runtimeNodes[i] == null) throw new InvalidOperationException($"Rune '{definitions[i].nodeName}' returned a null runtime node.");
        }

        for (int i = 1; i < nodeCount; i++)
        {
            RuneNodeData node = blueprint.GetNode(i);
            IRuntimeNode parent = runtimeNodes[node.ParentNodeIndex];
            IRuntimeNode child = runtimeNodes[i];

            if (parent is IRuntimeCore behaviourCore && child is IBehaviour behaviour) behaviourCore.AddBehaviour(behaviour);
            else if (parent is IRuntimeCore triggerCore && child is ITrigger trigger) triggerCore.AddTrigger(trigger);
            else if (parent is ITrigger outcomeTrigger && (child is IEffect || child is IRuntimeCore)) outcomeTrigger.AddOutcome(child);
            else if (parent is RuntimeLink runtimeLink && child is RuntimeLinkLaw linkLaw) runtimeLink.Law = linkLaw;
            else throw new InvalidOperationException($"Cannot connect '{definitions[i].nodeName}' to parent '{definitions[node.ParentNodeIndex].nodeName}'.");
        }

        if (runtimeNodes[0] is RuntimeLink link && link.Law == null) throw new InvalidOperationException("A Link Knot must have a Link Law attached.");

        return runtimeNodes;
    }
}

public static class RuneSpellBlueprintSerializer
{
    private const byte FormatVersion = 1;
    private const int HeaderSize = 2;
    private const int NodeSize = 5;

    public static byte[] Serialize(RuneSpellBlueprintData blueprint)
    {
        RuneRigData blueprintRig = new RuneRigData(blueprint.CreateNodeCopy());
        RuneRigValidationResult validation = RuneRigValidator.Validate(blueprintRig, RuneRigRootMode.Blueprint);

        if (!validation.IsValid)
            throw new InvalidDataException($"Cannot serialize invalid rune blueprint: {validation}");

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(FormatVersion);
            writer.Write((byte)blueprint.NodeCount);

            for (int i = 0; i < blueprint.NodeCount; i++)
            {
                RuneNodeData node = blueprint.GetNode(i);
                writer.Write(node.RuneDefinitionId);
                writer.Write(node.ParentNodeIndex);
                writer.Write(node.ParentBayConnection);
                writer.Write(node.BayCapacity);
            }

            return stream.ToArray();
        }
    }

    public static RuneSpellBlueprintData Deserialize(byte[] data)
    {
        if (data == null || data.Length < HeaderSize)
            throw new InvalidDataException("Rune blueprint packet is empty or incomplete.");

        using (MemoryStream stream = new MemoryStream(data))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            byte version = reader.ReadByte();

            if (version != FormatVersion)
                throw new InvalidDataException($"Unsupported rune blueprint version {version}. Expected {FormatVersion}.");

            byte nodeCount = reader.ReadByte();

            if (nodeCount == 0 || nodeCount > RuneRigLimits.MaxNodes)
                throw new InvalidDataException($"Invalid rune blueprint node count {nodeCount}.");

            int expectedLength = HeaderSize + nodeCount * NodeSize;

            if (data.Length != expectedLength)
                throw new InvalidDataException($"Rune blueprint packet length is {data.Length}, expected {expectedLength}.");

            RuneNodeData[] nodes = new RuneNodeData[nodeCount];

            for (int i = 0; i < nodeCount; i++)
            {
                nodes[i] = new RuneNodeData(
                    reader.ReadUInt16(),
                    reader.ReadByte(),
                    reader.ReadByte(),
                    reader.ReadByte());
            }

            RuneRigData blueprintRig = new RuneRigData(nodes);
            RuneRigValidationResult validation = RuneRigValidator.Validate(blueprintRig, RuneRigRootMode.Blueprint);

            if (!validation.IsValid)
                throw new InvalidDataException($"Received invalid rune blueprint: {validation}");

            return new RuneSpellBlueprintData(nodes);
        }
    }
}