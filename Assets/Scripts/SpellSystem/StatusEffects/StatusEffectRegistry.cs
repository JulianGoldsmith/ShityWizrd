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

        // Register all your effects here manually, or use reflection to find them all.
        // Example IDs: 1 = Grow, 2 = Pull, 3 = Freeze
        _effects.Add(1, new GrowStatusEffect());

        _isInitialized = true;
        Debug.Log("[StatusEffectRegistry] Initialized all stateless processors.");
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

