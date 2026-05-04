#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(XPBDPosAndRotSolver))]
public class XPBDPosAndRotSolverEditor : Editor
{
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
        SerializedProperty targetArmatureRoot = serializedObject.FindProperty("targetArmatureRoot");
        SerializedProperty joints = serializedObject.FindProperty("joints");

        EditorGUILayout.PropertyField(complianceCurve);
        EditorGUILayout.Space(5);
        EditorGUILayout.PropertyField(targetArmatureRoot, new GUIContent("Target Armature Root"));
        EditorGUILayout.Space(10);

        for (int i = 0; i < joints.arraySize; i++)
        {
            SerializedProperty joint = joints.GetArrayElementAtIndex(i);
            SerializedProperty isRagdollProp = joint.FindPropertyRelative("isRagdollJoint");
            
            // --- TINT THE BACKGROUND ---
            Color oldBg = GUI.backgroundColor;
            Color boxColor = Color.white;
            if (isRagdollProp != null)
            {
                // Vibrant Light Red if Ragdoll, Vibrant Light Green if Core
                ColorUtility.TryParseHtmlString(isRagdollProp.boolValue ? "#FF7F7F" : "#7FFF7F", out boxColor);
            }
            GUI.backgroundColor = boxColor;
            
            EditorGUILayout.BeginVertical("helpbox");
            GUI.backgroundColor = oldBg; 
            
            // --- DYNAMIC JOINT HEADER ---
            SerializedProperty parentProp = joint.FindPropertyRelative("parent");
            SerializedProperty childProp = joint.FindPropertyRelative("child");

            string parentName = parentProp.objectReferenceValue != null ? parentProp.objectReferenceValue.name : "Empty";
            string childName = childProp.objectReferenceValue != null ? childProp.objectReferenceValue.name : "Empty";
            string headerTitle = $"[{parentName} ➔ {childName}]";

            EditorGUILayout.BeginHorizontal();
            joint.isExpanded = EditorGUILayout.Foldout(joint.isExpanded, headerTitle, true, new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold });
            
            string minimizeIcon = joint.isExpanded ? "-" : "+";
            if (GUILayout.Button(minimizeIcon, GUILayout.Width(25))) joint.isExpanded = !joint.isExpanded;
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

                // --- TOP ROW BUTTONS: RAGDOLL, BAKE, MIRROR, AUTO-FILL ---
                EditorGUILayout.BeginHorizontal();
                if (isRagdollProp != null)
                {
                    EditorGUILayout.PropertyField(isRagdollProp, new GUIContent("Ragdoll"), GUILayout.Width(70));
                }
                
                GUILayout.FlexibleSpace(); 
                
                if (GUILayout.Button("Bake", GUILayout.Width(45)))
                {
                    serializedObject.ApplyModifiedProperties();
                    BakeIndividualJoint(solver, i);
                    serializedObject.Update();
                }

                if (GUILayout.Button("Align", GUILayout.Width(45)))
                {
                    serializedObject.ApplyModifiedProperties();
                    AlignIndividualJoint(solver, i);
                    serializedObject.Update();
                }

                if (GUILayout.Button("Mirror", GUILayout.Width(55)))
                {
                    serializedObject.ApplyModifiedProperties();
                    MirrorJoint(solver, i);
                    serializedObject.Update();
                }

                if (GUILayout.Button("Auto-Fill", GUILayout.Width(65)))
                {
                    PerformAutoFill(targetArmatureRoot, parentProp, childProp, joint);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);

                DrawConnectionRow("Parent", parentProp, joint.FindPropertyRelative("parentTarget"));
                DrawConnectionRow("Child", childProp, joint.FindPropertyRelative("childTarget"));

                EditorGUILayout.Space(5);

                // --- NEW: INFLUENCE & LEVER ARM GRID ---
                EditorGUILayout.BeginHorizontal();
                float oldLabel = EditorGUIUtility.labelWidth;
                
