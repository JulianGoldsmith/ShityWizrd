using System;
using UnityEngine;
using System.IO;
using Unity.VisualScripting;

#if UNITY_EDITOR
using UnityEditor; // Required for AssetDatabase and EditorUtility
#endif

[CreateAssetMenu(fileName = "NPCSpell", menuName = "NPCACtions/Spell")]
public class NPCActionSpell : NPCAction
{

    [Tooltip("populated by the 'Bake Spell From Name in context menu'")]
    public SpellGraph spell;

    [TextArea(4, 20)] public string bakedJson;

    [Tooltip("The name of the spell .json file to load from 'Assets/Scripts/SpellSystem/SavedSpells/'.")]
    public string spellName;

    public NetworkObjectBuffer networkObjectBuffer;

    [Header("HitBoxID - 1 is no hitbox")]
    public int hitBoxID = -1;
    public float timeAfterReleaseToActivateHitBox, hitBoxDuration; 

    public void LoadSpells(NetworkObjectBuffer nob)
    {
        name = spellName;
        networkObjectBuffer = nob;

        if (string.IsNullOrEmpty(bakedJson))
        {
            Debug.LogError($"No bakedJson found for '{spellName}'. Did you run Bake on the NPC?");
            return;
        }
        if (SpellGraphController.Instance == null)
        {
            Debug.LogError("Cannot load spell: SpellGraphController.Instance is not ready.");
            return;
        }

        var runtimeGraph = ScriptableObject.CreateInstance<SpellGraph>();
        runtimeGraph.name = $"Runtime_{spellName}";

        try
        {
            JsonUtility.FromJsonOverwrite(bakedJson, runtimeGraph);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to parse baked JSON for '{spellName}'. Error: {e.Message}");
            return;
        }

        runtimeGraph.InitilizeFromNodeData(SpellGraphController.Instance);
        SetAndInitialise(runtimeGraph);
    }

    void SetAndInitialise(SpellGraph graph)
    {

        spell = graph;
        if (spell != null)
        {
            SpellStateManager.instance.OnEquipSpellGraph(spell.spellGraphId, spell);
        }
    
        if (networkObjectBuffer != null)
            networkObjectBuffer.Initialise(spell);
    }

#if UNITY_EDITOR
    // Defines the root path for your JSON files
    private const string JsonRoot = "Assets/Scripts/SpellSystem/SavedSpells";

    [ContextMenu("Bake Spell JSON")]
    private void BakeSpellJSON()
    {
        if (string.IsNullOrEmpty(spellName))
        {
            Debug.LogWarning("Cannot bake: spellName is empty.", this);
            return;
        }

        string jsonPath = Path.Combine(JsonRoot, spellName + ".json").Replace("\\", "/");
        var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(jsonPath);
        
        if (textAsset == null)
        {
            Debug.LogError($"JSON not found at: {jsonPath}", this);
            bakedJson = null;
        }
        else
        {
            bakedJson = textAsset.text;
            // This "dirties" the asset, marking it as changed
            EditorUtility.SetDirty(this);
            // You can optionally save all assets immediately
            // AssetDatabase.SaveAssets(); 
            Debug.Log($"Successfully baked '{spellName}' JSON into {this.name}", this);
        }
    }
#endif
}

