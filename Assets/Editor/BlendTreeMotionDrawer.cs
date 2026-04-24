using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BlendTreeMotion1D))]
public class BlendTreeMotionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 1. Check if this is the very first element in the list
        bool isFirstElement = property.propertyPath.EndsWith("[0]");

        float clipWidth = position.width * 0.60f;
        float numWidth = position.width * 0.20f;

        // 2. If it is the first element, draw the Headers!
        if (isFirstElement)
        {
            Rect clipHeader = new Rect(position.x, position.y, clipWidth, EditorGUIUtility.singleLineHeight);
            Rect threshHeader = new Rect(clipHeader.xMax + 5, position.y, numWidth - 5, EditorGUIUtility.singleLineHeight);
            Rect speedHeader = new Rect(threshHeader.xMax + 5, position.y, numWidth - 5, EditorGUIUtility.singleLineHeight);

            // Draw bold text labels
            EditorGUI.LabelField(clipHeader, "Motion Clip", EditorStyles.boldLabel);
            EditorGUI.LabelField(threshHeader, "Threshold", EditorStyles.boldLabel);
            EditorGUI.LabelField(speedHeader, "Speed", EditorStyles.boldLabel);

            // Shift the starting Y position down so the fields don't overlap the text
            position.y += EditorGUIUtility.singleLineHeight + 2;
        }

        // 3. Fetch properties
        var clipProp = property.FindPropertyRelative("Clip");
        var threshProp = property.FindPropertyRelative("Threshold");
        var speedProp = property.FindPropertyRelative("TimeScale");

        // 4. Draw the actual data fields
        Rect clipRect = new Rect(position.x, position.y, clipWidth, EditorGUIUtility.singleLineHeight);
        Rect threshRect = new Rect(clipRect.xMax + 5, position.y, numWidth - 5, EditorGUIUtility.singleLineHeight);
        Rect speedRect = new Rect(threshRect.xMax + 5, position.y, numWidth - 5, EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(clipRect, clipProp, GUIContent.none);
        EditorGUI.PropertyField(threshRect, threshProp, GUIContent.none);
        EditorGUI.PropertyField(speedRect, speedProp, GUIContent.none);

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Give the standard 1-line height
        float height = EditorGUIUtility.singleLineHeight;

        // If it's the first element, we need to tell Unity to allocate double the height to fit the header!
        if (property.propertyPath.EndsWith("[0]"))
        {
            height += EditorGUIUtility.singleLineHeight + 2;
        }

        return height;
    }
}