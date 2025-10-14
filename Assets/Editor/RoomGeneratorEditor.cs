using UnityEngine;
using UnityEditor;


[CustomEditor(typeof(RoomGenerator))]
public class RoomGeneratorEditor : Editor
{

    public override void OnInspectorGUI()
    {

        DrawDefaultInspector();


        RoomGenerator roomGenerator = (RoomGenerator)target;


        EditorGUILayout.Space(10);


        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Generate New ROOM", GUILayout.Height(40)))
        {

            roomGenerator.GeneratePrefab();
        }
    }
}