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

        GUI.enabled = false;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("NetworkMaterialID"), new GUIContent("Network ID"));
        GUI.enabled = true;

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Core Data Profile", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("The complete underlying material used before transformations and coatings are calculated.", MessageType.None);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseData"), new GUIContent("Base Data Asset"));

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Transform Warp Overrides", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Transform warps compete with the base material. At a value of one, an involved property is completely transformed.", MessageType.None);
        DrawSafeSlot("stoneifyOverride", "Stoneify");

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Coating Warp Overrides", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Coatings modify the already-transformed material. Only checked properties and involved conditions participate.", MessageType.None);
        DrawSafeSlot("gooifyOverride", "Gooify");

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("State Evolution Rates", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ambientCoolingRate"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("naturalDryingRate"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("warpDecayRate"));

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Visuals", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("visual_material"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("casts_shadows"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shatter_particle_color"));

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSafeSlot(string propertyName, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property != null) EditorGUILayout.PropertyField(property, new GUIContent(label));
    }
}
#endif