using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StaticSpellBlueprint
{
    private readonly IRuntimeNode[] _hydratedNodes;

    public ushort ID { get; }
    public string Name { get; }
    public SpellGraphId SpellID { get; }
    public EntryPointType EntryType { get; }
    public IRuntimeNode RootNode => _hydratedNodes[0];
    public int NodeCount => _hydratedNodes.Length;

    public StaticSpellBlueprint(ushort id, string name, EntryPointType entryType, IRuntimeNode[] hydratedNodes)
    {
        ID = id;
        Name = name;
        SpellID = new SpellGraphId(PlayerRef.None, id);
        EntryType = entryType;
        _hydratedNodes = hydratedNodes;
    }

    public bool TryGetNode(int nodeIndex, out IRuntimeNode node)
    {
        node = null;

        if (nodeIndex < 0 || nodeIndex >= _hydratedNodes.Length) return false;

        node = _hydratedNodes[nodeIndex];
        return node != null;
    }
}

public static class StaticSpellRegistry
{
    private static StaticSpellBlueprint[] _blueprints;
    private static Dictionary<string, ushort> _nameToID;
    private static bool _isInitialized;

    public static bool IsInitialized => _isInitialized;

    public static void Initialize(StaticSpellDictionary spellDictionary, MasterNodeDictionary nodeDictionary)
    {
        if (_isInitialized) return;

        if (spellDictionary == null)
        {
            Debug.LogError("[StaticSpellRegistry] StaticSpellDictionary is missing.");
            return;
        }

        if (nodeDictionary == null)
        {
            Debug.LogError("[StaticSpellRegistry] MasterNodeDictionary is missing.");
            return;
        }

        if (!NodeRegistry.IsInitialized)
        {
            Debug.LogError("[StaticSpellRegistry] NodeRegistry must be initialized first.");
            return;
        }

        if (spellDictionary.Spells.Count > ushort.MaxValue)
        {
            Debug.LogError("[StaticSpellRegistry] The dictionary exceeds the ushort ID limit.");
            return;
        }

        _blueprints = new StaticSpellBlueprint[spellDictionary.Spells.Count];
        _nameToID = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

        int hydratedCount = 0;

        for (int i = 1; i < spellDictionary.Spells.Count; i++)
        {
            StaticSpellDictionaryEntry entry = spellDictionary.Spells[i];
            if (!entry.IsValid) continue;

            if (!TryHydrateEntry((ushort)i, entry, nodeDictionary, out StaticSpellBlueprint blueprint, out string error))
            {
                Debug.LogError($"[StaticSpellRegistry] Failed to hydrate static spell [{i}] '{entry.Name}': {error}");
                continue;
            }

            if (_nameToID.ContainsKey(blueprint.Name))
            {
                Debug.LogError($"[StaticSpellRegistry] Duplicate static spell name '{blueprint.Name}'. Names must be unique.");
                continue;
            }

            _blueprints[i] = blueprint;
            _nameToID.Add(blueprint.Name, blueprint.ID);
            hydratedCount++;
        }

        _isInitialized = true;
        Debug.Log($"[StaticSpellRegistry] Hydrated {hydratedCount} permanent static spell blueprints.");
    }

    public static bool TryGetBlueprint(ushort staticSpellID, out StaticSpellBlueprint blueprint)
    {
        blueprint = null;

        if (!_isInitialized || _blueprints == null) return false;
        if (staticSpellID == 0 || staticSpellID >= _blueprints.Length) return false;

        blueprint = _blueprints[staticSpellID];
        return blueprint != null;
    }

    public static bool TryGetBlueprint(SpellGraphId spellID, out StaticSpellBlueprint blueprint)
    {
        blueprint = null;

        if (spellID.AuthorRef != PlayerRef.None) return false;
        if (spellID.BlueprintNumber <= 0 || spellID.BlueprintNumber > ushort.MaxValue) return false;

        return TryGetBlueprint((ushort)spellID.BlueprintNumber, out blueprint);
    }

    public static bool TryGetBlueprint(string spellName, out StaticSpellBlueprint blueprint)
    {
        blueprint = null;

        if (!_isInitialized || string.IsNullOrWhiteSpace(spellName)) return false;
        if (!_nameToID.TryGetValue(spellName, out ushort staticSpellID)) return false;

        return TryGetBlueprint(staticSpellID, out blueprint);
    }

    public static bool TryGetID(string spellName, out ushort staticSpellID)
    {
        staticSpellID = 0;

        if (!_isInitialized || string.IsNullOrWhiteSpace(spellName)) return false;

        return _nameToID.TryGetValue(spellName, out staticSpellID);
    }

    private static bool TryHydrateEntry(ushort staticSpellID, StaticSpellDictionaryEntry entry, MasterNodeDictionary nodeDictionary, out StaticSpellBlueprint blueprint, out string error)
    {
        blueprint = null;
        error = null;

        SpellGraph temporaryGraph = ScriptableObject.CreateInstance<SpellGraph>();

        try
        {
            JsonUtility.FromJsonOverwrite(entry.JSON.text, temporaryGraph);

            if (temporaryGraph.Data.Nodes == null || temporaryGraph.Data.Nodes.Length == 0)
            {
                error = "JSON contains no spell nodes.";
                return false;
            }

            SpellCompilationContext context = new SpellCompilationContext();

            IRuntimeNode[] hydratedNodes = SpellHydrator.HydrateFullGraph(
                temporaryGraph.Data,
                nodeDictionary.BakedNodes,
                context
            );

            if (hydratedNodes == null || hydratedNodes.Length == 0 || hydratedNodes[0] == null)
            {
                error = "Hydration produced no root node.";
                return false;
            }

            if (!TryGetEntryType(hydratedNodes[0], out EntryPointType entryType))
            {
                error = $"Root node '{hydratedNodes[0].GetType().Name}' is not a valid spell entry.";
                return false;
            }

            string spellName = string.IsNullOrWhiteSpace(entry.Name) ? entry.JSON.name : entry.Name;
            blueprint = new StaticSpellBlueprint(staticSpellID, spellName, entryType, hydratedNodes);
            return true;
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }
        finally
        {
            if (temporaryGraph != null)
                UnityEngine.Object.Destroy(temporaryGraph);
        }
    }

    private static bool TryGetEntryType(IRuntimeNode rootNode, out EntryPointType entryType)
    {
        entryType = default;

        if (rootNode is RuntimeEntryPoint entryPoint)
        {
            entryType = entryPoint.ExpectedType;
            return true;
        }

        if (rootNode is IRuntimeCore)
        {
            entryType = EntryPointType.SpawnCore;
            return true;
        }

        if (rootNode is ITrigger)
        {
            entryType = EntryPointType.Trigger;
            return true;
        }

        if (rootNode is IEffect)
        {
            entryType = EntryPointType.Effect;
            return true;
        }

        return false;
    }
}