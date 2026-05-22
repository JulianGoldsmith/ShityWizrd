#if UNITY_EDITOR
using System.Linq; 
using UnityEditor;
using UnityEngine;

// ============================================================================
// 1. THE CUSTOM INSPECTOR
// ============================================================================

[CustomEditor(typeof(AnimMasterProfileSO))]
public class AnimMasterProfileSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var defaultEntryProp = serializedObject.FindProperty("DefaultEntryStateID");
        var allStatesProp = serializedObject.FindProperty("AllStates");
        var anyStateTransProp = serializedObject.FindProperty("AnyStateTransitions");
        var floatParamsProp = serializedObject.FindProperty("FloatParameters");
        var boolParamsProp = serializedObject.FindProperty("BoolParameters");
        var triggerParamsProp = serializedObject.FindProperty("TriggerParameters");

        EditorGUILayout.PropertyField(defaultEntryProp);
        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("All States", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        
        if (allStatesProp.arraySize == 0)
        {
            EditorGUILayout.LabelField("No states added yet.", EditorStyles.centeredGreyMiniLabel);
        }
        else
        {
            for (int i = 0; i < allStatesProp.arraySize; i++)
            {
                var stateProp = allStatesProp.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(stateProp, GUIContent.none, true);
                EditorGUILayout.Space(4);
            }
        }

        EditorGUILayout.Space(5);
        if (GUILayout.Button("+ Add New State", GUILayout.Height(30)))
        {
            ShowAddStateMenu(allStatesProp);
        }

        EditorGUILayout.Space(15);

        EditorGUILayout.PropertyField(anyStateTransProp);
        EditorGUILayout.Space(10);
        
        EditorGUILayout.LabelField("Blackboard Parameters", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(floatParamsProp);
        EditorGUILayout.PropertyField(boolParamsProp);
        EditorGUILayout.PropertyField(triggerParamsProp);

        serializedObject.ApplyModifiedProperties();
    }

    private void ShowAddStateMenu(SerializedProperty allStatesProp)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("1D Blend Tree"), false, () => AddState(allStatesProp, typeof(BlendTree1DState)));
        menu.AddItem(new GUIContent("2D Blend Tree"), false, () => AddState(allStatesProp, typeof(BlendState2D)));
        menu.ShowAsContext();
    }

    private void AddState(SerializedProperty allStatesProp, System.Type type)
    {
        allStatesProp.serializedObject.Update();
        allStatesProp.arraySize++;
        var newElement = allStatesProp.GetArrayElementAtIndex(allStatesProp.arraySize - 1);
        newElement.managedReferenceValue = System.Activator.CreateInstance(type);
        newElement.isExpanded = true;
        allStatesProp.serializedObject.ApplyModifiedProperties();
    }
}

// ============================================================================
// 2. THE MASTER UI BLOCK ARCHITECTURE
// ============================================================================

public class BaseCollapsibleBlockDrawer : PropertyDrawer
{
    protected virtual Color HeaderColor => Color.white;
    protected virtual Color BodyColor => Color.gray;
    protected virtual Color ButtonColor => new Color(0.5f, 0.5f, 0.5f, 1f);
    protected virtual bool HasDeleteButton => false;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineH = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        if (property.isExpanded)
        {
            Rect bodyRect = new Rect(position.x - 2, position.y - 2, position.width + 4, GetPropertyHeight(property, label) + 4);
            EditorGUI.DrawRect(bodyRect, BodyColor);
        }

        Rect headerRect = new Rect(position.x - 2, position.y - 2, position.width + 4, lineH + 8);
        EditorGUI.DrawRect(headerRect, HeaderColor);

        float y = position.y + 4;
        float btnW = 20f;

        Rect minRect = new Rect(position.x, y, btnW, lineH);
        GUI.backgroundColor = ButtonColor; 
        if (GUI.Button(minRect, property.isExpanded ? "-" : "+")) property.isExpanded = !property.isExpanded;
        GUI.backgroundColor = Color.white;

