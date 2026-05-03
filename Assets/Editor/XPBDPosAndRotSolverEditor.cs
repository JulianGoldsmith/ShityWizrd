#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(XPBDPosAndRotSolver))]
public class XPBDPosAndRotSolverEditor : Editor
{
    // Store UI state strictly in the Editor script, not in the game data!
    private Dictionary<int, bool> expandLimitsMap = new Dictionary<int, bool>();

    private bool IsLimitsExpanded(int index)
    {
        if (!expandLimitsMap.ContainsKey(index)) expandLimitsMap[index] = false;
        return expandLimitsMap[index];
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        XPBDPosAndRotSolver solver = (XPBDPosAndRotSolver)target;
        SerializedProperty complianceCurve = serializedObject.FindProperty("complianceCurve");
        SerializedProperty joints = serializedObject.FindProperty("joints");

        EditorGUILayout.PropertyField(complianceCurve);
        EditorGUILayout.Space(10);

        for (int i = 0; i < joints.arraySize; i++)
        {
            SerializedProperty joint = joints.GetArrayElementAtIndex(i);
            
            EditorGUILayout.BeginVertical("helpbox");
            
            // --- DYNAMIC JOINT HEADER ---
            SerializedProperty parentProp = joint.FindPropertyRelative("parent");
            SerializedProperty childProp = joint.FindPropertyRelative("child");

            string parentName = parentProp.objectReferenceValue != null ? parentProp.objectReferenceValue.name : "Empty";
            string childName = childProp.objectReferenceValue != null ? childProp.objectReferenceValue.name : "Empty";
            string headerTitle = $"[{parentName} ➔ {childName}]";

            EditorGUILayout.BeginHorizontal();
            
            // 1. FIX: Simple LabelField removes the unwanted automatic Unity foldout arrow
            EditorGUILayout.LabelField(headerTitle, EditorStyles.boldLabel);
            
            string minimizeIcon = joint.isExpanded ? "-" : "+";
            if (GUILayout.Button(minimizeIcon, GUILayout.Width(25)))
            {
                joint.isExpanded = !joint.isExpanded;
            }

            if (GUILayout.Button("X", GUILayout.Width(25)))
            {
                joints.DeleteArrayElementAtIndex(i);
                break; 
            }
            EditorGUILayout.EndHorizontal();

            // --- DRAW BODY ONLY IF EXPANDED ---
            if (joint.isExpanded)
            {
                EditorGUILayout.Space(5);

                DrawConnectionRow("Parent", parentProp, joint.FindPropertyRelative("parentTarget"));
                DrawConnectionRow("Child", childProp, joint.FindPropertyRelative("childTarget"));

                EditorGUILayout.Space(5);

                // 2. FIX: Added the 5th explicit "Show" column
                DrawGridHeader("Constraint", "On", "Show", "Compliance", "Damping");
                
                DrawDynamicsRow("Position", 
                    joint.FindPropertyRelative("enablePosition"), 
                    joint.FindPropertyRelative("distanceCompliance"), 
                    joint.FindPropertyRelative("distanceDamping"));

                DrawDynamicsRow("Rotation", 
                    joint.FindPropertyRelative("enableRotation"), 
                    joint.FindPropertyRelative("muscleCompliance"), 
                    joint.FindPropertyRelative("muscleDamping"));

                SerializedProperty enableLimits = joint.FindPropertyRelative("enableAngularLimits");
                bool expandLimits = DrawLimitsDynamicsRow(i, enableLimits, 
                    joint.FindPropertyRelative("limitCompliance"), 
                    joint.FindPropertyRelative("limitDamping"));

                // --- SECTION 3 & 4: ANGULAR LIMITS ---
                if (expandLimits)
                {
                    EditorGUILayout.Space(5);
                    
                    // Axis Row
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Twist Axis", GUILayout.Width(80));
                    EditorGUILayout.PropertyField(joint.FindPropertyRelative("twistAxis"), GUIContent.none, GUILayout.Width(60));
                    GUILayout.Space(15);
                    EditorGUILayout.LabelField("Fwd Axis", GUILayout.Width(60));
                    EditorGUILayout.PropertyField(joint.FindPropertyRelative("forwardAxis"), GUIContent.none, GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(5);

                    // Angles Grid
                    DrawGridHeaderLimits("Axis Limit", "Min Angle", "Max Angle");
                    DrawLimitRow("Twist", joint.FindPropertyRelative("twistLimits"));
                    DrawLimitRow("Swing 1 (U/D)", joint.FindPropertyRelative("swing1Limits"));
                    DrawLimitRow("Swing 2 (L/R)", joint.FindPropertyRelative("swing2Limits"));

                    EditorGUILayout.Space(5);
                    
                    // Gizmo Row
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Draw Gizmo", GUILayout.Width(80));
                    joint.FindPropertyRelative("drawLimitGizmos").boolValue = EditorGUILayout.Toggle(joint.FindPropertyRelative("drawLimitGizmos").boolValue, GUILayout.Width(20));
                    GUILayout.Space(15);
                    
                    // FIX: We re-attach the label to the PropertyField to restore the drag zone, 
                    // and temporarily shrink the label width so it fits our custom grid perfectly!
                    float oldLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 75; 
                    EditorGUILayout.PropertyField(joint.FindPropertyRelative("gizmoSize"), new GUIContent("Gizmo Size"), GUILayout.MinWidth(120));
                    EditorGUIUtility.labelWidth = oldLabelWidth; // Reset it immediately 
                    
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        if (GUILayout.Button("Add New Joint", GUILayout.Height(25)))
        {
            joints.arraySize++;
        }

        EditorGUILayout.Space(15);

        // --- BAKING BUTTON ---
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
        if (GUILayout.Button("Bake Joint Anchors & Rest Pose", buttonStyle, GUILayout.Height(30)))
        {
            Undo.RecordObject(solver, "Bake Rig");
            solver.BakeJointsFromTargets();
            foreach (var j in solver.joints) j.BakeRestPose();
            EditorUtility.SetDirty(solver);
            Debug.Log("[XPBD] Anchors and Rest Poses Baked Successfully!");
        }
        EditorGUILayout.Space(5);

        // --- AUTO ALIGN BUTTON ---
        GUIStyle autoButtonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
        if (GUILayout.Button("Auto-Align Angular Limit Axes", autoButtonStyle, GUILayout.Height(30)))
        {
            Undo.RecordObject(solver, "Auto-Align Axes");
            solver.AutoAlignAllAxes();
            EditorUtility.SetDirty(solver);
            Debug.Log("[XPBD] Axes Auto-Aligned based on bone hierarchy!");
        }
        serializedObject.ApplyModifiedProperties();
    }

    // --- HELPER DRAWING METHODS ---

    private void DrawConnectionRow(string label, SerializedProperty rb, SerializedProperty target)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(50));
        EditorGUILayout.PropertyField(rb, GUIContent.none, GUILayout.MinWidth(80));
        EditorGUILayout.LabelField("Target", GUILayout.Width(45));
        EditorGUILayout.PropertyField(target, GUIContent.none, GUILayout.MinWidth(80));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawGridHeader(string col1, string col2, string col3, string col4, string col5)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(col1, EditorStyles.miniBoldLabel, GUILayout.Width(80));
        EditorGUILayout.LabelField(col2, EditorStyles.miniBoldLabel, GUILayout.Width(30));
        EditorGUILayout.LabelField(col3, EditorStyles.miniBoldLabel, GUILayout.Width(40)); // Show Column
        EditorGUILayout.LabelField(col4, EditorStyles.miniBoldLabel, GUILayout.MinWidth(60));
        EditorGUILayout.LabelField(col5, EditorStyles.miniBoldLabel, GUILayout.MinWidth(60));
        EditorGUILayout.EndHorizontal();
    }

   private void DrawDynamicsRow(string label, SerializedProperty enable, SerializedProperty comp, SerializedProperty damp)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(80));
        enable.boolValue = EditorGUILayout.Toggle(enable.boolValue, GUILayout.Width(30));
        
        GUILayout.Space(40); 
        
        // TRICK: Create an invisible 10px drag handle just inside the float box
        float oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 10; 
        
        comp.floatValue = EditorGUILayout.FloatField(" ", comp.floatValue, GUILayout.MinWidth(60));
        damp.floatValue = EditorGUILayout.FloatField(" ", damp.floatValue, GUILayout.MinWidth(60));
        
        EditorGUIUtility.labelWidth = oldLabelWidth; // Reset immediately
        
        EditorGUILayout.EndHorizontal();
    }

