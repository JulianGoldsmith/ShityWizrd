#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(StaticSpellDictionary))]
public class StaticSpellDictionaryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty spellsProperty = serializedObject.FindProperty("Spells");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("The Static Spell Dictionary", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Static spells are locally hydrated on every peer. Their list index is their permanent Static Spell ID. Index zero is reserved.",
            MessageType.Info
        );
        EditorGUILayout.Space(10);

        for (int i = 1; i < spellsProperty.arraySize; i++)
        {
            SerializedProperty entryProperty = spellsProperty.GetArrayElementAtIndex(i);
            SerializedProperty nameProperty = entryProperty.FindPropertyRelative("Name");
            SerializedProperty jsonProperty = entryProperty.FindPropertyRelative("JSON");

            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();

            GUILayout.Label($"[{i}]", GUILayout.Width(40));
            EditorGUILayout.PropertyField(nameProperty, GUIContent.none, GUILayout.MinWidth(120));
            EditorGUILayout.PropertyField(jsonProperty, GUIContent.none);

            if (jsonProperty.objectReferenceValue != null && GUILayout.Button("Open", GUILayout.Width(50)))
            {
                Selection.activeObject = jsonProperty.objectReferenceValue;
                EditorGUIUtility.PingObject(jsonProperty.objectReferenceValue);
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);

            if (GUILayout.Button("X", GUILayout.Width(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Tombstone Static Spell?",
                    $"Remove static spell ID {i}?\n\nThe slot will remain empty so existing IDs do not change.",
                    "Yes",
                    "Cancel"
                ))
                {
                    nameProperty.stringValue = string.Empty;
                    jsonProperty.objectReferenceValue = null;
                }
            }

            GUI.backgroundColor = Color.white;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("+ Add Static Spell", EditorStyles.miniButton, GUILayout.Height(25)))
        {
            int newIndex = spellsProperty.arraySize;
            spellsProperty.arraySize++;

            SerializedProperty newEntry = spellsProperty.GetArrayElementAtIndex(newIndex);
            newEntry.FindPropertyRelative("Name").stringValue = string.Empty;
            newEntry.FindPropertyRelative("JSON").objectReferenceValue = null;
        }

        if (GUILayout.Button("Clean Trailing Tombstones", EditorStyles.miniButton, GUILayout.Height(25)))
        {
            CleanTrailingTombstones(spellsProperty);
        }

        EditorGUILayout.EndHorizontal();

        serializedObject.ApplyModifiedProperties();
    }

    private void CleanTrailingTombstones(SerializedProperty spellsProperty)
    {
        for (int i = spellsProperty.arraySize - 1; i > 0; i--)
        {
            SerializedProperty entryProperty = spellsProperty.GetArrayElementAtIndex(i);
            SerializedProperty jsonProperty = entryProperty.FindPropertyRelative("JSON");

            if (jsonProperty.objectReferenceValue != null) break;

            spellsProperty.DeleteArrayElementAtIndex(i);
        }
    }
}
#endif