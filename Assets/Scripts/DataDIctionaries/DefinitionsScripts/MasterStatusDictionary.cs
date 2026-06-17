using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MasterStatusDictionary", menuName = "SpellSystem/Master Status Dictionary")]
public class MasterStatusDictionary : ScriptableObject
{
    [Tooltip("Do not edit manually! Handled via the Rune Forge dual-publish.")]
    public List<EffectNode> BakedStatuses = new List<EffectNode>() { null };

    private void OnValidate()
    {
        if (BakedStatuses == null) { BakedStatuses = new List<EffectNode>() { null }; return; }
        if (BakedStatuses.Count == 0) { BakedStatuses.Add(null); }
        else if (BakedStatuses[0] != null)
        {
            EffectNode displaced = BakedStatuses[0];
            BakedStatuses.Add(displaced);
            BakedStatuses[0] = null;
        }
    }
}