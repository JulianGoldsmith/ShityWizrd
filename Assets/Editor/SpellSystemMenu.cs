using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class SpellSystemMenu
{
    [MenuItem("SpellSystem/Open Master Dictionary")]
    public static void OpenMasterNodeDictionary()
    {
        string[] guids = AssetDatabase.FindAssets("t:MasterNodeDictionary");

        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            MasterNodeDictionary dict = AssetDatabase.LoadAssetAtPath<MasterNodeDictionary>(path);

            Selection.activeObject = dict;
            EditorGUIUtility.PingObject(dict);
        }
        else
        {
            Debug.LogWarning("Could not find a MasterNodeDictionary in the project! Please create one in your Assets folder.");
        }
    }

    [MenuItem("SpellSystem/MASS RE-BAKE ALL DICTIONARIES", false, 50)]
    public static void MassRebakeDictionaries()
    {
        if (!EditorUtility.DisplayDialog("Mass Re-Bake Dictionaries?",
            "This will rebuild both Master Dictionaries using ONLY the runes currently published in your Node Dictionary. It will remove all tombstones.\n\nWARNING: This will reassign all Network IDs and will BREAK any currently saved JSON spells!\n\nAre you completely sure?",
            "Yes, Compress & Re-Bake", "Cancel"))
        {
            return;
        }

        string[] nodeGuids = AssetDatabase.FindAssets("t:MasterNodeDictionary");
        string[] statusGuids = AssetDatabase.FindAssets("t:MasterStatusDictionary");

        if (nodeGuids.Length == 0 || statusGuids.Length == 0)
        {
            Debug.LogError("Cannot Re-Bake: Missing MasterNodeDictionary or MasterStatusDictionary in project.");
            return;
        }

        MasterNodeDictionary nodeDict = AssetDatabase.LoadAssetAtPath<MasterNodeDictionary>(AssetDatabase.GUIDToAssetPath(nodeGuids[0]));
        MasterStatusDictionary statusDict = AssetDatabase.LoadAssetAtPath<MasterStatusDictionary>(AssetDatabase.GUIDToAssetPath(statusGuids[0]));

        List<SpellNode> currentlyPublishedNodes = new List<SpellNode>();
        for (int i = 1; i < nodeDict.BakedNodes.Count; i++) 
        {
            if (nodeDict.BakedNodes[i] != null)
            {
                currentlyPublishedNodes.Add(nodeDict.BakedNodes[i]);
            }
        }

        currentlyPublishedNodes = currentlyPublishedNodes.OrderBy(n => n.name).ToList();

        nodeDict.BakedNodes = new List<SpellNode>() { null };
        statusDict.BakedStatuses = new List<EffectNode>() { null };

        int nodeCounter = 1;
        int statusCounter = 1;

        foreach (SpellNode node in currentlyPublishedNodes)
        {
            nodeDict.BakedNodes.Add(node);
            node.NetworkNodeID = (ushort)nodeCounter;
            nodeCounter++;

            if (node is EffectNode effectNode)
            {
                if (effectNode.Lifecycle != EffectLifecycle.Instant)
                {
                    statusDict.BakedStatuses.Add(effectNode);
                    effectNode.NetworkStatusID = (byte)statusCounter;
                    statusCounter++;
                }
                else
                {
                    effectNode.NetworkStatusID = 0;
                }
            }

            EditorUtility.SetDirty(node);
        }

        EditorUtility.SetDirty(nodeDict);
        EditorUtility.SetDirty(statusDict);
        AssetDatabase.SaveAssets();

        Debug.Log($"<b>[Spell System] Mass Re-Bake Complete!</b>\nCleaned out tombstones and re-baked {nodeCounter - 1} Nodes and {statusCounter - 1} Status Effects.");
    }

}