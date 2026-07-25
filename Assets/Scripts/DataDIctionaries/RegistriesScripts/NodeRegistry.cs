using UnityEngine;

public static class NodeRegistry
{
    private static SpellNode[] _nodes;
    private static bool _isInitialized;

    public static bool IsInitialized => _isInitialized;

    public static void Initialize(MasterNodeDictionary dictionaryAsset)
    {
        if (_isInitialized)
            return;

        if (dictionaryAsset == null)
        {
            Debug.LogError("[NodeRegistry] Failed to initialize: Dictionary asset is null.");
            return;
        }

        _nodes = dictionaryAsset.BakedNodes.ToArray();
        _isInitialized = true;

        Debug.Log($"[NodeRegistry] Hydrated {_nodes.Length - 1} node templates into static memory.");
    }

    public static bool TryGetNodeTemplate(ushort id, out SpellNode node)
    {
        node = null;

        if (!_isInitialized || _nodes == null)
            return false;

        if (id == 0 || id >= _nodes.Length)
            return false;

        node = _nodes[id];
        return node != null;
    }

    public static SpellNode GetNodeTemplate(ushort id)
    {
        if (TryGetNodeTemplate(id, out SpellNode node))
            return node;

        if (!_isInitialized)
            Debug.LogWarning("[NodeRegistry] Accessed before initialization.");

        return null;
    }
}