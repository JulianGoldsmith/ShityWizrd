using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerMovementController))]
public class PlayerControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // Draw default inspector
        DrawDefaultInspector();

        PlayerMovementController controller = (PlayerMovementController)target;

        // Add a button
        if (GUILayout.Button("Auto-Assign Components"))
        {
            controller.AssignComponentsInEditor();

            // Mark scene as dirty so changes save
            EditorUtility.SetDirty(controller);
        }
    }
}