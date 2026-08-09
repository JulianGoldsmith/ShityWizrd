using Fusion;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public enum SpellBlueprintFormat : byte
{
    LegacyGraph,
    RuneRig
}

public sealed class RuntimeSpell
{
    private readonly IRuntimeNode[] _nodes;

    public SpellBlueprintFormat Format { get; }
    public EntryPointType EntryType { get; }
    public IRuntimeNode RootNode => _nodes[0];
    public int NodeCount => _nodes.Length;

    public RuntimeSpell(SpellBlueprintFormat format, EntryPointType entryType, IRuntimeNode[] nodes)
    {
        if (nodes == null || nodes.Length == 0)
            throw new ArgumentException("A runtime spell must contain at least one node.", nameof(nodes));

        Format = format;
        EntryType = entryType;
        _nodes = (IRuntimeNode[])nodes.Clone();
    }

    public IRuntimeNode GetNode(int nodeIndex)
    {
        if (nodeIndex < 0 || nodeIndex >= _nodes.Length) return null;
        return _nodes[nodeIndex];
    }
}

public static class SpellBlueprintLibrary
{
    public const int MaxStaticSpellID = 9999;

    private static readonly Dictionary<SpellGraphId, RuntimeSpell> _spells = new Dictionary<SpellGraphId, RuntimeSpell>();
    private static readonly HashSet<SpellGraphId> _staticSpellIDs = new HashSet<SpellGraphId>();

    private static bool _staticSpellsInitialized;

    public static void InitializeStatic(StaticSpellDictionary dictionary)
    {
        if (_staticSpellsInitialized) return;

        if (dictionary == null)
        {
            Debug.LogError("[SpellBlueprintLibrary] StaticSpellDictionary is missing.");
            return;
        }

        if (!NodeRegistry.IsInitialized)
        {
            Debug.LogError("[SpellBlueprintLibrary] NodeRegistry must be initialized first.");
            return;
        }

        if (dictionary.Spells.Count - 1 > MaxStaticSpellID)
        {
            Debug.LogError($"[SpellBlueprintLibrary] Static spell IDs cannot exceed {MaxStaticSpellID}.");
            return;
        }

        int hydratedCount = 0;

        for (int i = 1; i < dictionary.Spells.Count; i++)
        {
            StaticSpellDictionaryEntry entry = dictionary.Spells[i];
            if (!entry.IsValid) continue;

            SpellGraphId spellID = new SpellGraphId(PlayerRef.None, i);
            SpellGraph temporaryGraph = ScriptableObject.CreateInstance<SpellGraph>();

            try
            {
                JsonUtility.FromJsonOverwrite(entry.JSON.text, temporaryGraph);

                RuntimeSpell runtimeSpell = HydrateLegacy(temporaryGraph.Data);

                _spells[spellID] = runtimeSpell;
                _staticSpellIDs.Add(spellID);
                hydratedCount++;
            }
            catch (Exception exception)
            {
                string spellName = string.IsNullOrWhiteSpace(entry.Name) ? entry.JSON.name : entry.Name;
                Debug.LogError($"[SpellBlueprintLibrary] Failed to hydrate static spell [{i}] '{spellName}':\n{exception}");
            }
            finally
            {
                if (temporaryGraph != null)
                    UnityEngine.Object.Destroy(temporaryGraph);
            }
        }

        _staticSpellsInitialized = true;
        Debug.Log($"[SpellBlueprintLibrary] Hydrated {hydratedCount} permanent static spells.");
    }

