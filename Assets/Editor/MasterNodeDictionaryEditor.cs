using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MasterNodeDictionary))]
public class MasterNodeDictionaryEditor : Editor
{
    // Your defined color standard (using pastel variants so text remains readable)
    private readonly Color coreColor = new Color(0.6f, 0.8f, 1f);      // Blue
    private readonly Color behaviourColor = new Color(0.8f, 0.6f, 1f); // Purple
    private readonly Color triggerColor = new Color(0.6f, 1f, 0.6f);   // Green
    private readonly Color effectColor = new Color(1f, 0.6f, 0.6f);    // Red (Standardized for Effects)
    private readonly Color valueColor = new Color(1f, 1f, 0.6f);       // Yellow

    public override void OnInspectorGUI()
    {
        MasterNodeDictionary dict = (MasterNodeDictionary)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("The Rune Dictionary", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("This is all of the runes that are ID'd over the network", MessageType.Info);
        EditorGUILayout.Space(10);

        DrawCategory<CoreNode>("Spawn Cores", coreColor, dict);
        DrawCategory<BehaviourNode>("Behaviours", behaviourColor, dict);
        DrawCategory<TriggerNode>("Triggers", triggerColor, dict);
        DrawCategory<EffectNode>("Effects", effectColor, dict);
        DrawCategory<ValueNode>("Values", valueColor, dict);

        EditorGUILayout.Space(20);
        if (GUILayout.Button("Clean Up Trailing Tombstones", GUILayout.Height(30)))
        {
            CleanTrailingNulls(dict);
        }

        if (GUI.changed)
        {
            EditorUtility.SetDirty(dict);
        }
    }

    private void DrawCategory<T>(string header, Color rowColor, MasterNodeDictionary dict) where T : SpellNode
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(header, EditorStyles.boldLabel);

        for (int i = 1; i < dict.BakedNodes.Count; i++) 
        {
            var node = dict.BakedNodes[i];

            if (node == null || !(node is T)) continue;

            GUI.backgroundColor = rowColor;
            GUILayout.BeginHorizontal("box");
            GUI.backgroundColor = Color.white; 

            GUILayout.Label($"[{i}] {node.nodeName}", GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Edit", GUILayout.Width(60)))
            {
                EditorGUIUtility.PingObject(node);
                Selection.activeObject = node;
            }

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f); 
            if (GUILayout.Button("X", GUILayout.Width(30)))
            {
                if (EditorUtility.DisplayDialog("Tombstone Rune?", $"Are you sure you want to remove '{node.nodeName}'?\n\nThis will safely leave an empty slot to protect Network IDs.", "Yes", "Cancel"))
                {
                    dict.BakedNodes[i] = null;
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button($"+ Add New {header}...", EditorStyles.miniButton))
        {
            ShowAddMenu<T>(dict);
        }
    }

    private void ShowAddMenu<T>(MasterNodeDictionary dict) where T : SpellNode
    {
        GenericMenu menu = new GenericMenu();
        var types = TypeCache.GetTypesDerivedFrom<T>();

        foreach (var type in types)
        {
            if (type.IsAbstract) continue; 
            menu.AddItem(new GUIContent(type.Name), false, () => CreateNewNode(type, dict));
        }

        menu.ShowAsContext();
    }

    private void CreateNewNode(Type nodeType, MasterNodeDictionary dict)
    {
        int newId = -1;
        for (int i = 1; i < dict.BakedNodes.Count; i++)
        {
            if (dict.BakedNodes[i] == null)
            {
                newId = i;
                break;
            }
        }

        if (newId == -1)
        {
            dict.BakedNodes.Add(null);
            newId = dict.BakedNodes.Count - 1;
        }

        string subFolder = "Misc";
        if (typeof(CoreNode).IsAssignableFrom(nodeType)) subFolder = "Cores";
        else if (typeof(BehaviourNode).IsAssignableFrom(nodeType)) subFolder = "Behaviours";
        else if (typeof(TriggerNode).IsAssignableFrom(nodeType)) subFolder = "Triggers";
        else if (typeof(EffectNode).IsAssignableFrom(nodeType)) subFolder = "Effects";
        else if (typeof(ValueNode).IsAssignableFrom(nodeType)) subFolder = "Values";

        if (!AssetDatabase.IsValidFolder("Assets/SpellNodes"))
            AssetDatabase.CreateFolder("Assets", "SpellNodes");

        string targetFolderPath = $"Assets/SpellNodes/{subFolder}";
        if (!AssetDatabase.IsValidFolder(targetFolderPath))
            AssetDatabase.CreateFolder("Assets/SpellNodes", subFolder);

        SpellNode newNode = (SpellNode)CreateInstance(nodeType);
        newNode.nodeName = $"New {nodeType.Name}";
        newNode.NetworkNodeID = (ushort)newId;

        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{targetFolderPath}/{newNode.nodeName}.asset");

        AssetDatabase.CreateAsset(newNode, assetPath);
        AssetDatabase.SaveAssets();

        dict.BakedNodes[newId] = newNode;
        EditorUtility.SetDirty(dict);

        EditorGUIUtility.PingObject(newNode);
        Selection.activeObject = newNode;
    }

    private void CleanTrailingNulls(MasterNodeDictionary dict)
    {
        for (int i = dict.BakedNodes.Count - 1; i > 0; i--)
        {
            if (dict.BakedNodes[i] == null) dict.BakedNodes.RemoveAt(i);
            else break;
        }
        EditorUtility.SetDirty(dict);
    }
}