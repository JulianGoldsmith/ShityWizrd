using System.Collections.Generic;
using UnityEngine;

public static class StatusEffectRegistry
{
    private static readonly Dictionary<byte, IStatusEffect> _effects = new Dictionary<byte, IStatusEffect>();
    private static EffectNode[] _blueprints; // Fast array cache for runtime logic!
    private static bool _isInitialized = false;

    public static void Initialize(MasterStatusDictionary dictAsset)
    {
        if (_isInitialized) return;
        if (dictAsset == null) return;

        _blueprints = dictAsset.BakedStatuses.ToArray();

        GenericStatusEffect genericProcessor = new GenericStatusEffect();

        var dict = dictAsset.BakedStatuses;

        for (int i = 1; i < dict.Count; i++)
        {
            if (dict[i] != null)
            {
                _effects.Add((byte)i, genericProcessor);
            }
        }

        _isInitialized = true;
        Debug.Log($"[StatusEffectRegistry] Initialized {_effects.Count} stateless processors.");
    }

    public static IStatusEffect GetStatusEffect(byte effectID)
    {
        if (_effects.TryGetValue(effectID, out IStatusEffect processor))
        {
            return processor;
        }

        Debug.LogError($"[StatusEffectRegistry] No processor found for Effect ID: {effectID}!");
        return null;
    }

    public static EffectNode GetBlueprint(byte effectID)
    {
        if (!_isInitialized || effectID == 0 || effectID >= _blueprints.Length) return null;
        return _blueprints[effectID];
    }
}