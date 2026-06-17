using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(SpellNode), true)]
public class SpellNodeEditor : Editor
{
    // Core Properties
    private SerializedProperty nodeNameProp;
    private SerializedProperty descriptionProp;

    // Appearance Properties
    private SerializedProperty iconProp;
    private SerializedProperty overrideMeshProp;
    private SerializedProperty overrideMaterialProp;
    private SerializedProperty overrideVisualScaleProp;

    private bool showAppearance = true;

    private void OnEnable()
    {
        // Link all properties to the actual variable names in SpellNode.cs
        nodeNameProp = serializedObject.FindProperty("nodeName");
        descriptionProp = serializedObject.FindProperty("description");

        iconProp = serializedObject.FindProperty("icon");
        overrideMeshProp = serializedObject.FindProperty("overrideMesh");
        overrideMaterialProp = serializedObject.FindProperty("overrideMaterial");
        overrideVisualScaleProp = serializedObject.FindProperty("ovverideVisualScale");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SpellNode node = (SpellNode)target;

        // 1. THE BACK BUTTON
        GUI.backgroundColor = new Color(0.8f, 0.9f, 1f);
        if (GUILayout.Button("← Back to Rune Dictionary", GUILayout.Height(35)))
        {
            ReturnToDictionary();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space(10);

        // 2. IDENTITY (Name & Description) + AUTO-RENAMING
        EditorGUI.BeginChangeCheck(); // Start listening for typing
        EditorGUILayout.PropertyField(nodeNameProp);

        if (EditorGUI.EndChangeCheck()) // If the name changed this frame
        {
            serializedObject.ApplyModifiedProperties(); // Save the text field to the object

            string newName = nodeNameProp.stringValue;
            if (!string.IsNullOrWhiteSpace(newName))
            {
                string assetPath = AssetDatabase.GetAssetPath(target);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    // Physically rename the file in the project folder!
                    AssetDatabase.RenameAsset(assetPath, newName);
                    AssetDatabase.SaveAssets();
                }
            }
        }

        EditorGUILayout.PropertyField(descriptionProp);
        EditorGUILayout.Space(5);

        // 3. APPEARANCE FOLDOUT
        EditorGUILayout.BeginVertical("box");
        showAppearance = EditorGUILayout.Foldout(showAppearance, "Appearance Settings", true, EditorStyles.foldoutHeader);
        if (showAppearance)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(iconProp);
            EditorGUILayout.PropertyField(overrideMeshProp);
            EditorGUILayout.PropertyField(overrideMaterialProp);
            EditorGUILayout.PropertyField(overrideVisualScaleProp);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // 4. DRAW THE REST OF THE GAMEPLAY PROPERTIES
        // We add "nodeName" and "description" to the exclusion list so they don't draw twice!
        DrawPropertiesExcluding(serializedObject,
            "m_Script",
            "nodeName",
            "description",
            "icon",
            "overrideMesh",
            "overrideMaterial",
            "ovverideVisualScale",
            "NetworkNodeID",
            "NetworkStatusID");

        serializedObject.ApplyModifiedProperties();

        // ---------------------------------------------------------
        // 5. DICTIONARY PUBLISHING LOGIC
        // ---------------------------------------------------------
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Dictionary Publishing", EditorStyles.boldLabel);

        // 1. Load Both Dictionaries
        string[] nodeGuids = AssetDatabase.FindAssets("t:MasterNodeDictionary");
        string[] statusGuids = AssetDatabase.FindAssets("t:MasterStatusDictionary");

        if (nodeGuids.Length == 0 || statusGuids.Length == 0)
        {
            EditorGUILayout.HelpBox("Missing Master Dictionaries! Ensure both MasterNodeDictionary and MasterStatusDictionary exist.", MessageType.Error);
            return;
        }

        MasterNodeDictionary nodeDict = AssetDatabase.LoadAssetAtPath<MasterNodeDictionary>(AssetDatabase.GUIDToAssetPath(nodeGuids[0]));
        MasterStatusDictionary statusDict = AssetDatabase.LoadAssetAtPath<MasterStatusDictionary>(AssetDatabase.GUIDToAssetPath(statusGuids[0]));

        if (nodeDict == null || statusDict == null) return;

        // 2. Check States
        bool isNodePublished = nodeDict.BakedNodes.Contains(node);
        EffectNode effectNode = node as EffectNode;
        bool isStatusEffect = effectNode != null && effectNode.Lifecycle != EffectLifecycle.Instant;
        bool isStatusPublished = isStatusEffect && statusDict.BakedStatuses.Contains(effectNode);

        // 3. Draw the Status Readout
        if (isNodePublished)
        {
            GUI.enabled = false;
            string label = $"Published (Node ID: {node.NetworkNodeID})";
            if (isStatusEffect && isStatusPublished) label += $" | (Status ID: {effectNode.NetworkStatusID})";

            GUILayout.Button(label, GUILayout.Height(40));
            GUI.enabled = true;
        }

        // 4. THE SMART PUBLISH BUTTON
        bool needsPublishing = !isNodePublished || (isStatusEffect && !isStatusPublished);

        if (needsPublishing)
        {
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); // Red
            if (GUILayout.Button("Publish to Dictionary Network", GUILayout.Height(40)))
            {
                // --- PUBLISH TO NODE DICTIONARY (Always happens) ---
                if (!isNodePublished)
                {
                    int emptyNodeSlot = FindEmptySlot(nodeDict.BakedNodes);
                    if (emptyNodeSlot != -1)
                    {
                        nodeDict.BakedNodes[emptyNodeSlot] = node;
                        node.NetworkNodeID = (ushort)emptyNodeSlot;
                    }
                    else
                    {
                        nodeDict.BakedNodes.Add(node);
                        node.NetworkNodeID = (ushort)(nodeDict.BakedNodes.Count - 1);
                    }
                    EditorUtility.SetDirty(nodeDict);
                }

                // --- SMART STATUS DICTIONARY LOGIC ---
                if (effectNode != null)
                {
                    if (isStatusEffect && !isStatusPublished)
                    {
                        // Add to Status Dictionary
                        int emptyStatusSlot = FindEmptySlot(statusDict.BakedStatuses);
                        if (emptyStatusSlot != -1)
                        {
                            statusDict.BakedStatuses[emptyStatusSlot] = effectNode;
                            effectNode.NetworkStatusID = (byte)emptyStatusSlot;
                        }
                        else
                        {
                            statusDict.BakedStatuses.Add(effectNode);
                            effectNode.NetworkStatusID = (byte)(statusDict.BakedStatuses.Count - 1);
                        }
                        EditorUtility.SetDirty(statusDict);
                    }
                    else if (!isStatusEffect && statusDict.BakedStatuses.Contains(effectNode))
                    {
                        // Designer changed it back to Instant! Safely Tombstone the old status ID.
                        int index = statusDict.BakedStatuses.IndexOf(effectNode);
                        statusDict.BakedStatuses[index] = null;
                        effectNode.NetworkStatusID = 0;
                        EditorUtility.SetDirty(statusDict);
                        Debug.Log($"Removed {effectNode.name} from Status Dictionary because it was changed to Instant.");
                    }
                }

                EditorUtility.SetDirty(node);
                AssetDatabase.SaveAssets();
            }
            GUI.backgroundColor = Color.white;
        }
    }

    // Helper function to find tombstones
    private int FindEmptySlot<T>(List<T> list) where T : class
    {
        for (int i = 1; i < list.Count; i++) // Skip index 0
        {
            if (list[i] == null) return i;
        }
        return -1;
    }

    private void ReturnToDictionary()
    {
        string[] guids = AssetDatabase.FindAssets("t:MasterNodeDictionary");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            MasterNodeDictionary dict = AssetDatabase.LoadAssetAtPath<MasterNodeDictionary>(path);
            Selection.activeObject = dict;
            EditorGUIUtility.PingObject(dict);
        }
    }
}