        Rect delRect = new Rect(position.xMax - btnW, y, btnW, lineH);
        if (HasDeleteButton)
        {
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUI.Button(delRect, "X"))
            {
                DeleteArrayElementSafe(property);
                GUI.backgroundColor = Color.white;
                EditorGUI.EndProperty();
                return;
            }
            GUI.backgroundColor = Color.white;
        }

        float contentW = position.width - btnW - 10;
        if (HasDeleteButton) contentW -= (btnW + 5);
        
        Rect headerContentRect = new Rect(minRect.xMax + 5, y, contentW, lineH);
        DrawHeaderContent(headerContentRect, property);

        y += lineH + spacing * 2;

        if (property.isExpanded)
        {
            DrawBodyContent(position, property, ref y, lineH, spacing);
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight + 8; 
        if (property.isExpanded)
        {
            height += EditorGUIUtility.standardVerticalSpacing * 2; 
            height += GetBodyHeight(property);
            height += 8f; 
        }
        return height;
    }

    protected virtual void DrawHeaderContent(Rect rect, SerializedProperty property) { }
    protected virtual void DrawBodyContent(Rect position, SerializedProperty property, ref float y, float lineH, float spacing) { }
    protected virtual float GetBodyHeight(SerializedProperty property) { return 0f; }

    protected void DeleteArrayElementSafe(SerializedProperty property)
    {
        string path = property.propertyPath;
        int bracket = path.LastIndexOf('[');
        if (bracket > 0)
        {
            string arrayPath = path.Substring(0, bracket - 11);
            int index = int.Parse(path.Substring(bracket + 1, path.Length - bracket - 2));

            SerializedProperty arrayProp = property.serializedObject.FindProperty(arrayPath);
            if (arrayProp != null)
            {
                arrayProp.DeleteArrayElementAtIndex(index);
                if (index < arrayProp.arraySize && arrayProp.GetArrayElementAtIndex(index).managedReferenceValue == null)
                    arrayProp.DeleteArrayElementAtIndex(index);
                property.serializedObject.ApplyModifiedProperties();
            }
        }
    }
    
    protected SerializedProperty GetProp(SerializedProperty p, params string[] names)
    {
        foreach (var n in names)
        {
            var f = p.FindPropertyRelative(n);
            if (f != null) return f;
        }
        return null;
    }
}

// ============================================================================
// 3. THE TRANSITION DRAWER (Muted Plum Palette)
// ============================================================================

[CustomPropertyDrawer(typeof(AnimTransition))]
public class AnimTransitionDrawer : BaseCollapsibleBlockDrawer
{
    // The inner panel colors (increasingly lighter/desaturated plum)
    protected override Color HeaderColor => new Color(0.32f, 0.26f, 0.32f, 1f); 
    protected override Color BodyColor => new Color(0.36f, 0.31f, 0.36f, 1f); 
    protected override Color ButtonColor => new Color(0.55f, 0.40f, 0.55f, 1f); // Muted Rose Button
    protected override bool HasDeleteButton => true;

    protected override void DrawHeaderContent(Rect rect, SerializedProperty property)
    {
        var targetProp = GetProp(property, "TargetStateID", "targetStateID");
        float oldWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 40; 
        if (targetProp != null) EditorGUI.PropertyField(rect, targetProp, new GUIContent("➔ To"));
        EditorGUIUtility.labelWidth = oldWidth;
    }