                EditorGUIUtility.labelWidth = 70;
                EditorGUILayout.PropertyField(joint.FindPropertyRelative("leverArmScale"), new GUIContent("Lever Arm"), GUILayout.MinWidth(120));
                
                GUILayout.Space(10);
                
                EditorGUIUtility.labelWidth = 75;
                EditorGUILayout.PropertyField(joint.FindPropertyRelative("parentRotationInfluence"), new GUIContent("Parent Inf"), GUILayout.MinWidth(120));
                
                EditorGUIUtility.labelWidth = oldLabel;
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(5);

                // --- DYNAMICS GRID ---
                DrawGridHeader("Constraint", "On", "Compliance", "Damping");
                
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
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Twist Axis", GUILayout.Width(80));
                    EditorGUILayout.PropertyField(joint.FindPropertyRelative("twistAxis"), GUIContent.none, GUILayout.Width(60));
                    GUILayout.Space(15);
                    EditorGUILayout.LabelField("Fwd Axis", GUILayout.Width(60));
                    EditorGUILayout.PropertyField(joint.FindPropertyRelative("forwardAxis"), GUIContent.none, GUILayout.Width(60));
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Space(5);

                    DrawGridHeaderLimits("Axis Limit", "Min Angle", "Max Angle");
                    DrawLimitRow("Twist", joint.FindPropertyRelative("twistLimits"));
                    DrawLimitRow("Swing 1 (U/D)", joint.FindPropertyRelative("swing1Limits"));
                    DrawLimitRow("Swing 2 (L/R)", joint.FindPropertyRelative("swing2Limits"));

                    EditorGUILayout.Space(5);
                    
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Draw Gizmo", GUILayout.Width(80));
                    joint.FindPropertyRelative("drawLimitGizmos").boolValue = EditorGUILayout.Toggle(joint.FindPropertyRelative("drawLimitGizmos").boolValue, GUILayout.Width(20));
                    GUILayout.Space(15);
                    
                    float oldLabelWidth = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.labelWidth = 75; 
                    EditorGUILayout.PropertyField(joint.FindPropertyRelative("gizmoSize"), new GUIContent("Gizmo Size"), GUILayout.MinWidth(120));
                    EditorGUIUtility.labelWidth = oldLabelWidth; 
                    
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        if (GUILayout.Button("Add New Joint", GUILayout.Height(25)))
        {
            serializedObject.ApplyModifiedProperties(); 
            Undo.RecordObject(solver, "Add New Joint");
            solver.joints.Add(new XPBDTestJoint()); 
            EditorUtility.SetDirty(solver);
            serializedObject.Update(); 
        }

        EditorGUILayout.Space(15);

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

        if (GUILayout.Button("Auto-Align Angular Limit Axes", buttonStyle, GUILayout.Height(30)))
        {
            Undo.RecordObject(solver, "Auto-Align Axes");
            solver.AutoAlignAllAxes();
            EditorUtility.SetDirty(solver);
            Debug.Log("[XPBD] Axes Auto-Aligned based on bone hierarchy!");
        }

