using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

[CustomEditor(typeof(GlobalNPCCommandRegistry))]
public class GlobalNPCCommandRegistryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw the default list so you can still see it
        DrawDefaultInspector();

        GlobalNPCCommandRegistry registry = (GlobalNPCCommandRegistry)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Registry Auto-Tools", EditorStyles.boldLabel);

        // --- BUTTON 1: VALIDATE & SORT ---
        if (GUILayout.Button("1. Validate & Sort (Fix Errors)", GUILayout.Height(30)))
        {
            ValidateAndSort(registry);
        }

        // --- BUTTON 2: AUTO-FILL ---
        if (GUILayout.Button("2. Auto-Fetch Commands", GUILayout.Height(30)))
        {
            AutoFill(registry);
        }

        EditorGUILayout.Space(10);

        // --- BUTTON 3: FACTORY RESET ---
        GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); // Light red
        if (GUILayout.Button("DANGER: Factory Reset (Clear All)", GUILayout.Height(25)))
        {
            if (EditorUtility.DisplayDialog("Factory Reset Registry?",
                "Are you sure you want to clear the entire registry? All SO assignments will be wiped.",
                "Yes, Wipe It", "Cancel"))
            {
                ClearAll(registry);
            }
        }
        GUI.backgroundColor = Color.white;
    }

    private void ValidateAndSort(GlobalNPCCommandRegistry registry)
    {
        Undo.RecordObject(registry, "Validate and Sort Registry");

        var enumValues = Enum.GetValues(typeof(CommandType)).Cast<CommandType>().ToList();
        enumValues.Remove(CommandType.None); // We don't need a slot for 'None'

        // 1. Purge mismatched SOs
        for (int i = 0; i < registry.UniversalCommands.Count; i++)
        {
            var map = registry.UniversalCommands[i];
            if (map.Command != null && map.Command.Type != map.Type)
            {
                Debug.LogWarning($"[Registry] Removed mismatched command '{map.Command.name}' from the {map.Type} slot.");
                map.Command = null;
                registry.UniversalCommands[i] = map;
            }
        }

        // 2. Add missing Enum slots
        var existingTypes = registry.UniversalCommands.Select(m => m.Type).ToList();
        foreach (var type in enumValues)
        {
            if (!existingTypes.Contains(type))
            {
                registry.UniversalCommands.Add(new GlobalNPCCommandRegistry.Mapping
                {
                    SlotName = type.ToString(),
                    Type = type,
                    Command = null
                });
            }
        }

        // 3. Sort by Enum integer value and fix names
        registry.UniversalCommands.Sort((a, b) => a.Type.CompareTo(b.Type));

        for (int i = 0; i < registry.UniversalCommands.Count; i++)
        {
            var map = registry.UniversalCommands[i];
            map.SlotName = map.Type.ToString(); // Refreshes the hidden string for the inspector UI
            registry.UniversalCommands[i] = map;
        }

        EditorUtility.SetDirty(registry);
        Debug.Log("[Registry] Validated and Sorted successfully!");
    }

    private void AutoFill(GlobalNPCCommandRegistry registry)
    {
        // Always validate first to ensure our slots are perfect
        ValidateAndSort(registry);

        Undo.RecordObject(registry, "Auto-Fill Registry");

        // Find all SOs in the project that derive from NPCCommand
        string[] guids = AssetDatabase.FindAssets("t:NPCCommand");
        int fillCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            NPCCommand commandAsset = AssetDatabase.LoadAssetAtPath<NPCCommand>(path);

            if (commandAsset != null)
            {
                CommandType targetType = commandAsset.Type;

                for (int i = 0; i < registry.UniversalCommands.Count; i++)
                {
                    var map = registry.UniversalCommands[i];
                    if (map.Type == targetType)
                    {
                        if (map.Command == null)
                        {
                            map.Command = commandAsset;
                            registry.UniversalCommands[i] = map;
                            fillCount++;
                        }
                        else if (map.Command != commandAsset)
                        {
                            Debug.LogWarning($"[Registry] Found duplicate SO for {targetType}: '{commandAsset.name}'. Kept existing '{map.Command.name}'.");
                        }
                        break;
                    }
                }
            }
        }

        EditorUtility.SetDirty(registry);
        Debug.Log($"[Registry] Auto-Fill complete! Slotted {fillCount} new commands.");
    }

    private void ClearAll(GlobalNPCCommandRegistry registry)
    {
        Undo.RecordObject(registry, "Clear Registry");

        registry.UniversalCommands.Clear();

        var enumValues = Enum.GetValues(typeof(CommandType)).Cast<CommandType>().ToList();
        enumValues.Remove(CommandType.None);

        foreach (var type in enumValues)
        {
            registry.UniversalCommands.Add(new GlobalNPCCommandRegistry.Mapping
            {
                SlotName = type.ToString(),
                Type = type,
                Command = null
            });
        }

        EditorUtility.SetDirty(registry);
        Debug.Log("[Registry] Factory Reset complete! All slots are empty.");
    }
}