    protected override void DrawBodyContent(Rect position, SerializedProperty property, ref float y, float lineH, float spacing)
    {
        var durProp = GetProp(property, "BlendDurationSeconds", "BlendDuration", "blendDuration");
        var priProp = GetProp(property, "Priority", "priority");
        var condProp = GetProp(property, "Conditions", "conditions");

        float oldWidth = EditorGUIUtility.labelWidth;

        Rect durRect = new Rect(position.x + 5, y, position.width * 0.5f - 10, lineH);
        Rect priRect = new Rect(position.x + position.width * 0.5f + 5, y, position.width * 0.5f - 10, lineH);

        EditorGUIUtility.labelWidth = 55;
        if (durProp != null) EditorGUI.PropertyField(durRect, durProp, new GUIContent("Duration"));
        
        EditorGUIUtility.labelWidth = 50;
        if (priProp != null) EditorGUI.PropertyField(priRect, priProp, new GUIContent("Priority"));
        
        EditorGUIUtility.labelWidth = oldWidth;
        y += lineH + spacing * 2;

        if (condProp != null)
        {
            for (int i = 0; i < condProp.arraySize; i++)
            {
                var condElement = condProp.GetArrayElementAtIndex(i);
                
                // Draw a very subtle background box just for this condition
                Rect condBgRect = new Rect(position.x + 5, y - 2, position.width - 10, lineH + 4);
                EditorGUI.DrawRect(condBgRect, new Color(0.42f, 0.36f, 0.42f, 1f)); 

                Rect condRect = new Rect(position.x + 8, y, position.width - 36, lineH);
                Rect condDelRect = new Rect(condRect.xMax + 4, y, 20, lineH);

                string typeName = condElement.managedReferenceFullTypename.Split(' ').LastOrDefault();

                if (typeName == "FloatCondition")
                {
                    var pName = condElement.FindPropertyRelative("ParameterName");
                    var pOp = condElement.FindPropertyRelative("Operator");
                    var pThresh = condElement.FindPropertyRelative("Threshold");

                    Rect r1 = new Rect(condRect.x, y, condRect.width * 0.4f, lineH);
                    Rect r2 = new Rect(r1.xMax + 5, y, condRect.width * 0.3f - 5, lineH);
                    Rect r3 = new Rect(r2.xMax + 5, y, condRect.width * 0.3f - 5, lineH);

                    EditorGUI.PropertyField(r1, pName, GUIContent.none);
                    EditorGUI.PropertyField(r2, pOp, GUIContent.none);
                    EditorGUI.PropertyField(r3, pThresh, GUIContent.none);
                }
                else if (typeName == "BoolCondition")
                {
                    var pName = condElement.FindPropertyRelative("ParameterName");
                    var pVal = condElement.FindPropertyRelative("ExpectedValue");

                    Rect r1 = new Rect(condRect.x, y, condRect.width * 0.6f, lineH);
                    Rect r2 = new Rect(r1.xMax + 5, y, condRect.width * 0.4f - 5, lineH);

                    EditorGUI.PropertyField(r1, pName, GUIContent.none);
                    pVal.boolValue = EditorGUI.Popup(r2, pVal.boolValue ? 0 : 1, new string[] { "== True", "== False" }) == 0;
                }
                else if (typeName == "TriggerCondition")
                {
                    var pName = condElement.FindPropertyRelative("ParameterName");
                    EditorGUI.PropertyField(condRect, pName, GUIContent.none);
                }
                else if (typeName == "StateTimeCondition")
                {
                    var pTime = condElement.FindPropertyRelative("MinimumSecondsInState");
                    EditorGUIUtility.labelWidth = 110;
                    EditorGUI.PropertyField(condRect, pTime, new GUIContent("Time In State >="));
                    EditorGUIUtility.labelWidth = oldWidth;
                }
                else
                {
                    EditorGUI.LabelField(condRect, "Unknown Condition Type");
                }

                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUI.Button(condDelRect, "x"))
                {
                    condProp.DeleteArrayElementAtIndex(i);
                    if (i < condProp.arraySize && condProp.GetArrayElementAtIndex(i).managedReferenceValue == null)
                        condProp.DeleteArrayElementAtIndex(i);
                    GUI.backgroundColor = Color.white;
                    break; 
                }
                GUI.backgroundColor = Color.white;

                y += lineH + 6; // Extra padding to accommodate the subtle box
            }

            y += spacing;
            Rect addBtnRect = new Rect(position.x + position.width * 0.15f, y, position.width * 0.7f, lineH);
            if (GUI.Button(addBtnRect, "+ Add New Condition"))
            {
                ShowAddConditionMenu(condProp);
            }
            y += lineH + spacing;
        }
    }

    protected override float GetBodyHeight(SerializedProperty property)
    {
        float h = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 2; 
        var condProp = GetProp(property, "Conditions", "conditions");
        if (condProp != null)
        {
            h += (condProp.arraySize * (EditorGUIUtility.singleLineHeight + 6)); // Height logic matching the +6 spacing
        }
        h += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; 
        return h;
    }

    private void ShowAddConditionMenu(SerializedProperty condProp)
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Float Condition"), false, () => AddCondition(condProp, typeof(FloatCondition)));
        menu.AddItem(new GUIContent("Bool Condition"), false, () => AddCondition(condProp, typeof(BoolCondition)));
        menu.AddItem(new GUIContent("Trigger Condition"), false, () => AddCondition(condProp, typeof(TriggerCondition)));
        menu.AddItem(new GUIContent("State Time Condition"), false, () => AddCondition(condProp, typeof(StateTimeCondition)));
        menu.ShowAsContext();
    }

    private void AddCondition(SerializedProperty condProp, System.Type type)
    {
        condProp.serializedObject.Update();
        condProp.arraySize++;
        var newElement = condProp.GetArrayElementAtIndex(condProp.arraySize - 1);
        newElement.managedReferenceValue = System.Activator.CreateInstance(type);
        condProp.serializedObject.ApplyModifiedProperties();
    }
}

