using Fusion;
using System;
using System.Collections.Generic;
using UnityEngine;

public static class StaticSpellRegistry
{
    private static RuntimeSpell[] _blueprints;
    private static string[] _names;
    private static Dictionary<string, ushort> _nameToID;
    private static bool _isInitialized;
    public const ushort MaxStaticSpellID = 9999;
    public static bool IsInitialized => _isInitialized;

    public static void Initialize(StaticSpellDictionary spellDictionary)
    {
        if (_isInitialized) return;

        if (spellDictionary == null)
        {
            Debug.LogError("[StaticSpellRegistry] StaticSpellDictionary is missing.");
            return;
        }

        if (!NodeRegistry.IsInitialized)
        {
            Debug.LogError("[StaticSpellRegistry] NodeRegistry must be initialized first.");
            return;
        }

        if (spellDictionary.Spells.Count - 1 > MaxStaticSpellID)
        {
            Debug.LogError($"[StaticSpellRegistry] Static spell IDs cannot exceed {MaxStaticSpellID}.");
            return;
        }

        _blueprints = new RuntimeSpell[spellDictionary.Spells.Count];
        _names = new string[spellDictionary.Spells.Count];
        _nameToID = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

        int hydratedCount = 0;

        for (int i = 1; i < spellDictionary.Spells.Count; i++)
        {
            StaticSpellDictionaryEntry entry = spellDictionary.Spells[i];
            if (!entry.IsValid) continue;

            string spellName = string.IsNullOrWhiteSpace(entry.Name) ? entry.JSON.name : entry.Name;

            if (_nameToID.ContainsKey(spellName))
            {
                Debug.LogError($"[StaticSpellRegistry] Duplicate static spell name '{spellName}'.");
                continue;
            }

            if (!TryHydrateEntry(entry, out RuntimeSpell runtimeSpell, out string error))
            {
                Debug.LogError($"[StaticSpellRegistry] Failed to hydrate [{i}] '{spellName}': {error}");
                continue;
            }

            _blueprints[i] = runtimeSpell;
            _names[i] = spellName;
            _nameToID.Add(spellName, (ushort)i);
            hydratedCount++;
        }

        _isInitialized = true;
        Debug.Log($"[StaticSpellRegistry] Hydrated {hydratedCount} permanent static spell blueprints.");
    }

    public static bool TryGetBlueprint(ushort staticSpellID, out RuntimeSpell runtimeSpell)
    {
        runtimeSpell = null;

        if (!_isInitialized || _blueprints == null) return false;
        if (staticSpellID == 0 || staticSpellID >= _blueprints.Length) return false;

        runtimeSpell = _blueprints[staticSpellID];
        return runtimeSpell != null;
    }

    public static bool TryGetBlueprint(SpellGraphId spellID, out RuntimeSpell runtimeSpell)
    {
        runtimeSpell = null;

        if (spellID.AuthorRef != PlayerRef.None) return false;
        if (spellID.BlueprintNumber <= 0 || spellID.BlueprintNumber > ushort.MaxValue) return false;

        return TryGetBlueprint((ushort)spellID.BlueprintNumber, out runtimeSpell);
    }

    public static bool TryGetBlueprint(string spellName, out RuntimeSpell runtimeSpell)
    {
        runtimeSpell = null;

        if (!TryGetID(spellName, out ushort staticSpellID)) return false;

        return TryGetBlueprint(staticSpellID, out runtimeSpell);
    }

    public static bool TryGetID(string spellName, out ushort staticSpellID)
    {
        staticSpellID = 0;

        if (!_isInitialized || string.IsNullOrWhiteSpace(spellName)) return false;

        return _nameToID.TryGetValue(spellName, out staticSpellID);
    }

    public static bool TryGetSpellID(string spellName, out SpellGraphId spellID)
    {
        spellID = default;

        if (!TryGetID(spellName, out ushort staticSpellID)) return false;

        spellID = new SpellGraphId(PlayerRef.None, staticSpellID);
        return true;
    }

    public static string GetName(ushort staticSpellID)
    {
        if (!_isInitialized || _names == null) return null;
        if (staticSpellID == 0 || staticSpellID >= _names.Length) return null;

        return _names[staticSpellID];
    }

    private static bool TryHydrateEntry(StaticSpellDictionaryEntry entry, out RuntimeSpell runtimeSpell, out string error)
    {
        runtimeSpell = null;
        error = null;

        SpellGraph temporaryGraph = ScriptableObject.CreateInstance<SpellGraph>();

        try
        {
            JsonUtility.FromJsonOverwrite(entry.JSON.text, temporaryGraph);
            return SpellBlueprintHydrator.TryHydrate(temporaryGraph, out runtimeSpell, out error);
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
}