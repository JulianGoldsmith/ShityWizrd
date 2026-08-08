using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct StaticSpellDictionaryEntry
{
    public string Name;
    public TextAsset JSON;

    public bool IsValid => JSON != null;
}

[CreateAssetMenu(fileName = "StaticSpellDictionary", menuName = "Dictionaries/Static Spell Dictionary")]
public class StaticSpellDictionary : ScriptableObject
{
    [Tooltip("Index zero is reserved. The list index is the static spell ID.")]
    public List<StaticSpellDictionaryEntry> Spells = new List<StaticSpellDictionaryEntry>() { default };

    private void OnValidate()
    {
        if (Spells == null)
        {
            Spells = new List<StaticSpellDictionaryEntry>() { default };
            return;
        }

        if (Spells.Count == 0)
        {
            Spells.Add(default);
            return;
        }

        if (Spells[0].IsValid || !string.IsNullOrWhiteSpace(Spells[0].Name))
        {
            StaticSpellDictionaryEntry displaced = Spells[0];
            Spells[0] = default;
            Spells.Add(displaced);

            Debug.LogWarning($"[StaticSpellDictionary] Index zero is reserved. Moved '{displaced.Name}' to ID {Spells.Count - 1}.", this);
        }

        for (int i = 1; i < Spells.Count; i++)
        {
            StaticSpellDictionaryEntry entry = Spells[i];

            if (entry.JSON != null && string.IsNullOrWhiteSpace(entry.Name))
            {
                entry.Name = entry.JSON.name;
                Spells[i] = entry;
            }
        }
    }
}