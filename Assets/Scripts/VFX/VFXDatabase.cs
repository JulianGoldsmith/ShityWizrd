using UnityEngine;
using System.Collections.Generic;
using System.Linq; 

[CreateAssetMenu(fileName = "VFXDatabase", menuName = "Spell System/VFXDatabase")]
public class VFXDatabase : ScriptableObject
{
    [Tooltip("The ultimate fallback VFX if nothing else is found.")]
    public GameObject globalDefaultVFX;

    [SerializeField]
    private List<ModifierVFXSet> vfxSets;

    private Dictionary<ModifierType, Dictionary<VFXContext, GameObject>> _vfxDictionary;

    private void OnEnable()
    {
        _vfxDictionary = new Dictionary<ModifierType, Dictionary<VFXContext, GameObject>>();
        foreach (var vfxSet in vfxSets)
        {
            var contextDict = new Dictionary<VFXContext, GameObject>();
            foreach (var mapping in vfxSet.contextualVFXs)
            {
                contextDict[mapping.context] = mapping.vfxPrefab;
            }
            _vfxDictionary[vfxSet.modifierType] = contextDict;
        }
    }


    // Gets a contextspecific VFX based on the modifier type.

    public GameObject GetVFX(ModifierType type, VFXContext context)
    {

        if (_vfxDictionary.TryGetValue(type, out var contextDict))
        {

            if (contextDict.TryGetValue(context, out var vfx) && vfx != null)
            {
                return vfx; 
            }
        }

        Debug.LogWarning($"VFX not found for Type '{type}' and Context '{context}'. Using global default.");
        return globalDefaultVFX;
    }
}

[System.Serializable]
public class ContextualVFXMapping
{
    public VFXContext context;
    public GameObject vfxPrefab;
}


[System.Serializable]
public class ModifierVFXSet
{
    public ModifierType modifierType;
    public List<ContextualVFXMapping> contextualVFXs;
}




// Defines the context or mechanic of the VFX.

public enum VFXContext
{
    None,

    CastChargeEffect,
    CastLoopEffect,
    CastReleaseEffect,

    Projectile,
    
    Explosion,


    HitImpact,
}