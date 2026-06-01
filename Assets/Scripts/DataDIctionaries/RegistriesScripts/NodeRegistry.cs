using UnityEngine;

public static class NodeRegistry
{
    private static SpellNode[] _nodes;
    private static bool _isInitialized = false;

    public static void Initialize(MasterNodeDictionary dictionaryAsset)
    {
        if (_isInitialized) return;

        if (dictionaryAsset == null)
        {
            Debug.LogError("[NodeRegistry] Failed to initialize: Dictionary Asset is null.");
            return;
        }

        _nodes = dictionaryAsset.BakedNodes.ToArray();
        _isInitialized = true;

        Debug.Log($"[NodeRegistry] Hydrated {_nodes.Length - 1} node templates into static memory.");
    }

    public static SpellNode GetNodeTemplate(ushort id)
    {
        if (!_isInitialized) Debug.LogWarning("[NodeRegistry] Accessed before initialization!");
        if (id == 0 || id >= _nodes.Length) return null;
        return _nodes[id];
    }
}