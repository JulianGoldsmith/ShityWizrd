using UnityEngine;

public static class MaterialRegistry
{
    private static PhysicsObjectMaterial[] _materials;
    private static PhysicsObjectMaterial _fallback;
    private static bool _isInitialized = false;

    public static void Initialize(MasterMaterialDictionary dictionaryAsset)
    {
        if (_isInitialized) return;

        if (dictionaryAsset == null)
        {
            Debug.LogError("[MaterialRegistry] Failed to initialize: Dictionary Asset is null.");
            return;
        }

        _materials = dictionaryAsset.Materials.ToArray();
        _fallback = _materials.Length > 0 ? _materials[0] : null;
        _isInitialized = true;

        Debug.Log($"[MaterialRegistry] Hydrated {_materials.Length - 1} materials into static memory.");
    }

    public static PhysicsObjectMaterial GetMaterial(ushort id)
    {
        if (!_isInitialized) Debug.LogWarning("[MaterialRegistry] Accessed before initialization!");

        // Return NULL (Fallback) if out of bounds or intentionally 0
        if (id == 0 || id >= _materials.Length) return _fallback;

        // Return the material, or fallback if it's a tombstone
        return _materials[id] != null ? _materials[id] : _fallback;
    }
}