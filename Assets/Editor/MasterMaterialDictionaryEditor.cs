#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MasterMaterialDictionary))]
public class MasterMaterialDictionaryEditor : Editor
{

    public override void OnInspectorGUI()
    {
        MasterMaterialDictionary dict = (MasterMaterialDictionary)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("The Material Dictionary", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("All active physics materials. The List Index [ID] is the Network ID used by Photon Fusion.", MessageType.Info);
        EditorGUILayout.Space(10);

        serializedObject.Update();
        SerializedProperty listProp = serializedObject.FindProperty("Materials");

        EditorGUILayout.LabelField("Registered Materials", EditorStyles.boldLabel);

        // Start at 1 because 0 is protected as NULL/Fallback
        for (int i = 1; i < listProp.arraySize; i++)
        {
            SerializedProperty elementProp = listProp.GetArrayElementAtIndex(i);
            PhysicsObjectMaterial mat = elementProp.objectReferenceValue as PhysicsObjectMaterial;

            GUILayout.BeginHorizontal("box");

            // Draw the Network ID
            GUILayout.Label($"[{i}]", GUILayout.Width(35));

            if (mat != null)
            {
                // Ensure the asset knows its own ID for local lookups
                if (mat.NetworkMaterialID != (ushort)i)
                {
                    mat.NetworkMaterialID = (ushort)i;
                    EditorUtility.SetDirty(mat);
                }

                EditorGUILayout.PropertyField(elementProp, GUIContent.none, GUILayout.ExpandWidth(true));

                if (GUILayout.Button("Edit", GUILayout.Width(60)))
                {
                    EditorGUIUtility.PingObject(mat);
                    Selection.activeObject = mat;
                }
            }
            else
            {
                EditorGUILayout.PropertyField(elementProp, GUIContent.none, GUILayout.ExpandWidth(true));
            }

            // Tombstone Button
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("X", GUILayout.Width(30)))
            {
                if (EditorUtility.DisplayDialog("Tombstone Material?", "Are you sure you want to remove this material?\n\nThis will safely leave an empty slot to protect existing Network IDs.", "Yes", "Cancel"))
                {
                    elementProp.objectReferenceValue = null;
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Material Slot", EditorStyles.miniButton, GUILayout.Height(25)))
        {
            listProp.arraySize++;
        }

        if (GUILayout.Button("Clean Trailing Tombstones", EditorStyles.miniButton, GUILayout.Height(25)))
        {
            CleanTrailingNulls(dict);
        }
        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private void CleanTrailingNulls(MasterMaterialDictionary dict)
    {
        for (int i = dict.Materials.Count - 1; i > 0; i--)
        {
            if (dict.Materials[i] == null) dict.Materials.RemoveAt(i);
            else break;
        }
        EditorUtility.SetDirty(dict);
    }
}
#endif