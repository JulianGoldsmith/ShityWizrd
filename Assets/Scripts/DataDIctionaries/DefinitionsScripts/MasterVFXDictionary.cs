using UnityEngine;
using System;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MasterVFXDictionary", menuName = "Dictionaries/Master VFX Dictionary")]
public class MasterVFXDictionary : ScriptableObject
{
    public List<VFXThemeCategory> themes = new List<VFXThemeCategory>();

    // This method safely generates or updates the matrix without deleting existing prefabs
    public void GenerateMatrix()
    {
        foreach (VFXTheme themeEnum in Enum.GetValues(typeof(VFXTheme)))
        {
            VFXThemeCategory themeCategory = themes.Find(t => t.theme == themeEnum);
            if (themeCategory == null)
            {
                themeCategory = new VFXThemeCategory { theme = themeEnum };
                themes.Add(themeCategory);
            }

            foreach (VFXLifecycle lifecycleEnum in Enum.GetValues(typeof(VFXLifecycle)))
            {
                VFXLifecycleCategory lifeCategory = themeCategory.lifecycles.Find(l => l.lifecycle == lifecycleEnum);
                if (lifeCategory == null)
                {
                    lifeCategory = new VFXLifecycleCategory { lifecycle = lifecycleEnum };
                    themeCategory.lifecycles.Add(lifeCategory);
                }

                foreach (VFXTopology topologyEnum in Enum.GetValues(typeof(VFXTopology)))
                {
                    VFXShapeSlot shapeSlot = lifeCategory.shapes.Find(s => s.topology == topologyEnum);
                    if (shapeSlot == null)
                    {
                        shapeSlot = new VFXShapeSlot { topology = topologyEnum };
                        lifeCategory.shapes.Add(shapeSlot);
                    }
                }   

                // Sort shapes to ensure consistent grid ordering
                lifeCategory.shapes.Sort((a, b) => a.topology.CompareTo(b.topology));
            }
            // Sort lifecycles
            themeCategory.lifecycles.Sort((a, b) => a.lifecycle.CompareTo(b.lifecycle));
        }
        // Sort themes (Fallback always at the top)
        themes.Sort((a, b) => a.theme.CompareTo(b.theme));
    }
}

// THE TAXONOMY
public enum VFXTheme { Fallback, Frost, Fire, Stonify, Gooify, Push, Pull }
public enum VFXLifecycle { Burst, Sustain }
public enum VFXTopology { Impact, Sphere, Beam }

// THE SERIALIZED MATRIX STRUCTURES
[Serializable]
public class VFXShapeSlot
{
    public VFXTopology topology;
    public GameObject prefab;
}

[Serializable]
public class VFXLifecycleCategory
{
    public VFXLifecycle lifecycle;
    public List<VFXShapeSlot> shapes = new List<VFXShapeSlot>();

    // For Editor UI state
    [HideInInspector] public bool isExpanded = true;
}

[Serializable]
public class VFXThemeCategory
{
    public VFXTheme theme;
    public Color fallbackColor = Color.white;
    public List<VFXLifecycleCategory> lifecycles = new List<VFXLifecycleCategory>();

    // For Editor UI state
    [HideInInspector] public bool isExpanded = false;
}