    private bool DrawLimitsDynamicsRow(int index, SerializedProperty enable, SerializedProperty comp, SerializedProperty damp)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Limits", GUILayout.Width(80));
        enable.boolValue = EditorGUILayout.Toggle(enable.boolValue, GUILayout.Width(30));
        
        bool isExpanded = IsLimitsExpanded(index);
        isExpanded = GUILayout.Toggle(isExpanded, "", EditorStyles.foldout, GUILayout.Width(40));
        expandLimitsMap[index] = isExpanded;

        // TRICK: Create an invisible 10px drag handle just inside the float box
        float oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 10; 

        comp.floatValue = EditorGUILayout.FloatField(" ", comp.floatValue, GUILayout.MinWidth(60));
        damp.floatValue = EditorGUILayout.FloatField(" ", damp.floatValue, GUILayout.MinWidth(60));
        
        EditorGUIUtility.labelWidth = oldLabelWidth;
        
        EditorGUILayout.EndHorizontal();

        return isExpanded;
    }

    private void DrawGridHeaderLimits(string col1, string col2, string col3)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(col1, EditorStyles.miniBoldLabel, GUILayout.Width(110));
        EditorGUILayout.LabelField(col2, EditorStyles.miniBoldLabel, GUILayout.MinWidth(60));
        EditorGUILayout.LabelField(col3, EditorStyles.miniBoldLabel, GUILayout.MinWidth(60));
        EditorGUILayout.EndHorizontal();
    }

   private void DrawLimitRow(string label, SerializedProperty limitVector)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(110)); 
        
        // TRICK: Create an invisible 10px drag handle just inside the float box
        float oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 10; 

        Vector2 val = limitVector.vector2Value;
        val.x = EditorGUILayout.FloatField(" ", val.x, GUILayout.MinWidth(60));
        val.y = EditorGUILayout.FloatField(" ", val.y, GUILayout.MinWidth(60));
        limitVector.vector2Value = val;
        
        EditorGUIUtility.labelWidth = oldLabelWidth;
        
        EditorGUILayout.EndHorizontal();
    }
}
#endif