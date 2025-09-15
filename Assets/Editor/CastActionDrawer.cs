/*using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(CastAction), true)]
public class CastActionDrawer : PropertyDrawer
{
    private readonly Dictionary<string, Type> _actionTypes = new Dictionary<string, Type>();
    private bool _initialized = false;

    private void Initialize()
    {
        if (_initialized) return;

        var derivedTypes = TypeCache.GetTypesDerivedFrom<CastAction>();
        _actionTypes.Clear();
        foreach (var type in derivedTypes)
        {
            if (!type.IsAbstract)
            {
                string friendlyName = ObjectNames.NicifyVariableName(type.Name.Replace("CastAction", ""));
                _actionTypes.Add(friendlyName, type);
            }
        }
        _initialized = true;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        Initialize();
        EditorGUI.BeginProperty(position, label, property);

        if (property.managedReferenceValue == null)
        {
            DrawTypeSelection(position, property);
        }
        else
        {
            DrawCustomInspector(position, property);
        }

        EditorGUI.EndProperty();
    }

    private void DrawTypeSelection(Rect position, SerializedProperty property)
    {
        Rect dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        if (EditorGUI.DropdownButton(dropdownRect, new GUIContent("Select Action Type"), FocusType.Keyboard))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("None"), false, () => SetProperty(property, null));

            foreach (var (name, type) in _actionTypes)
            {
                menu.AddItem(new GUIContent(name), false, () => SetProperty(property, Activator.CreateInstance(type)));
            }
            menu.ShowAsContext();
        }
    }

    private void SetProperty(SerializedProperty property, object value)
    {
        property.managedReferenceValue = value;
        property.serializedObject.ApplyModifiedProperties();
    }

    private void DrawCustomInspector(Rect position, SerializedProperty property)
    {
        string currentTypeName = ObjectNames.NicifyVariableName(property.managedReferenceValue.GetType().Name);

        // Draw the main foldout for the entire action
        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, currentTypeName, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            // Start the drawing rect *after* the main foldout
            Rect currentRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, position.width, 0);

            SerializedProperty activatesHitboxProp = property.FindPropertyRelative("activatesHitBox");
            SerializedProperty endProperty = property.GetEndProperty();
            SerializedProperty iterator = property.Copy();

            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;

                    if ((iterator.name == "hitboxActivationDelay" || iterator.name == "hitboxActiveDuration") && (activatesHitboxProp != null && !activatesHitboxProp.boolValue))
                    {
                        continue;
                    }

                    // *** THE FIX IS HERE ***
                    // 1. Get the actual height of the current property.
                    float propHeight = EditorGUI.GetPropertyHeight(iterator, true);
                    // 2. Set the drawing rect's height for this specific property.
                    currentRect.height = propHeight;
                    // 3. Draw the property.
                    EditorGUI.PropertyField(currentRect, iterator, true);
                    // 4. Advance the Y position by the height of the property we just drew.
                    currentRect.y += propHeight + EditorGUIUtility.standardVerticalSpacing;

                } while (iterator.NextVisible(false));
            }

            EditorGUI.indentLevel--;
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // This part was already correct, it calculates the total needed height.
        // The problem was in OnGUI not using that height correctly for each element.
        if (property.managedReferenceValue == null)
        {
            return EditorGUIUtility.singleLineHeight;
        }

        float totalHeight = EditorGUIUtility.singleLineHeight;

        if (property.isExpanded)
        {
            SerializedProperty activatesHitboxProp = property.FindPropertyRelative("activatesHitBox");
            SerializedProperty endProperty = property.GetEndProperty();
            SerializedProperty iterator = property.Copy();

            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;

                    if ((iterator.name == "hitboxActivationDelay" || iterator.name == "hitboxActiveDuration"))
                    {
                        if (activatesHitboxProp != null && !activatesHitboxProp.boolValue)
                        {
                            continue;
                        }
                    }

                    totalHeight += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;

                } while (iterator.NextVisible(false));
            }
        }

        return totalHeight;
    }
}*/