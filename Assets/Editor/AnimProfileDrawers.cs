#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// --- 1. FLOAT CONDITION DRAWER ---
[CustomPropertyDrawer(typeof(FloatCondition))]
public class FloatConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        var paramProp = property.FindPropertyRelative("ParameterName");
        var opProp = property.FindPropertyRelative("Operator");
        var threshProp = property.FindPropertyRelative("Threshold");

        // Split into 3 columns: 40% Parameter, 30% Operator, 30% Threshold
        Rect paramRect = new Rect(position.x, position.y, position.width * 0.4f, position.height);
        Rect opRect = new Rect(paramRect.xMax + 5, position.y, position.width * 0.3f - 5, position.height);
        Rect threshRect = new Rect(opRect.xMax + 5, position.y, position.width * 0.3f - 5, position.height);

        EditorGUI.PropertyField(paramRect, paramProp, GUIContent.none);
        EditorGUI.PropertyField(opRect, opProp, GUIContent.none);
        EditorGUI.PropertyField(threshRect, threshProp, GUIContent.none);

        EditorGUI.EndProperty();
    }
}

// --- 2. BOOL CONDITION DRAWER ---
[CustomPropertyDrawer(typeof(BoolCondition))]
public class BoolConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        
        var paramProp = property.FindPropertyRelative("ParameterName");
        var valProp = property.FindPropertyRelative("ExpectedValue");

        Rect paramRect = new Rect(position.x, position.y, position.width * 0.6f, position.height);
        Rect valRect = new Rect(paramRect.xMax + 5, position.y, position.width * 0.4f - 5, position.height);

        EditorGUI.PropertyField(paramRect, paramProp, GUIContent.none);
        
        // Draw the bool as a dropdown for better readability (True/False instead of a checkbox)
        valProp.boolValue = EditorGUI.Popup(valRect, valProp.boolValue ? 0 : 1, new string[] { "== True", "== False" }) == 0;

        EditorGUI.EndProperty();
    }
}

// --- 3. TRIGGER CONDITION DRAWER ---
[CustomPropertyDrawer(typeof(TriggerCondition))]
public class TriggerConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.PropertyField(position, property.FindPropertyRelative("ParameterName"), GUIContent.none);
    }
}

// --- 4. STATE TIME CONDITION DRAWER ---
[CustomPropertyDrawer(typeof(StateTimeCondition))]
public class StateTimeConditionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var timeProp = property.FindPropertyRelative("MinimumSecondsInState");
        
        float oldWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 120;
        EditorGUI.PropertyField(position, timeProp, new GUIContent("Time In State >="));
        EditorGUIUtility.labelWidth = oldWidth;
    }
}

[CustomPropertyDrawer(typeof(AnimTransition))]
public class AnimTransitionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var targetProp = property.FindPropertyRelative("TargetStateID");
        var durProp = property.FindPropertyRelative("BlendDurationSeconds");
        var priProp = property.FindPropertyRelative("Priority");
        var condProp = property.FindPropertyRelative("Conditions");

        // The Top Row (Target, Dur, Pri)
        Rect topRow = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        
        float w = topRow.width;
        Rect targetRect = new Rect(topRow.x, topRow.y, w * 0.45f, topRow.height);
        Rect durRect = new Rect(targetRect.xMax + 5, topRow.y, w * 0.30f - 5, topRow.height);
        Rect priRect = new Rect(durRect.xMax + 5, topRow.y, w * 0.25f - 5, topRow.height);

        float oldWidth = EditorGUIUtility.labelWidth;
        
        EditorGUIUtility.labelWidth = 50; // Squish the word "Target"
        EditorGUI.PropertyField(targetRect, targetProp, new GUIContent("➔ To"));
        
        EditorGUIUtility.labelWidth = 35; // Squish the word "Duration"
        EditorGUI.PropertyField(durRect, durProp, new GUIContent("Dur"));
        
        EditorGUIUtility.labelWidth = 25; // Squish the word "Priority"
        EditorGUI.PropertyField(priRect, priProp, new GUIContent("Pri"));
        
        EditorGUIUtility.labelWidth = oldWidth;

        // Draw the Conditions list normally directly underneath
        Rect condRect = new Rect(position.x, topRow.yMax + 2, position.width, EditorGUI.GetPropertyHeight(condProp));
        EditorGUI.PropertyField(condRect, condProp, true);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var condProp = property.FindPropertyRelative("Conditions");
        return EditorGUIUtility.singleLineHeight + 2 + EditorGUI.GetPropertyHeight(condProp, true);
    }
}


