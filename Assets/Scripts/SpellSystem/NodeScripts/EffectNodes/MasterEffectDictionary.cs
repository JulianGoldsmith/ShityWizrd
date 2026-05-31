using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MasterEffectDictionary", menuName = "SpellSystem/Master Effect Dictionary")]
public class MasterEffectDictionary : ScriptableObject
{
    private static MasterEffectDictionary _instance;
    public static MasterEffectDictionary Instance
    {
        get
        {
            if (_instance == null) _instance = Resources.Load<MasterEffectDictionary>("MasterEffectDictionary");
            return _instance;
        }
    }

    [Tooltip("Do not edit manually! Use the Publisher UI on the nodes.")]
    public List<GenericEffectNode> BakedEffects = new List<GenericEffectNode>();

    private void OnValidate()
    {
        if (BakedEffects == null)
        {
            BakedEffects = new List<GenericEffectNode>() { null };
            return;
        }

        if (BakedEffects.Count == 0)
        {
            BakedEffects.Add(null);
        }
        else if (BakedEffects[0] != null)
        {
            GenericEffectNode displacedNode = BakedEffects[0];

            BakedEffects.Add(displacedNode);
            BakedEffects[0] = null;

            Debug.LogWarning($"[MasterEffectDictionary] Auto-Correction: Index 0 is reserved. Moved {displacedNode.name} to Index {BakedEffects.Count - 1}.");
        }
    }
}