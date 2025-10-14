using UnityEngine;
using UnityEditor; 


[CustomEditor(typeof(LevelGenerator))]
public class LevelGeneratorEditor : Editor
{

    public override void OnInspectorGUI()
    {

        DrawDefaultInspector();


        LevelGenerator levelGenerator = (LevelGenerator)target;


        EditorGUILayout.Space(10);


        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate New Level", GUILayout.Height(40)))
        {

            levelGenerator.StartGeneration();
        }

        GUI.enabled = levelGenerator.IsGenerating;
        Color originalColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("Stop Generation", GUILayout.Height(30)))
        {
            levelGenerator.StopGeneration();
        }

        GUI.backgroundColor = originalColor;
        GUI.enabled = true;
    }
}