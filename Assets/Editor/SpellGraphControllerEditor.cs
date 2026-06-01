using UnityEngine;
using UnityEditor;
using System.Collections.Generic; // <-- Added to support the List<> logic


[CustomEditor(typeof(SpellGraphController))]
public class SpellGraphControllerEditor : Editor
{
    private string saveSpellName = "SpellName";
    private string loadSpellName = "SpellName";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SpellGraphController controller = (SpellGraphController)target;

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Save & Load Spells", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("Save Current Graph", EditorStyles.miniBoldLabel);
        saveSpellName = EditorGUILayout.TextField("Save Name", saveSpellName);

        if (GUILayout.Button("Save Spell"))
        {
            if (!string.IsNullOrEmpty(saveSpellName))
            {
                controller.SaveSpellToAssets(saveSpellName);
            }
            else
            {
                Debug.LogError("Save Spell Name cannot be empty.");
            }
        }

        EditorGUILayout.Space(10);

        EditorGUILayout.LabelField("Load Graph from File", EditorStyles.miniBoldLabel);
        loadSpellName = EditorGUILayout.TextField("Load Name", loadSpellName);

        if (GUILayout.Button("Load Spell Data to current item"))
        {
            if (!string.IsNullOrEmpty(loadSpellName))
            {
                controller.LoadSpellByNameToCurrentItem(loadSpellName);
            }
            else
            {
                Debug.LogError("Load Spell Name cannot be empty.");
            }
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("New Spell To Current Item"))
        {
            controller.ClearAndCreateNewSpellOnActiveItem();
        }

        // --- NEW: ARCHITECTURE MIGRATION TOOLS ---
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Architecture Migration Tools", EditorStyles.boldLabel);

        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("MASS PUBLISH: Add All Nodes to Master Dictionary", GUILayout.Height(40)))
        {
            MassPublishNodes(controller);
        }
        GUI.backgroundColor = Color.white; // Reset color so it doesn't tint everything else
    }

    private void MassPublishNodes(SpellGraphController controller)
    {
        // 1. Find the Master Dictionary
        string[] guids = AssetDatabase.FindAssets("t:MasterNodeDictionary");
        if (guids.Length == 0)
        {
            Debug.LogError("Could not find a MasterNodeDictionary! Please create one.");
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        MasterNodeDictionary dictionary = AssetDatabase.LoadAssetAtPath<MasterNodeDictionary>(path);

        if (dictionary == null) return;

        // 2. Gather every single node from the controller's manual lists
        List<SpellNode> allNodes = new List<SpellNode>();

        if (controller.availableCasterNodes != null) allNodes.AddRange(controller.availableCasterNodes);
        if (controller.availableCoreNodes != null) allNodes.AddRange(controller.availableCoreNodes);
        if (controller.availableBehaviourNodes != null) allNodes.AddRange(controller.availableBehaviourNodes);
        if (controller.availableTriggerNodes != null) allNodes.AddRange(controller.availableTriggerNodes);
        if (controller.availableFilterNodes != null) allNodes.AddRange(controller.availableFilterNodes);
        if (controller.availableEffectNodes != null) allNodes.AddRange(controller.availableEffectNodes);
        if (controller.availableValueNodes != null) allNodes.AddRange(controller.availableValueNodes);
        if (controller.availableSubgraphNodes != null) allNodes.AddRange(controller.availableSubgraphNodes);
        if (controller.entryPointTemplate != null) allNodes.Add(controller.entryPointTemplate);

        int addedCount = 0;

        // 3. Loop through and run the publishing logic!
        foreach (SpellNode node in allNodes)
        {
            if (node == null) continue;

            // Skip nodes that are already in the dictionary
            if (dictionary.BakedNodes.Contains(node)) continue;

            // Find a tombstoned (null) slot starting at Index 1
            int emptySlot = -1;
            for (int i = 1; i < dictionary.BakedNodes.Count; i++)
            {
                if (dictionary.BakedNodes[i] == null)
                {
                    emptySlot = i;
                    break;
                }
            }

            // Assign the ID
            if (emptySlot != -1)
            {
                dictionary.BakedNodes[emptySlot] = node;
                node.NetworkNodeID = (ushort)emptySlot;
            }
            else
            {
                dictionary.BakedNodes.Add(node);
                node.NetworkNodeID = (ushort)(dictionary.BakedNodes.Count - 1);
            }

            // Tell Unity we changed the Node asset so it saves the new ID to the disk
            EditorUtility.SetDirty(node);
            addedCount++;
        }

        // 4. Save the Dictionary
        if (addedCount > 0)
        {
            EditorUtility.SetDirty(dictionary);
            AssetDatabase.SaveAssets();
            Debug.Log($"<color=green>[Migration Success]</color> Found and published {addedCount} unlinked nodes to the Master Dictionary!");
        }
        else
        {
            Debug.Log($"<color=yellow>[Migration]</color> Scanned all lists. Everything is already published!");
        }
    }
}