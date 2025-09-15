using UnityEngine;
using UnityEditor; 


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
    }
}