    public static bool Store(SpellGraphId spellID, SpellGraph graph)
    {
        if (spellID.IsNull() || graph == null) return false;

        if (_staticSpellIDs.Contains(spellID))
        {
            Debug.LogError($"[SpellBlueprintLibrary] Cannot overwrite static spell {spellID.BlueprintNumber}.");
            return false;
        }

        try
        {
            _spells[spellID] = HydrateLegacy(graph.Data);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SpellBlueprintLibrary] Failed to hydrate spell {spellID.BlueprintNumber}:\n{exception}");
            return false;
        }
    }

    public static bool Store(SpellGraphId spellID, RuneSpellBlueprintData blueprint)
    {
        if (spellID.IsNull() || blueprint.NodeCount <= 0) return false;

        if (_staticSpellIDs.Contains(spellID))
        {
            Debug.LogError($"[SpellBlueprintLibrary] Cannot overwrite static spell {spellID.BlueprintNumber}.");
            return false;
        }

        try
        {
            _spells[spellID] = HydrateRuneRig(blueprint);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[SpellBlueprintLibrary] Failed to hydrate rune spell {spellID.BlueprintNumber}:\n{exception}");
            return false;
        }
    }

    public static RuntimeSpell Get(SpellGraphId spellID)
    {
        if (spellID.IsNull()) return null;

        _spells.TryGetValue(spellID, out RuntimeSpell runtimeSpell);
        return runtimeSpell;
    }

    public static void RemoveDynamic(SpellGraphId spellID)
    {
        if (spellID.IsNull() || _staticSpellIDs.Contains(spellID)) return;

        _spells.Remove(spellID);
    }

    private static RuntimeSpell HydrateLegacy(SpellNetworkData data)
    {
        if (data.Nodes == null || data.Nodes.Length == 0)
            throw new InvalidOperationException("Legacy spell contains no nodes.");

        if (data.MaxNodeIndex >= data.Nodes.Length)
            throw new InvalidOperationException($"MaxNodeIndex {data.MaxNodeIndex} exceeds the node array.");

        if (data.Wires == null)
            throw new InvalidOperationException("Legacy spell contains no wire array.");

        if (data.MaxWireIndex >= data.Wires.Length)
            throw new InvalidOperationException($"MaxWireIndex {data.MaxWireIndex} exceeds the wire array.");

        IRuntimeNode[] runtimeNodes = new IRuntimeNode[data.MaxNodeIndex + 1];
        SpellCompilationContext context = new SpellCompilationContext
        {
            GraphData = data
        };

        for (int i = 0; i <= data.MaxNodeIndex; i++)
        {
            ushort templateID = data.Nodes[i].TemplateID;
            if (templateID == 0) continue;

            if (!NodeRegistry.TryGetNodeTemplate(templateID, out SpellNode template))
                throw new InvalidOperationException($"Spell node {i} references missing template ID {templateID}.");

            context.CurrentNodeIndex = i;
            runtimeNodes[i] = template.CompileNode(context);

            if (runtimeNodes[i] == null)
                throw new InvalidOperationException($"Spell node {i}, template '{template.nodeName}', compiled to null.");
        }

        for (int i = 0; i <= data.MaxWireIndex; i++)
        {
            WireData wire = data.Wires[i];
            if (wire.FromSocketIndex == 255) continue;

            IRuntimeNode source = runtimeNodes[wire.FromNodeIndex];
            IRuntimeNode target = runtimeNodes[wire.ToNodeIndex];

            if (source == null || target == null) continue;

            if (source is RuntimeEntryPoint entryPoint)
            {
                entryPoint.SetConnection(target);
            }
            else if (source is IBehaviour behaviour && target is IRuntimeCore behaviourCore)
            {
                behaviourCore.AddBehaviour(behaviour);
            }
            else if (source is IRuntimeCore triggerCore && target is ITrigger trigger)
            {
                triggerCore.AddTrigger(trigger);
            }
            else if (source is ITrigger outcomeTrigger)
            {
                outcomeTrigger.AddOutcome(target);
            }
            else if (source is IRuntimeValueNode valueNode)
            {
                InjectValueNode(data, wire, target, valueNode);
            }
        }

        return BuildRuntimeSpell(SpellBlueprintFormat.LegacyGraph, runtimeNodes);
    }

    private static RuntimeSpell HydrateRuneRig(RuneSpellBlueprintData blueprint)
    {
        RuneRigData blueprintRig = new RuneRigData(blueprint.CreateNodeCopy());
        RuneRigValidationResult validation = RuneRigValidator.Validate(blueprintRig, RuneRigRootMode.Blueprint);

        if (!validation.IsValid)
            throw new InvalidOperationException($"Cannot hydrate invalid rune spell: {validation}");

        int nodeCount = blueprint.NodeCount;
        SpellNode[] definitions = new SpellNode[nodeCount];
        IRuntimeNode[] runtimeNodes = new IRuntimeNode[nodeCount];
        List<SpellNode>[] downstreamDefinitions = new List<SpellNode>[nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            ushort definitionID = blueprint.GetNode(i).RuneDefinitionId;

            if (!NodeRegistry.TryGetNodeTemplate(definitionID, out definitions[i]))
                throw new InvalidOperationException($"Rune definition {definitionID} is not registered.");

            downstreamDefinitions[i] = new List<SpellNode>();
        }

        for (int i = 1; i < nodeCount; i++)
        {
            RuneNodeData node = blueprint.GetNode(i);
            downstreamDefinitions[node.ParentNodeIndex].Add(definitions[i]);
        }

        SpellCompilationContext context = new SpellCompilationContext
        {
            DownstreamNodeDefinitions = downstreamDefinitions
        };

        for (int i = 0; i < nodeCount; i++)
        {
            context.CurrentNodeIndex = i;
            runtimeNodes[i] = definitions[i].CompileNode(context);

            if (runtimeNodes[i] == null)
                throw new InvalidOperationException($"Rune '{definitions[i].nodeName}' compiled to null.");
        }

        for (int i = 1; i < nodeCount; i++)
        {
            RuneNodeData node = blueprint.GetNode(i);
            IRuntimeNode parent = runtimeNodes[node.ParentNodeIndex];
            IRuntimeNode child = runtimeNodes[i];

            if (parent is IRuntimeCore behaviourCore && child is IBehaviour behaviour)
            {
                behaviourCore.AddBehaviour(behaviour);
            }
            else if (parent is IRuntimeCore triggerCore && child is ITrigger trigger)
            {
                triggerCore.AddTrigger(trigger);
            }
            else if (parent is ITrigger outcomeTrigger && (child is IEffect || child is IRuntimeCore))
            {
                outcomeTrigger.AddOutcome(child);
            }
            else
            {
                throw new InvalidOperationException($"Cannot connect '{definitions[i].nodeName}' to '{definitions[node.ParentNodeIndex].nodeName}'.");
            }
        }

        return BuildRuntimeSpell(SpellBlueprintFormat.RuneRig, runtimeNodes);
    }

    private static RuntimeSpell BuildRuntimeSpell(SpellBlueprintFormat format, IRuntimeNode[] runtimeNodes)
    {
        if (runtimeNodes == null || runtimeNodes.Length == 0 || runtimeNodes[0] == null) throw new InvalidOperationException("Hydration produced no root node.");

        IRuntimeNode rootNode = runtimeNodes[0];
        EntryPointType entryType;

        if (rootNode is RuntimeEntryPoint entryPoint)
        {
            if (entryPoint.ConnectedLogic is IRuntimeCore) entryType = EntryPointType.SpawnCore;
            else if (entryPoint.ConnectedLogic is ITrigger) entryType = EntryPointType.Trigger;
            else if (entryPoint.ConnectedLogic is IEffect) entryType = EntryPointType.Effect;
            else throw new InvalidOperationException("Legacy spell entry point has no valid connected node.");

            entryPoint.ExpectedType = entryType;
        }
        else if (rootNode is IRuntimeCore)
        {
            entryType = EntryPointType.SpawnCore;
        }
        else if (rootNode is ITrigger)
        {
            entryType = EntryPointType.Trigger;
        }
        else if (rootNode is IEffect)
        {
            entryType = EntryPointType.Effect;
        }
        else
        {
            throw new InvalidOperationException($"Runtime root '{rootNode.GetType().Name}' is not a valid spell entry.");
        }

        return new RuntimeSpell(format, entryType, runtimeNodes);
    }

    private static void InjectValueNode(SpellNetworkData data, WireData wire, IRuntimeNode target, IRuntimeValueNode valueNode)
    {
        ushort targetTemplateID = data.Nodes[wire.ToNodeIndex].TemplateID;

        if (!NodeRegistry.TryGetNodeTemplate(targetTemplateID, out SpellNode targetTemplate))
            return;

        List<SocketDefinition> sockets = targetTemplate.GetSockets();
        if (wire.ToSocketIndex >= sockets.Count) return;

        string fieldName = sockets[wire.ToSocketIndex].TargetFieldName;
        if (string.IsNullOrEmpty(fieldName)) return;

        bool TryInject(object targetObject)
        {
            FieldInfo field = targetObject.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null) return false;

            if (field.GetValue(targetObject) is not IRuntimeDataProperty dataProperty)
                return false;

            dataProperty.AddValueNode(valueNode);
            return true;
        }

        if (TryInject(target)) return;
        if (target is not RuntimeCoreBase runtimeCore) return;

        foreach (IBehaviour behaviour in runtimeCore.Behaviours)
        {
            if (TryInject(behaviour)) return;
        }

        foreach (ITrigger trigger in runtimeCore.Triggers)
        {
            if (TryInject(trigger)) return;
        }

        Debug.LogWarning($"[SpellBlueprintLibrary] Could not inject value node into '{fieldName}' on {target.GetType().Name}.");
    }
}