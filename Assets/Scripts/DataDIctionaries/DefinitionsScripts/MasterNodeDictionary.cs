using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MasterNodeDictionary", menuName = "Dictionary/Master Node Dictionary")]
public class MasterNodeDictionary : ScriptableObject
{
    [Tooltip("Do not edit manually! Use the UI buttons on the nodes.")]
    public List<SpellNode> BakedNodes = new List<SpellNode>() { null };

    private void OnValidate()
    {
        if (BakedNodes == null) { BakedNodes = new List<SpellNode>() { null }; return; }
        if (BakedNodes.Count == 0) { BakedNodes.Add(null); }
        else if (BakedNodes[0] != null)
        {
            SpellNode displaced = BakedNodes[0];
            BakedNodes.Add(displaced);
            BakedNodes[0] = null;
            Debug.LogWarning($"[MasterNodeDictionary] Auto-Correction: Index 0 is reserved empty. Moved {displaced.name}.");
        }
    }
}