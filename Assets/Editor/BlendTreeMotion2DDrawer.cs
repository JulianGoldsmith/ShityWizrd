using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BlendTreeMotion2D))]
public class BlendTreeMotion2DDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        bool isFirstElement = property.propertyPath.EndsWith("[0]");

        // 4-Column Layout
        float clipWidth = position.width * 0.40f;
        float posWidth = position.width * 0.40f; // Split this for X and Y
        float speedWidth = position.width * 0.20f;

        if (isFirstElement)
        {
            Rect clipH = new Rect(position.x, position.y, clipWidth, EditorGUIUtility.singleLineHeight);
            Rect posH = new Rect(clipH.xMax + 5, position.y, posWidth - 5, EditorGUIUtility.singleLineHeight);
            Rect speedH = new Rect(posH.xMax + 5, position.y, speedWidth - 5, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(clipH, "Motion Clip", EditorStyles.boldLabel);
            EditorGUI.LabelField(posH, "Pos (X, Y)", EditorStyles.boldLabel);
            EditorGUI.LabelField(speedH, "Speed", EditorStyles.boldLabel);

            position.y += EditorGUIUtility.singleLineHeight + 2;
        }

        var clipProp = property.FindPropertyRelative("Clip");
        var posProp = property.FindPropertyRelative("Position");
        var speedProp = property.FindPropertyRelative("TimeScale");

        Rect clipRect = new Rect(position.x, position.y, clipWidth, EditorGUIUtility.singleLineHeight);
        Rect posRect = new Rect(clipRect.xMax + 5, position.y, posWidth - 5, EditorGUIUtility.singleLineHeight);
        Rect speedRect = new Rect(posRect.xMax + 5, position.y, speedWidth - 5, EditorGUIUtility.singleLineHeight);

        // Draw Fields
        EditorGUI.PropertyField(clipRect, clipProp, GUIContent.none);

        // Trick to draw the Vector2 nicely without labels
        EditorGUIUtility.labelWidth = 14f;
        EditorGUI.PropertyField(posRect, posProp, GUIContent.none);
        EditorGUIUtility.labelWidth = 0f; // Reset

        EditorGUI.PropertyField(speedRect, speedProp, GUIContent.none);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return property.propertyPath.EndsWith("[0]")
            ? EditorGUIUtility.singleLineHeight * 2 + 2
            : EditorGUIUtility.singleLineHeight;
    }
}