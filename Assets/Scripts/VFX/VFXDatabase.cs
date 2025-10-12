using UnityEngine;
using System.Collections.Generic;
using System.Linq; 

[CreateAssetMenu(fileName = "VFXDatabase", menuName = "Spell System/VFXDatabase")]
public class VFXDatabase : ScriptableObject
{
    [Tooltip("The ultimate fallback VFX if nothing else is found.")]
    public GameObject globalDefaultVFX;

    [SerializeField]
    private List<ContextualVFXSet> vfxSets;

    private Dictionary<VFXContext, Dictionary<ModifierType, GameObject>> _vfxDictionary;

    private void OnEnable()
    {
        _vfxDictionary = new Dictionary<VFXContext, Dictionary<ModifierType, GameObject>>();
        foreach(var vfxSet in vfxSets)
        {
            var modifiervfxlists = new Dictionary<ModifierType, GameObject>();
            foreach (var mapping in vfxSet.modifierVFXs)
            {
                modifiervfxlists[mapping.modifierType] = mapping.vfxPrefab;
            }
            _vfxDictionary[vfxSet.context] = modifiervfxlists;
        }
    }


    // Gets a contextspecific VFX based on the modifier type.

    public GameObject GetVFX(VFXContext context, ModifierType type)
    {

        if (_vfxDictionary.TryGetValue(context, out var contextDict))
        {

            if (contextDict.TryGetValue(type, out var vfx) && vfx != null)
            {
                return vfx; 
            }
        }

        Debug.LogWarning($"VFX not found for Type '{type}' and Context '{context}'. Using global default.");
        return globalDefaultVFX;
    }
}

[System.Serializable]
public class ContextualVFXSet
{
    public VFXContext context;
    public List<ModifierVFXMapping> modifierVFXs;
}


[System.Serializable]
public class ModifierVFXMapping
{
    public ModifierType modifierType;
    public GameObject vfxPrefab;
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

    AuraBubble,
}