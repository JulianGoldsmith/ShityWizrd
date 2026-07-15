using UnityEngine;
using UnityEditor;
using System;

[CustomEditor(typeof(MasterVFXDictionary))]
public class MasterVFXDictionaryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MasterVFXDictionary dictionary = (MasterVFXDictionary)target;

        EditorGUILayout.Space(10);
        if (GUILayout.Button("Refresh & Validate VFX Matrix", GUILayout.Height(30)))
        {
            Undo.RecordObject(dictionary, "Generate VFX Matrix");
            dictionary.GenerateMatrix();
            EditorUtility.SetDirty(dictionary);
        }
        EditorGUILayout.Space(10);

        var topologies = (VFXTopology[])Enum.GetValues(typeof(VFXTopology));

        foreach (var themeCat in dictionary.themes)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            string headerName = themeCat.theme == VFXTheme.Fallback ? " ⭐ MASTER FALLBACKS" : $" {themeCat.theme} Theme";

            GUIStyle boldFoldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            themeCat.isExpanded = EditorGUILayout.Foldout(themeCat.isExpanded, headerName, true, boldFoldout);

            if (themeCat.theme != VFXTheme.Fallback)
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Fallback Tint:", EditorStyles.miniLabel);
                themeCat.fallbackColor = EditorGUILayout.ColorField(GUIContent.none, themeCat.fallbackColor, true, true, true, GUILayout.Width(60));
            }
            EditorGUILayout.EndHorizontal();

            if (themeCat.isExpanded)
            {
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("", GUILayout.Width(75));
                foreach (var top in topologies)
                {
                    EditorGUILayout.LabelField(top.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
                }
                EditorGUILayout.EndHorizontal();

                foreach (var lifeCat in themeCat.lifecycles)
                {
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField(lifeCat.lifecycle.ToString(), EditorStyles.boldLabel, GUILayout.Width(75));

                    foreach (var top in topologies)
                    {
                        var shape = lifeCat.shapes.Find(s => s.topology == top);

                        if (shape != null)
                        {
                            GUI.backgroundColor = shape.prefab == null ? new Color(1f, 0.8f, 0.8f) : Color.white;

                            shape.prefab = (GameObject)EditorGUILayout.ObjectField(shape.prefab, typeof(GameObject), false, GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));

                            GUI.backgroundColor = Color.white; // Reset
                        }
                        else
                        {
                            EditorGUILayout.LabelField("Refresh Needed", EditorStyles.centeredGreyMiniLabel, GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.Space(2); // Minor spacing between rows for readability
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }

        if (GUI.changed) EditorUtility.SetDirty(dictionary);
    }
}