// ============================================================================
// 4. THE STATE DRAWER (Slate Blue Palette)
// ============================================================================

public class BaseAnimStateDrawer : BaseCollapsibleBlockDrawer
{
    // The master state container uses neutral slate greys
    protected override Color HeaderColor => new Color(0.16f, 0.18f, 0.20f, 1f); 
    protected override Color BodyColor => new Color(0.21f, 0.22f, 0.25f, 1f); 
    protected override Color ButtonColor => new Color(0.35f, 0.55f, 0.65f, 1f); // Muted Slate Blue Button
    protected override bool HasDeleteButton => true;

    protected override void DrawHeaderContent(Rect rect, SerializedProperty property)
    {
        var nameProp = property.FindPropertyRelative("StateName");
        var rootProp = property.FindPropertyRelative("ExtractRootMotion");

        Rect nameRect = new Rect(rect.x, rect.y, rect.width * 0.65f, rect.height);
        Rect rootRect = new Rect(nameRect.xMax + 5, rect.y, rect.width * 0.35f - 5, rect.height);
        
        float oldWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 40; 
        EditorGUI.PropertyField(nameRect, nameProp, new GUIContent("State"));
        
        EditorGUIUtility.labelWidth = 125; 
        EditorGUI.PropertyField(rootRect, rootProp, new GUIContent("Extract Root Motion"));
        EditorGUIUtility.labelWidth = oldWidth;
    }

