#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PhysicsObjectMaterial), true)]
public class PhysicsObjectMaterialEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("material_name"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("label"));

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Core Data Profile", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("The mathematical foundation of this object.", MessageType.None);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseData"), new GUIContent("Base Data Asset"));

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Warp Overrides", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Slot in specific MaterialData assets for custom reactions. Leave blank to use global defaults.", MessageType.None);
        DrawSafeSlot("stoneifyOverride", "Stoneify");
        DrawSafeSlot("gooifyOverride", "Gooify");
        DrawSafeSlot("rubberifyOverride", "Rubberify");
        DrawSafeSlot("oilifyOverride", "Oilify");

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("State Evolution Rates", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientCoolingRate"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("naturalDryingRate"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("warpDecayRate"));

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Visuals (Legacy Testing)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These fields exist temporarily on the housing to preserve dependencies.", MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("vfx_material"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("casts_shadows"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shatter_particle_color"));

        serializedObject.ApplyModifiedProperties();
    }

    // Safely draw warp override slots even if the variables haven't been added to the script yet
    private void DrawSafeSlot(string propertyName, string label)
    {
        SerializedProperty prop = serializedObject.FindProperty(propertyName);
        if (prop != null)
        {
            EditorGUILayout.PropertyField(prop, new GUIContent(label));
        }
    }
}
#endif