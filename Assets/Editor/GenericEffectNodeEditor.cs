using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GenericEffectNode))]
public class GenericEffectNodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        GenericEffectNode node = (GenericEffectNode)target;

        // 1. Draw the default inspector (so the designer can still edit the list)
        DrawDefaultInspector();

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Network Publishing", EditorStyles.boldLabel);

        // 2. Find the Master Dictionary in the project
        string[] guids = AssetDatabase.FindAssets("t:MasterEffectDictionary");
        if (guids.Length == 0)
        {
            EditorGUILayout.HelpBox("Could not find a MasterEffectDictionary in the project! Please right-click in your Resources folder and create one.", MessageType.Error);
            return;
        }

        // Load the dictionary asset
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        MasterEffectDictionary dictionary = AssetDatabase.LoadAssetAtPath<MasterEffectDictionary>(path);

        if (dictionary == null) return;

        // 3. The Opt-In Logic
        bool isPublished = dictionary.BakedEffects.Contains(node);

        if (isPublished)
        {
            // Already in the dictionary. Grey it out.
            GUI.enabled = false;
            GUILayout.Button($"Published to Network (ID: {node.NetworkEffectID})", GUILayout.Height(40));
            GUI.enabled = true;
        }
        else
        {
            // Not in the dictionary. Paint it RED.
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Publish to Network Dictionary", GUILayout.Height(40)))
            {
                // FIX: Search for a tombstoned slot starting at Index 1! (Skip 0)
                int emptySlot = -1;
                for (int i = 1; i < dictionary.BakedEffects.Count; i++)
                {
                    if (dictionary.BakedEffects[i] == null)
                    {
                        emptySlot = i;
                        break;
                    }
                }

                if (emptySlot != -1)
                {
                    dictionary.BakedEffects[emptySlot] = node;
                    node.NetworkEffectID = emptySlot;
                }
                else
                {
                    dictionary.BakedEffects.Add(node);
                    node.NetworkEffectID = dictionary.BakedEffects.Count - 1;
                }

                // Force Unity to save the changes to the disk
                EditorUtility.SetDirty(dictionary);
                EditorUtility.SetDirty(node);
                AssetDatabase.SaveAssets();
            }
            GUI.backgroundColor = Color.white; // Reset color
        }
    }
}