        serializedObject.ApplyModifiedProperties();
    }

    // --- INDIVIDUAL BAKE LOGIC ---
    private void BakeIndividualJoint(XPBDPosAndRotSolver solver, int index)
    {
        XPBDTestJoint joint = solver.joints[index];

        if (joint.parent == null || joint.child == null || joint.parentTarget == null || joint.childTarget == null)
        {
            Debug.LogWarning($"[XPBD] Cannot bake joint. Ensure Parent, Child, and Targets are all assigned first.");
            return;
        }

        Undo.RecordObject(solver, "Bake Individual Joint");

        // 1. Bake Anchors
        Vector3 pivotWorld = joint.childTarget.position;
        joint.parentAnchorLocal = Quaternion.Inverse(joint.parent.rotation) * (pivotWorld - joint.parent.position);
        joint.childAnchorLocal = Quaternion.Inverse(joint.child.rotation) * (pivotWorld - joint.child.position);
        joint.bakedParentScale = joint.parent.transform.localScale;
        joint.bakedChildScale = joint.child.transform.localScale;

        // 2. Bake Rest Pose (This will now flawlessly extract the target bone's natural tilt)
        joint.BakeRestPose();

        EditorUtility.SetDirty(solver);
        Debug.Log($"[XPBD] Baked Anchors and Rest Pose for {joint.child.name}");
    }

    // --- INDIVIDUAL ALIGN LOGIC ---
    private void AlignIndividualJoint(XPBDPosAndRotSolver solver, int index)
    {
        XPBDTestJoint joint = solver.joints[index];
        Undo.RecordObject(solver, "Align Individual Joint");

        // Call the single, unified mathematical function from the solver!
        solver.AutoAlignJointAxis(joint);

        EditorUtility.SetDirty(solver);
        Debug.Log($"[XPBD] Axes Auto-Aligned for {(joint.child != null ? joint.child.name : "Joint")}");
    }

    // --- MIRROR LOGIC ---
    private void MirrorJoint(XPBDPosAndRotSolver solver, int index)
    {
        XPBDTestJoint source = solver.joints[index];
        XPBDTestJoint mirrored = new XPBDTestJoint();

        mirrored.isRagdollJoint = source.isRagdollJoint;
        mirrored.enablePosition = source.enablePosition;
        mirrored.enableRotation = source.enableRotation;
        mirrored.enableAngularLimits = source.enableAngularLimits;

        mirrored.distanceCompliance = source.distanceCompliance;
        mirrored.distanceDamping = source.distanceDamping;
        mirrored.muscleCompliance = source.muscleCompliance;
        mirrored.muscleDamping = source.muscleDamping;
        mirrored.limitCompliance = source.limitCompliance;
        mirrored.limitDamping = source.limitDamping;

        mirrored.drawLimitGizmos = source.drawLimitGizmos;
        mirrored.gizmoSize = source.gizmoSize;

        if (source.parent != null) mirrored.parent = FindMirroredRigidbody(source.parent);
        if (source.child != null) mirrored.child = FindMirroredRigidbody(source.child);
        if (source.parentTarget != null) mirrored.parentTarget = FindMirroredTransform(source.parentTarget);
        if (source.childTarget != null) mirrored.childTarget = FindMirroredTransform(source.childTarget);

        if (mirrored.parent != null && mirrored.child != null && mirrored.parentTarget != null && mirrored.childTarget != null)
        {
            Vector3 pivotWorld = mirrored.childTarget.position;
            mirrored.parentAnchorLocal = Quaternion.Inverse(mirrored.parent.rotation) * (pivotWorld - mirrored.parent.position);
            mirrored.childAnchorLocal = Quaternion.Inverse(mirrored.child.rotation) * (pivotWorld - mirrored.child.position);
            mirrored.bakedParentScale = mirrored.parent.transform.localScale;
            mirrored.bakedChildScale = mirrored.child.transform.localScale;
            
            mirrored.BakeRestPose(); 
            
            Vector3 mirrorNormal = solver.transform.right;

            Vector3 srcTwistWorld = source.child.rotation * source.GetAxisVector(source.twistAxis);
            Vector3 mirTwistWorld = srcTwistWorld - 2 * Vector3.Dot(srcTwistWorld, mirrorNormal) * mirrorNormal;
            Vector3 dstTwistLocal = Quaternion.Inverse(mirrored.child.rotation) * mirTwistWorld;
            mirrored.twistAxis = mirrored.GetClosestAxis(dstTwistLocal);

            Vector3 srcFwdWorld = source.child.rotation * source.GetAxisVector(source.forwardAxis);
            Vector3 mirFwdWorld = srcFwdWorld - 2 * Vector3.Dot(srcFwdWorld, mirrorNormal) * mirrorNormal;
            Vector3 dstFwdLocal = Quaternion.Inverse(mirrored.child.rotation) * mirFwdWorld;
            mirrored.forwardAxis = mirrored.GetClosestAxis(dstFwdLocal, true, mirrored.twistAxis);

            // --- THE NEW HANDEDNESS DETECTION ---
            mirrored.isMirroredBasis = source.isMirroredBasis; // Inherit
            source.RecalculateAxes();
            mirrored.RecalculateAxes();

            // Check if the pure reflection of Swing 1 matches the Right-Handed rebuilt Swing 1
            Vector3 sS1_World = source.parent.rotation * source.swing1AxisParent;
            Vector3 mS1_World = sS1_World - 2 * Vector3.Dot(sS1_World, mirrorNormal) * mirrorNormal;
            Vector3 dS1_World = mirrored.parent.rotation * mirrored.swing1AxisParent;
            
            // If they oppose each other, the Cross Product flipped it! Flag it as a Mirrored Basis.
            if (Vector3.Dot(mS1_World, dS1_World) < 0)
            {
                mirrored.isMirroredBasis = !mirrored.isMirroredBasis;
                mirrored.RecalculateAxes(); // Recalculate with the new flipped flag
            }

            // --- PARITY MAPPING (Still required because Unity Quaternions are Right-Handed) ---
            Vector3 sT_World = source.parent.rotation * source.twistAxisParent;
            Vector3 mT_World = sT_World - 2 * Vector3.Dot(sT_World, mirrorNormal) * mirrorNormal;
            Vector3 dT_World = mirrored.parent.rotation * mirrored.twistAxisParent;
            
            if (Vector3.Dot(mT_World, dT_World) > 0) mirrored.twistLimits = new Vector2(-source.twistLimits.y, -source.twistLimits.x);
            else mirrored.twistLimits = source.twistLimits;

            Vector3 mS1_World_Final = sS1_World - 2 * Vector3.Dot(sS1_World, mirrorNormal) * mirrorNormal;
            Vector3 dS1_World_Final = mirrored.parent.rotation * mirrored.swing1AxisParent;
            
            if (Vector3.Dot(mS1_World_Final, dS1_World_Final) > 0) mirrored.swing1Limits = new Vector2(-source.swing1Limits.y, -source.swing1Limits.x);
            else mirrored.swing1Limits = source.swing1Limits;

            Vector3 sS2_World = source.parent.rotation * source.swing2AxisParent;
            Vector3 mS2_World = sS2_World - 2 * Vector3.Dot(sS2_World, mirrorNormal) * mirrorNormal;
            Vector3 dS2_World = mirrored.parent.rotation * mirrored.swing2AxisParent;
            
            if (Vector3.Dot(mS2_World, dS2_World) > 0) mirrored.swing2Limits = new Vector2(-source.swing2Limits.y, -source.swing2Limits.x);
            else mirrored.swing2Limits = source.swing2Limits;
        }

        Undo.RecordObject(solver, "Mirror Joint");
        solver.joints.Add(mirrored);
        EditorUtility.SetDirty(solver);
    }

    private Rigidbody FindMirroredRigidbody(Rigidbody source)
    {
        string mirroredName = GetMirroredName(source.name);
        if (mirroredName == source.name) return source; // E.g., Spine stays Spine

        Transform root = source.transform.root;
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            if (rb.name.Trim() == mirroredName) return rb;
        }
        
        // THE FIX: Warn the user exactly what went wrong!
        Debug.LogWarning($"[XPBD Mirror] Could not find a Rigidbody named '{mirroredName}'. Falling back to '{source.name}'. Did you forget to add a Rigidbody component to the proxy?");
        return source; 
    }

    private Transform FindMirroredTransform(Transform source)
    {
        string mirroredName = GetMirroredName(source.name);
        if (mirroredName == source.name) return source;

        Transform root = source.transform.root;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.Trim() == mirroredName) return t;
        }
        
        Debug.LogWarning($"[XPBD Mirror] Could not find a Transform named '{mirroredName}'. Falling back to '{source.name}'.");
        return source; 
    }

    private string GetMirroredName(string name)
    {
        name = name.Trim(); 
        
        if (name.Contains(".L")) return name.Replace(".L", ".R");
        if (name.Contains(".R")) return name.Replace(".R", ".L");
        if (name.Contains("_L")) return name.Replace("_L", "_R");
        if (name.Contains("_R")) return name.Replace("_R", "_L");
        if (name.Contains("Left")) return name.Replace("Left", "Right");
        if (name.Contains("Right")) return name.Replace("Right", "Left");
        
        // Handle exact ends (e.g., ArmL -> ArmR)
        if (name.EndsWith("L")) return name.Substring(0, name.Length - 1) + "R";
        if (name.EndsWith("R")) return name.Substring(0, name.Length - 1) + "L";

        return name;
    }

    // --- AUTO FILL LOGIC ---

    private void PerformAutoFill(SerializedProperty rootProp, SerializedProperty parentProp, SerializedProperty childProp, SerializedProperty jointProp)
    {
        Transform armatureRoot = rootProp.objectReferenceValue as Transform;
        if (armatureRoot == null)
        {
            Debug.LogWarning("[XPBD Auto-Fill] Please assign the 'Target Armature Root' at the top of the component first!");
            return;
        }

        Rigidbody parentRb = parentProp.objectReferenceValue as Rigidbody;
        Rigidbody childRb = childProp.objectReferenceValue as Rigidbody;

        if (parentRb != null)
        {
            Transform foundP = FindChildFuzzy(armatureRoot, CleanName(parentRb.name));
            if (foundP != null) jointProp.FindPropertyRelative("parentTarget").objectReferenceValue = foundP;
        }
        if (childRb != null)
        {
            Transform foundC = FindChildFuzzy(armatureRoot, CleanName(childRb.name));
            if (foundC != null) jointProp.FindPropertyRelative("childTarget").objectReferenceValue = foundC;
        }
    }

    private string CleanName(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.ToLower().Replace(".", "").Replace("_", "").Replace(" ", "");
    }

    private Transform FindChildFuzzy(Transform root, string fuzzyName)
    {
        if (root == null || string.IsNullOrEmpty(fuzzyName)) return null;
        if (CleanName(root.name) == fuzzyName) return root;

        foreach (Transform child in root)
        {
            Transform found = FindChildFuzzy(child, fuzzyName);
            if (found != null) return found;
        }
        return null;
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

    private void DrawGridHeader(string col1, string col2, string col3, string col4)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(col1, EditorStyles.miniBoldLabel, GUILayout.Width(80));
        EditorGUILayout.LabelField(col2, EditorStyles.miniBoldLabel, GUILayout.Width(30));
        GUILayout.Space(15); 
        EditorGUILayout.LabelField(col3, EditorStyles.miniBoldLabel, GUILayout.MinWidth(60));
        EditorGUILayout.LabelField(col4, EditorStyles.miniBoldLabel, GUILayout.MinWidth(60));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawDynamicsRow(string label, SerializedProperty enable, SerializedProperty comp, SerializedProperty damp)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(80));
        enable.boolValue = EditorGUILayout.Toggle(enable.boolValue, GUILayout.Width(30));
        
        GUILayout.Space(40); 
        
        float oldLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 10; 
        
        comp.floatValue = EditorGUILayout.FloatField(" ", comp.floatValue, GUILayout.MinWidth(60));
        damp.floatValue = EditorGUILayout.FloatField(" ", damp.floatValue, GUILayout.MinWidth(60));
        
        EditorGUIUtility.labelWidth = oldLabelWidth;
        
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