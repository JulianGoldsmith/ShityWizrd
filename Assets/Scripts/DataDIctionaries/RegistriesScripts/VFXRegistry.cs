using UnityEngine;
using System.Collections.Generic;

public static class VFXRegistry
{
    private static Dictionary<(VFXTheme, VFXTopology, VFXLifecycle), GameObject> _vfxDictionary;
    private static Dictionary<VFXTheme, Color> _fallbackColors;

    public static void Initialize(MasterVFXDictionary masterData)
    {
        _vfxDictionary = new Dictionary<(VFXTheme, VFXTopology, VFXLifecycle), GameObject>();
        _fallbackColors = new Dictionary<VFXTheme, Color>();

        if (masterData == null) return;

        foreach (var themeCat in masterData.themes)
        {
            _fallbackColors[themeCat.theme] = themeCat.fallbackColor;

            foreach (var lifeCat in themeCat.lifecycles)
            {
                foreach (var shape in lifeCat.shapes)
                {
                    if (shape.prefab != null)
                    {
                        _vfxDictionary.Add((themeCat.theme, shape.topology, lifeCat.lifecycle), shape.prefab);
                    }
                }
            }
        }
        Debug.Log($"[VFXRegistry] Indexed {_vfxDictionary.Count} VFX Prefabs.");
    }

    // Returns the Prefab and the Color to apply to it
    public static (GameObject prefab, Color tint) GetVFX(VFXTheme theme, VFXTopology topology, VFXLifecycle lifecycle)
    {
        // 1. Try to find the exact concrete prefab
        if (_vfxDictionary.TryGetValue((theme, topology, lifecycle), out GameObject concretePrefab))
        {
            return (concretePrefab, Color.white); // Concrete prefabs dictate their own colors natively
        }

        // 2. Exact match failed. Grab the Fallback prefab and the theme's requested tint color.
        if (_vfxDictionary.TryGetValue((VFXTheme.Fallback, topology, lifecycle), out GameObject fallbackPrefab))
        {
            Color tintColor = _fallbackColors.TryGetValue(theme, out Color color) ? color : Color.white;
            return (fallbackPrefab, tintColor);
        }

        // 3. Complete failure (Even the master fallback is missing from the dictionary)
        Debug.LogWarning($"[VFXRegistry] No concrete OR fallback prefab found for {topology} {lifecycle}!");
        return (null, Color.white);
    }
}