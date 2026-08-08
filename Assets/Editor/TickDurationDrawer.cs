using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TickDurationAttribute))]
public class TickDurationDrawer : PropertyDrawer
{
    private const int TickRate = 64;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.Integer)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        TickDurationAttribute settings = (TickDurationAttribute)attribute;

        const float infoWidth = 110f;
        const float spacing = 4f;

        Rect valueRect = new Rect(position.x, position.y, position.width - infoWidth - spacing, position.height);
        Rect infoRect = new Rect(valueRect.xMax + spacing, position.y, infoWidth, position.height);

        EditorGUI.BeginChangeCheck();
        int value = EditorGUI.IntField(valueRect, label, property.intValue);

        if (EditorGUI.EndChangeCheck())
            property.intValue = Mathf.Max(settings.MinValue, value);

        float seconds = property.intValue / (float)TickRate;
        EditorGUI.LabelField(infoRect, $"{seconds:0.###} seconds", EditorStyles.miniLabel);
    }
}