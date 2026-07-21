using UnityEditor;
using UnityEngine;

// This tells Unity to use this script to draw the inspector for BonkManager
[CustomEditor(typeof(BonkManager))]
public class BonkManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 1. Draw all the standard variables (Max Composure, Decay Rate, etc.)
        DrawDefaultInspector();

        // Grab a reference to the script we are currently inspecting
        BonkManager manager = (BonkManager)target;

        EditorGUILayout.Space(10); // Add a little breathing room

        GUI.backgroundColor = new Color(0.65f, 0.25f, 0.85f);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Clear Bonk", GUILayout.Height(40)))
            {
                manager.ClearBonk();
            }
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        // 2. Set the button color based on whether bones are found
        if (manager.bones == null || manager.bones.Count == 0)
        {
            GUI.backgroundColor = Color.red;
        }
        else
        {
            GUI.backgroundColor = Color.green;
        }

        // 3. Create the big button and dynamically change its text
        string buttonText = manager.bones == null || manager.bones.Count == 0
            ? "Auto-Find Bones (Missing!)"
            : $"Update Bones (Found: {manager.bones.Count})";

        // GUILayout.Height(40) makes it nice and chunky
        if (GUILayout.Button(buttonText, GUILayout.Height(40)))
        {
            // Register an Undo state so you can Ctrl+Z if you accidentally wipe a custom setup
            Undo.RecordObject(manager, "Auto-Find Bones");

            // Call the method we wrote earlier
            manager.AutoFindBones();

            // Mark the object as "dirty" so Unity knows to save these new references to the prefab/scene
            EditorUtility.SetDirty(manager);
        }

        // 4. Reset the background color so we don't accidentally tint the rest of the Unity editor
        GUI.backgroundColor = Color.white;
    }
}