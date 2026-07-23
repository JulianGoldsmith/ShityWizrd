#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MaterialData), true)]
public class MaterialDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();

        // --- 1. BASE PROPERTIES (Muted Purple) ---
        Color mutedPurple = new Color(0.75f, 0.65f, 0.85f, 1f);
        Color defaultColor = GUI.backgroundColor;

        GUI.backgroundColor = mutedPurple;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = defaultColor; // Reset instantly so fields inside aren't tinted

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Base Properties", EditorStyles.boldLabel);
        if (GUILayout.Button("Reset", GUILayout.Width(60)))
        {
            SerializedProperty baseProp = serializedObject.FindProperty("baseProperties");
            baseProp.FindPropertyRelative("Density").floatValue = 1.0f;
            baseProp.FindPropertyRelative("Friction").floatValue = 0.5f;
            baseProp.FindPropertyRelative("Restitution").floatValue = 0.2f;
            baseProp.FindPropertyRelative("Hardness").floatValue = 0.5f;
            baseProp.FindPropertyRelative("Brittleness").floatValue = 0.1f;
            baseProp.FindPropertyRelative("Stickiness").floatValue = 0.0f;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);
        DrawPropertyBlockFields(serializedObject.FindProperty("baseProperties"));

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(10);

        // --- 2. CONDITIONS & OVERRIDES ---
        Color paleBlue = new Color(0.75f, 0.9f, 1.0f, 1f);
        Color softOrange = new Color(1.0f, 0.85f, 0.65f, 1f);
        Color softRed = new Color(1.0f, 0.7f, 0.7f, 1f);
        Color paleYellow = new Color(1.0f, 0.95f, 0.7f, 1f);

        DrawCombinedModule("❄️ Frozen", "frozenCondition", "useFrozenBlock", "frozenProperties", "frozenBonkResponse", paleBlue);
        DrawCombinedModule("🔥 Heated", "heatedCondition", "useHeatedBlock", "heatedProperties", "heatedBonkResponse", softOrange);
        DrawCombinedModule("🌋 Burning", "burningCondition", "useBurningBlock", "burningProperties", "burningBonkResponse", softRed);
        DrawCombinedModule("⚡ Conductive", "conductiveCondition", "useConductiveBlock", "conductiveProperties", "conductiveBonkResponse", paleYellow);

        EditorGUILayout.Space(10);

        // --- 3. AUDIO & VISUALS ---
        EditorGUILayout.LabelField("Audio & Visuals", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("visual_material"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("casts_shadows"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shatter_particle_color"));

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCombinedModule(string title, string condPropName, string usePropName, string blockPropName, string bonkResponsePropName, Color boxColor)
    {
        SerializedProperty condProp = serializedObject.FindProperty(condPropName);
        SerializedProperty useProp = serializedObject.FindProperty(usePropName);
        SerializedProperty blockProp = serializedObject.FindProperty(blockPropName);
        SerializedProperty bonkResponseProp = serializedObject.FindProperty(bonkResponsePropName);

        if (condProp == null) return;

        Color defaultColor = GUI.backgroundColor;
        GUI.backgroundColor = boxColor;

        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = defaultColor;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.Width(150));
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("Apply as Warp:", GUILayout.Width(90));
        EditorGUILayout.PropertyField(condProp.FindPropertyRelative("applyWhenUsedAsWarp"), GUIContent.none, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();
        float originalLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 50;

        EditorGUILayout.PropertyField(condProp.FindPropertyRelative("targetDriver"), new GUIContent("Driver"), GUILayout.ExpandWidth(true));
        EditorGUILayout.PropertyField(condProp.FindPropertyRelative("transitionCurve"), GUIContent.none, GUILayout.Width(70));

        EditorGUIUtility.labelWidth = originalLabelWidth;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth = 35;

        EditorGUILayout.PropertyField(condProp.FindPropertyRelative("beginThreshold"), new GUIContent("Min"));
        EditorGUILayout.Space(10);
        EditorGUILayout.PropertyField(condProp.FindPropertyRelative("completeThreshold"), new GUIContent("Max"));

        EditorGUIUtility.labelWidth = originalLabelWidth;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        DrawBonkResponse(bonkResponseProp);

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(useProp, new GUIContent("Override Condition Properties"), GUILayout.ExpandWidth(true));

        if (useProp.boolValue && blockProp != null)
        {
            if (GUILayout.Button("Copy from Base", GUILayout.Width(120)))
            {
                CopyBlock(serializedObject.FindProperty("baseProperties"), blockProp);
            }
        }

        EditorGUILayout.EndHorizontal();

        if (useProp.boolValue && blockProp != null)
        {
            EditorGUILayout.Space(2);
            DrawPropertyBlockFields(blockProp);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
    }

    private void DrawBonkResponse(SerializedProperty bonkResponseProp)
    {
        if (bonkResponseProp == null) return;

        SerializedProperty addsBonkProp = bonkResponseProp.FindPropertyRelative("addsBonk");

        EditorGUILayout.PropertyField(addsBonkProp, new GUIContent("Adds Bonk", "Whether this material condition contributes to the persistent elemental Bonk floor."));

        if (!addsBonkProp.boolValue) return;

        EditorGUILayout.BeginHorizontal();

        float originalLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 32;

        EditorGUILayout.PropertyField(bonkResponseProp.FindPropertyRelative("stressType"), new GUIContent("Type"), GUILayout.MinWidth(105));
        EditorGUILayout.PropertyField(bonkResponseProp.FindPropertyRelative("minBonk"), new GUIContent("Min"), GUILayout.Width(70));
        EditorGUILayout.PropertyField(bonkResponseProp.FindPropertyRelative("maxBonk"), new GUIContent("Max"), GUILayout.Width(70));
        EditorGUILayout.PropertyField(bonkResponseProp.FindPropertyRelative("responseCurve"), GUIContent.none, GUILayout.Width(65));

        EditorGUIUtility.labelWidth = originalLabelWidth;

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPropertyBlockFields(SerializedProperty blockProp)
    {
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(blockProp.FindPropertyRelative("Density"));
        EditorGUILayout.PropertyField(blockProp.FindPropertyRelative("Friction"));
        EditorGUILayout.PropertyField(blockProp.FindPropertyRelative("Restitution"), new GUIContent("Restitution (Bounce)"));
        EditorGUILayout.PropertyField(blockProp.FindPropertyRelative("Hardness"));
        EditorGUILayout.PropertyField(blockProp.FindPropertyRelative("Brittleness"));
        EditorGUILayout.PropertyField(blockProp.FindPropertyRelative("Stickiness"));
        EditorGUI.indentLevel--;
    }

    private void CopyBlock(SerializedProperty source, SerializedProperty target)
    {
        target.FindPropertyRelative("Density").floatValue = source.FindPropertyRelative("Density").floatValue;
        target.FindPropertyRelative("Friction").floatValue = source.FindPropertyRelative("Friction").floatValue;
        target.FindPropertyRelative("Restitution").floatValue = source.FindPropertyRelative("Restitution").floatValue;
        target.FindPropertyRelative("Hardness").floatValue = source.FindPropertyRelative("Hardness").floatValue;
        target.FindPropertyRelative("Brittleness").floatValue = source.FindPropertyRelative("Brittleness").floatValue;
        target.FindPropertyRelative("Stickiness").floatValue = source.FindPropertyRelative("Stickiness").floatValue;
    }
}
#endif