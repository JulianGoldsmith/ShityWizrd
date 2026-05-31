using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public static class StatusEffectRegistry
{
    private static readonly Dictionary<byte, IStatusEffect> _effects = new Dictionary<byte, IStatusEffect>();
    private static bool _isInitialized = false;

    public static void Initialize()
    {
        if (_isInitialized) return;

        GenericStatusEffect genericProcessor = new GenericStatusEffect();

        if (MasterEffectDictionary.Instance != null)
        {
            var dict = MasterEffectDictionary.Instance.BakedEffects;

            for (int i = 1; i < dict.Count; i++)
            {
                if (dict[i] != null)
                {
                    _effects.Add((byte)i, genericProcessor);
                }
            }
        }

        _isInitialized = true;
        Debug.Log($"[StatusEffectRegistry] Initialized all stateless processors.");
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
}

