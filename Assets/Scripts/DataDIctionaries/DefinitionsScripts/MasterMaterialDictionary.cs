using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[CreateAssetMenu(fileName = "MasterMaterialDictionary", menuName = "Dictionaries/Master Material Dictionary")]
public class MasterMaterialDictionary : ScriptableObject
{
    [Tooltip("Do not edit manually! Use the Editor UI.")]
    public List<PhysicsObjectMaterial> Materials = new List<PhysicsObjectMaterial>() { null };

    private void OnValidate()
    {
        if (Materials == null) { Materials = new List<PhysicsObjectMaterial>() { null }; return; }
        if (Materials.Count == 0) { Materials.Add(null); }
        else if (Materials[0] != null)
        {
            PhysicsObjectMaterial displaced = Materials[0];
            Materials.Add(displaced);
            Materials[0] = null;
            Debug.LogWarning($"[MasterMaterialDictionary] Auto-Correction: Index 0 is reserved empty for NULL/Fallback. Moved {displaced.name}.");
        }
    }
}

public class MaterialIDAttribute : PropertyAttribute
{
    // Empty, just acts as a tag for the Editor Drawer
    public MaterialIDAttribute() { }
}

public static class VFXLookUp
{

    public static GameObject SpawnVFX(VFXTheme theme, VFXTopology topology, VFXLifecycle lifecycle, Vector3 position, Quaternion rotation, float magnitude)
    {
        // 1. Get the prefab and the tint color from our Registry
        var (prefab, tint) = VFXRegistry.GetVFX(theme, topology, lifecycle);

        if (prefab == null) return null;

        // 2. Instantiate the visual (Local client only, no network load!)
        GameObject vfxInstance = Object.Instantiate(prefab, position, rotation);

        // 3. Inject Data into the Unity VFX Graph
        VisualEffect vfxGraph = vfxInstance.GetComponentInChildren<VisualEffect>();

        if (vfxGraph != null)
        {
            // We use Has[Property] checks so the game doesn't throw errors 
            // if you build a graph and forget to expose these specific variables.

            if (vfxGraph.HasVector4("TintColor"))
            {
                // Note: VFX Graph expects colors as HDR Vector4s usually, 
                // but passing standard Unity Color often auto-converts.
                vfxGraph.SetVector4("TintColor", tint);
            }

            if (vfxGraph.HasFloat("Magnitude"))
            {
                vfxGraph.SetFloat("Magnitude", magnitude);
            }

            // If it's a burst, we can tell the graph to play immediately
            if (lifecycle == VFXLifecycle.Burst)
            {
                vfxGraph.Play();
            }
        }
        else
        {
            Debug.LogWarning($"[VFXFactory] Prefab {prefab.name} does not contain a VisualEffect component!");
        }

        return vfxInstance;
    }
}