    protected override void DrawBodyContent(Rect position, SerializedProperty property, ref float y, float lineH, float spacing)
    {
        float oldWidth = EditorGUIUtility.labelWidth;

        DrawMiddleSection(position, property, ref y, lineH, spacing, oldWidth);

        // --- MOTIONS LAYER (Teal / Sage Palette) ---
        var motionsProp = property.FindPropertyRelative("Motions");
        DrawCustomSubList(position, motionsProp, ref y, lineH, spacing, "Motions", 
            new Color(0.15f, 0.27f, 0.27f, 1f),  // Header: Deep Teal
            new Color(0.18f, 0.25f, 0.26f, 1f),  // Body: Desaturated Teal
            new Color(0.21f, 0.27f, 0.28f, 1f),  // Inner Items: Lighter Teal
            new Color(0.30f, 0.55f, 0.50f, 1f),  // Button: Muted Sage
            "+ Add New Motion", true);

        y += spacing * 2;

        // --- TRANSITIONS LAYER (Plum / Mauve Palette) ---
        var transProp = property.FindPropertyRelative("OutboundTransitions");
        DrawCustomSubList(position, transProp, ref y, lineH, spacing, "Transitions", 
            new Color(0.28f, 0.19f, 0.28f, 1f),  // Header: Deep Plum
            new Color(0.25f, 0.20f, 0.25f, 1f),  // Body: Desaturated Plum
            Color.clear,                         // (Inner handled natively by AnimTransitionDrawer)
            new Color(0.55f, 0.40f, 0.55f, 1f),  // Button: Muted Rose
            "+ Add New Transition", false);
    }

    private void DrawCustomSubList(Rect position, SerializedProperty listProp, ref float y, float lineH, float spacing, string title, Color headerCol, Color bodyCol, Color itemBgCol, Color btnCol, string addBtnText, bool drawElementsDirectly)
    {
        if (listProp == null) return;

        Rect headerRect = new Rect(position.x, y, position.width, lineH + 4);
        EditorGUI.DrawRect(headerRect, headerCol);

        Rect minRect = new Rect(position.x + 2, y + 2, 20f, lineH);
        GUI.backgroundColor = btnCol; 
        if (GUI.Button(minRect, listProp.isExpanded ? "-" : "+")) listProp.isExpanded = !listProp.isExpanded;
        GUI.backgroundColor = Color.white;

        EditorGUI.LabelField(new Rect(minRect.xMax + 5, y + 2, 200, lineH), title, EditorStyles.boldLabel);
        y += lineH + 4 + spacing;

        if (listProp.isExpanded)
        {
            // Pre-calculate the background height so we can draw it behind the elements
            float listBodyH = spacing;
            for (int i = 0; i < listProp.arraySize; i++) {
                listBodyH += EditorGUI.GetPropertyHeight(listProp.GetArrayElementAtIndex(i), true);
                if (drawElementsDirectly) listBodyH += 4 + spacing;
                else listBodyH += spacing;
            }
            listBodyH += spacing + lineH + spacing;
            
            Rect bodyBgRect = new Rect(position.x, y, position.width, listBodyH);
            EditorGUI.DrawRect(bodyBgRect, bodyCol);

            y += spacing;

            for (int i = 0; i < listProp.arraySize; i++)
            {
                var elemProp = listProp.GetArrayElementAtIndex(i);
                float elemH = EditorGUI.GetPropertyHeight(elemProp, true);

                if (drawElementsDirectly)
                {
                    // Draw a subtle highlighted box behind each individual motion
                    Rect itemBg = new Rect(position.x + 4, y, position.width - 8, elemH + 4);
                    EditorGUI.DrawRect(itemBg, itemBgCol);

                    Rect mRect = new Rect(position.x + 8, y + 2, position.width - 40, elemH);
                    Rect delRect = new Rect(mRect.xMax + 5, y + 2, 20, lineH);
                    
                    EditorGUI.PropertyField(mRect, elemProp, true);

                    GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                    if (GUI.Button(delRect, "x")) { listProp.DeleteArrayElementAtIndex(i); break; }
                    GUI.backgroundColor = Color.white;

                    y += elemH + 4 + spacing;
                }
                else
                {
                    // Transitions draw their own inner panels via AnimTransitionDrawer
                    Rect mRect = new Rect(position.x + 4, y, position.width - 8, elemH);
                    EditorGUI.PropertyField(mRect, elemProp, true);
                    y += elemH + spacing;
                }
            }

            y += spacing;
            if (GUI.Button(new Rect(position.x + position.width * 0.1f, y, position.width * 0.8f, lineH), addBtnText))
            {
                listProp.arraySize++;
                if (!drawElementsDirectly) 
                {
                    var newElem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                    var conds = GetProp(newElem, "Conditions", "conditions");
                    if (conds != null) conds.ClearArray();
                }
            }
            y += lineH + spacing;
        }
    }

