using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "StaticSpellDatabase", menuName = "SpellNodes/Static Spell Database")]
public class StaticSpellDatabase : ScriptableObject
{
    [Tooltip("For Static Spells. The index (0, 1, 2) refers to the AI Actions or where ever else we call them from")]
    public List<TextAsset> staticSpells = new List<TextAsset>();
}