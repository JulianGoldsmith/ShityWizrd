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

        Color mutedPurple = new Color(0.75f, 0.65f, 0.85f, 1f);
        Color defaultColor = GUI.backgroundColor;

        GUI.backgroundColor = mutedPurple;
        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = defaultColor;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Base Properties", EditorStyles.boldLabel);

        if (GUILayout.Button("Reset", GUILayout.Width(60)))
        {
            SerializedProperty baseProp = serializedObject.FindProperty("baseProperties");

            SetProperty(baseProp, "useDensity", "Density", true, 1f);
            SetProperty(baseProp, "useFriction", "Friction", true, 0.5f);
            SetProperty(baseProp, "useRestitution", "Restitution", true, 0.2f);
            SetProperty(baseProp, "useHardness", "Hardness", true, 0.5f);
            SetProperty(baseProp, "useBrittleness", "Brittleness", true, 0.1f);
            SetProperty(baseProp, "useStickiness", "Stickiness", true, 0f);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("The checkbox determines whether this property participates when this MaterialData is used as a transformation or coating. Base materials always use every base property.", MessageType.None);
        EditorGUILayout.Space(2);

        DrawPropertyBlockFields(serializedObject.FindProperty("baseProperties"));

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        Color paleBlue = new Color(0.75f, 0.9f, 1f, 1f);
        Color softOrange = new Color(1f, 0.85f, 0.65f, 1f);
        Color softRed = new Color(1f, 0.7f, 0.7f, 1f);
        Color paleYellow = new Color(1f, 0.95f, 0.7f, 1f);

        DrawCombinedModule("Frozen", "frozenCondition", "useFrozenBlock", "frozenProperties", "frozenBonkResponse", paleBlue);
        DrawCombinedModule("Heated", "heatedCondition", "useHeatedBlock", "heatedProperties", "heatedBonkResponse", softOrange);
        DrawCombinedModule("Burning", "burningCondition", "useBurningBlock", "burningProperties", "burningBonkResponse", softRed);
        DrawCombinedModule("Conductive", "conductiveCondition", "useConductiveBlock", "conductiveProperties", "conductiveBonkResponse", paleYellow);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Audio & Visuals", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("visual_material"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("casts_shadows"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("shatter_particle_color"));

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawCombinedModule(string title, string conditionPropertyName, string useBlockPropertyName, string blockPropertyName, string bonkResponsePropertyName, Color boxColor)
    {
        SerializedProperty conditionProp = serializedObject.FindProperty(conditionPropertyName);
        SerializedProperty useBlockProp = serializedObject.FindProperty(useBlockPropertyName);
        SerializedProperty blockProp = serializedObject.FindProperty(blockPropertyName);
        SerializedProperty bonkResponseProp = serializedObject.FindProperty(bonkResponsePropertyName);

        if (conditionProp == null) return;

        Color defaultColor = GUI.backgroundColor;
        GUI.backgroundColor = boxColor;

        EditorGUILayout.BeginVertical("box");
        GUI.backgroundColor = defaultColor;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.Width(150));
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField(new GUIContent("Involved As Warp:", "Whether this condition participates when the MaterialData is used as a transformation or coating."), GUILayout.Width(105));
        EditorGUILayout.PropertyField(conditionProp.FindPropertyRelative("applyWhenUsedAsWarp"), GUIContent.none, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        EditorGUILayout.BeginHorizontal();

        float originalLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 50;

        EditorGUILayout.PropertyField(conditionProp.FindPropertyRelative("targetDriver"), new GUIContent("Driver"), GUILayout.ExpandWidth(true));
        EditorGUILayout.PropertyField(conditionProp.FindPropertyRelative("transitionCurve"), GUIContent.none, GUILayout.Width(70));

        EditorGUIUtility.labelWidth = originalLabelWidth;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth = 35;

        EditorGUILayout.PropertyField(conditionProp.FindPropertyRelative("beginThreshold"), new GUIContent("Min"));
        EditorGUILayout.Space(10);
        EditorGUILayout.PropertyField(conditionProp.FindPropertyRelative("completeThreshold"), new GUIContent("Max"));

        EditorGUIUtility.labelWidth = originalLabelWidth;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        DrawBonkResponse(bonkResponseProp);

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(useBlockProp, new GUIContent("Override Condition Properties"), GUILayout.ExpandWidth(true));

        if (useBlockProp.boolValue && blockProp != null)
        {
            if (GUILayout.Button("Copy from Base", GUILayout.Width(120))) CopyBlock(serializedObject.FindProperty("baseProperties"), blockProp);
        }

        EditorGUILayout.EndHorizontal();

        if (useBlockProp.boolValue && blockProp != null)
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

        EditorGUILayout.PropertyField(addsBonkProp, new GUIContent("Adds Bonk", "Whether this condition defines a bonk target when the material is evaluated."));

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

        DrawPropertyLine(blockProp, "useDensity", "Density", "Density");
        DrawPropertyLine(blockProp, "useFriction", "Friction", "Friction");
        DrawPropertyLine(blockProp, "useRestitution", "Restitution", "Restitution (Bounce)");
        DrawPropertyLine(blockProp, "useHardness", "Hardness", "Hardness");
        DrawPropertyLine(blockProp, "useBrittleness", "Brittleness", "Brittleness");
        DrawPropertyLine(blockProp, "useStickiness", "Stickiness", "Stickiness");

        EditorGUI.indentLevel--;
    }

    private void DrawPropertyLine(SerializedProperty blockProp, string involvementPropertyName, string valuePropertyName, string label)
    {
        EditorGUILayout.BeginHorizontal();

        SerializedProperty involvementProp = blockProp.FindPropertyRelative(involvementPropertyName);
        SerializedProperty valueProp = blockProp.FindPropertyRelative(valuePropertyName);

        EditorGUILayout.PropertyField(involvementProp, GUIContent.none, GUILayout.Width(18));
        EditorGUILayout.PropertyField(valueProp, new GUIContent(label));

        EditorGUILayout.EndHorizontal();
    }

    private void SetProperty(SerializedProperty blockProp, string involvementPropertyName, string valuePropertyName, bool involved, float value)
    {
        blockProp.FindPropertyRelative(involvementPropertyName).boolValue = involved;
        blockProp.FindPropertyRelative(valuePropertyName).floatValue = value;
    }

    private void CopyBlock(SerializedProperty source, SerializedProperty target)
    {
        CopyProperty(source, target, "useDensity", "Density");
        CopyProperty(source, target, "useFriction", "Friction");
        CopyProperty(source, target, "useRestitution", "Restitution");
        CopyProperty(source, target, "useHardness", "Hardness");
        CopyProperty(source, target, "useBrittleness", "Brittleness");
        CopyProperty(source, target, "useStickiness", "Stickiness");
    }

    private void CopyProperty(SerializedProperty source, SerializedProperty target, string involvementPropertyName, string valuePropertyName)
    {
        target.FindPropertyRelative(involvementPropertyName).boolValue = source.FindPropertyRelative(involvementPropertyName).boolValue;
        target.FindPropertyRelative(valuePropertyName).floatValue = source.FindPropertyRelative(valuePropertyName).floatValue;
    }
}
#endif