using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SpellNode), true)] // 'true' means this applies to all child classes too!
public class SpellNodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        SpellNode node = (SpellNode)target;

        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Dictionary Publishing", EditorStyles.boldLabel);

        string[] guids = AssetDatabase.FindAssets("t:MasterNodeDictionary");
        if (guids.Length == 0)
        {
            EditorGUILayout.HelpBox("Could not find a MasterNodeDictionary! Please create one.", MessageType.Error);
            return;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        MasterNodeDictionary dictionary = AssetDatabase.LoadAssetAtPath<MasterNodeDictionary>(path);
        if (dictionary == null) return;

        bool isPublished = dictionary.BakedNodes.Contains(node);

        if (isPublished)
        {
            GUI.enabled = false;
            GUILayout.Button($"Published to Dictionary (ID: {node.NetworkNodeID})", GUILayout.Height(40));
            GUI.enabled = true;
        }
        else
        {
            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Publish to Node Dictionary", GUILayout.Height(40)))
            {
                int emptySlot = -1;
                for (int i = 1; i < dictionary.BakedNodes.Count; i++)
                {
                    if (dictionary.BakedNodes[i] == null)
                    {
                        emptySlot = i;
                        break;
                    }
                }

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

                EditorUtility.SetDirty(dictionary);
                EditorUtility.SetDirty(node);
                AssetDatabase.SaveAssets();
            }
            GUI.backgroundColor = Color.white;
        }
    }
}