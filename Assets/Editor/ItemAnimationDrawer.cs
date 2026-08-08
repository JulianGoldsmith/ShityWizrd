using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ItemAnimation))]
public class ItemAnimationDrawer : PropertyDrawer
{
    private const int TickRate = 64;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        return line * 3f + spacing * 2f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty clipProperty = property.FindPropertyRelative("clip");
        SerializedProperty speedProperty = property.FindPropertyRelative("speedMultiplier");

        float line = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        Rect clipRect = new Rect(position.x, position.y, position.width, line);
        Rect speedRect = new Rect(position.x, clipRect.yMax + spacing, position.width, line);
        Rect infoRect = new Rect(position.x, speedRect.yMax + spacing, position.width, line);

        EditorGUI.PropertyField(clipRect, clipProperty, label);
        EditorGUI.PropertyField(speedRect, speedProperty);

        AnimationClip clip = clipProperty.objectReferenceValue as AnimationClip;

        if (clip == null)
        {
            EditorGUI.LabelField(infoRect, "No animation clip assigned", EditorStyles.miniLabel);
            EditorGUI.EndProperty();
            return;
        }

        float speed = Mathf.Max(0.01f, speedProperty.floatValue);
        float authoredSeconds = clip.length;
        float playbackSeconds = authoredSeconds / speed;

        int authoredTicks = Mathf.CeilToInt(authoredSeconds * TickRate);
        int playbackTicks = Mathf.CeilToInt(playbackSeconds * TickRate);

        string information;

        if (Mathf.Approximately(speed, 1f))
            information = $"{authoredSeconds:0.###} seconds  •  {authoredTicks} ticks at {TickRate} Hz";
        else
            information = $"Authored: {authoredSeconds:0.###}s / {authoredTicks} ticks  •  Playback: {playbackSeconds:0.###}s / {playbackTicks} ticks";

        EditorGUI.LabelField(infoRect, information, EditorStyles.miniLabel);
        EditorGUI.EndProperty();
    }
}