[CustomPropertyDrawer(typeof(BlendTree1DState))]
public class BlendTree1DStateDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var nameProp = property.FindPropertyRelative("StateName");
        var rootProp = property.FindPropertyRelative("ExtractRootMotion");
        var paramProp = property.FindPropertyRelative("_parameterName");
        var motionsProp = property.FindPropertyRelative("Motions");
        var transProp = property.FindPropertyRelative("OutboundTransitions");

        float lineH = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float y = position.y;

        // --- ROW 1: Name & Root Motion (Exact same as 2D) ---
        Rect nameRect = new Rect(position.x, y, position.width * 0.65f, lineH);
        Rect rootRect = new Rect(nameRect.xMax + 5, y, position.width * 0.35f - 5, lineH);
        
        float oldWidth = EditorGUIUtility.labelWidth;
        
        EditorGUIUtility.labelWidth = 75; 
        EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("State Name"));
        
        EditorGUIUtility.labelWidth = 125; 
        EditorGUI.PropertyField(rootRect, rootProp, new GUIContent("Extract Root Motion"));
        
        y += lineH + spacing;

        // --- ROW 2: Single Parameter ---
        Rect paramRect = new Rect(position.x, y, position.width * 0.65f, lineH);
        EditorGUIUtility.labelWidth = 75; 
        EditorGUI.PropertyField(paramRect, paramProp, new GUIContent("Parameter"));
        
        EditorGUIUtility.labelWidth = oldWidth; 
        y += lineH + spacing * 2; 

        // --- ROW 3 & 4: Lists ---
        float motionsHeight = EditorGUI.GetPropertyHeight(motionsProp, true);
        Rect motionsRect = new Rect(position.x, y, position.width, motionsHeight);
        EditorGUI.PropertyField(motionsRect, motionsProp, true);
        y += motionsHeight + spacing;

        float transHeight = EditorGUI.GetPropertyHeight(transProp, true);
        Rect transRect = new Rect(position.x, y, position.width, transHeight);
        EditorGUI.PropertyField(transRect, transProp, true);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var motionsProp = property.FindPropertyRelative("Motions");
        var transProp = property.FindPropertyRelative("OutboundTransitions");

        float height = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2; 
        height += EditorGUIUtility.standardVerticalSpacing; 
        height += EditorGUI.GetPropertyHeight(motionsProp, true) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(transProp, true);

        return height;
    }
}

[CustomPropertyDrawer(typeof(BlendState2D))]
public class BlendState2DDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var nameProp = property.FindPropertyRelative("StateName");
        var rootProp = property.FindPropertyRelative("ExtractRootMotion");
        var paramXProp = property.FindPropertyRelative("_parameterX");
        var paramYProp = property.FindPropertyRelative("_parameterY");
        var motionsProp = property.FindPropertyRelative("Motions");
        var transProp = property.FindPropertyRelative("OutboundTransitions");

        float lineH = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float y = position.y;

        // --- ROW 1: Name & Root Motion ---
        Rect nameRect = new Rect(position.x, y, position.width * 0.65f, lineH);
        Rect rootRect = new Rect(nameRect.xMax + 5, y, position.width * 0.35f - 5, lineH);
        
        float oldWidth = EditorGUIUtility.labelWidth;
        
        EditorGUIUtility.labelWidth = 75; // Compress "State Name"
        EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("State Name"));
        
        EditorGUIUtility.labelWidth = 125; // Compress "Extract Root Motion"
        EditorGUI.PropertyField(rootRect, rootProp, new GUIContent("Extract Root Motion"));
        
        y += lineH + spacing;

        // --- ROW 2: Parameters (X and Y inline) ---
        Rect paramXRect = new Rect(position.x, y, position.width * 0.5f - 2, lineH);
        Rect paramYRect = new Rect(position.x + position.width * 0.5f + 2, y, position.width * 0.5f - 2, lineH);
        
        EditorGUIUtility.labelWidth = 55; // Compress "Param X"
        EditorGUI.PropertyField(paramXRect, paramXProp, new GUIContent("Param X"));
        EditorGUI.PropertyField(paramYRect, paramYProp, new GUIContent("Param Y"));
        
        EditorGUIUtility.labelWidth = oldWidth; // Reset for Unity's default lists
        y += lineH + spacing * 2; // Extra tiny gap before the lists

        // --- ROW 3: Motions List ---
        float motionsHeight = EditorGUI.GetPropertyHeight(motionsProp, true);
        Rect motionsRect = new Rect(position.x, y, position.width, motionsHeight);
        EditorGUI.PropertyField(motionsRect, motionsProp, true);
        y += motionsHeight + spacing;

        // --- ROW 4: Transitions List ---
        float transHeight = EditorGUI.GetPropertyHeight(transProp, true);
        Rect transRect = new Rect(position.x, y, position.width, transHeight);
        EditorGUI.PropertyField(transRect, transProp, true);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var motionsProp = property.FindPropertyRelative("Motions");
        var transProp = property.FindPropertyRelative("OutboundTransitions");

        float height = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2; // Rows 1 & 2
        height += EditorGUIUtility.standardVerticalSpacing; // Extra gap
        height += EditorGUI.GetPropertyHeight(motionsProp, true) + EditorGUIUtility.standardVerticalSpacing;
        height += EditorGUI.GetPropertyHeight(transProp, true);

        return height;
    }
}
#endif