    protected override float GetBodyHeight(SerializedProperty property)
    {
        float height = GetMiddleSectionHeight(property);
        var motionsProp = property.FindPropertyRelative("Motions");
        var transProp = property.FindPropertyRelative("OutboundTransitions");
        
        height += GetCustomListHeight(motionsProp, true);
        height += EditorGUIUtility.standardVerticalSpacing * 2;
        height += GetCustomListHeight(transProp, false);
        
        return height;
    }

    private float GetCustomListHeight(SerializedProperty listProp, bool drawElementsDirectly)
    {
        if (listProp == null) return 0f;
        float h = EditorGUIUtility.singleLineHeight + 4 + EditorGUIUtility.standardVerticalSpacing; 
        if (listProp.isExpanded)
        {
            h += EditorGUIUtility.standardVerticalSpacing; 
            for (int i = 0; i < listProp.arraySize; i++)
            {
                h += EditorGUI.GetPropertyHeight(listProp.GetArrayElementAtIndex(i), true);
                if (drawElementsDirectly) h += 4 + EditorGUIUtility.standardVerticalSpacing;
                else h += EditorGUIUtility.standardVerticalSpacing;
            }
            h += EditorGUIUtility.standardVerticalSpacing + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; 
        }
        return h;
    }

    protected virtual void DrawMiddleSection(Rect position, SerializedProperty property, ref float y, float lineH, float spacing, float oldWidth) { }
    protected virtual float GetMiddleSectionHeight(SerializedProperty property) { return 0f; }
}

[CustomPropertyDrawer(typeof(BlendTree1DState))]
public class BlendTree1DStateDrawer : BaseAnimStateDrawer
{
    protected override void DrawMiddleSection(Rect position, SerializedProperty property, ref float y, float lineH, float spacing, float oldWidth)
    {
        var paramProp = property.FindPropertyRelative("_parameterName");
        Rect paramRect = new Rect(position.x, y, position.width * 0.65f, lineH);
        
        EditorGUIUtility.labelWidth = 75; 
        EditorGUI.PropertyField(paramRect, paramProp, new GUIContent("Parameter"));
        EditorGUIUtility.labelWidth = oldWidth; 
        
        y += lineH + spacing * 2; 
    }

    protected override float GetMiddleSectionHeight(SerializedProperty property)
    {
        return EditorGUIUtility.singleLineHeight + (EditorGUIUtility.standardVerticalSpacing * 2);
    }
}

[CustomPropertyDrawer(typeof(BlendState2D))]
public class BlendState2DDrawer : BaseAnimStateDrawer
{
    protected override void DrawMiddleSection(Rect position, SerializedProperty property, ref float y, float lineH, float spacing, float oldWidth)
    {
        var paramXProp = property.FindPropertyRelative("_parameterX");
        var paramYProp = property.FindPropertyRelative("_parameterY");
        
        Rect paramXRect = new Rect(position.x, y, position.width * 0.5f - 2, lineH);
        Rect paramYRect = new Rect(position.x + position.width * 0.5f + 2, y, position.width * 0.5f - 2, lineH);
        
        EditorGUIUtility.labelWidth = 55; 
        EditorGUI.PropertyField(paramXRect, paramXProp, new GUIContent("Param X"));
        EditorGUI.PropertyField(paramYRect, paramYProp, new GUIContent("Param Y"));
        EditorGUIUtility.labelWidth = oldWidth; 
        
        y += lineH + spacing * 2; 
    }

    protected override float GetMiddleSectionHeight(SerializedProperty property)
    {
        return EditorGUIUtility.singleLineHeight + (EditorGUIUtility.standardVerticalSpacing * 2);
    }
}
#endif