using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
public class SubclassSelectorDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, 15, EditorGUIUtility.singleLineHeight);
        Rect popupRect = new Rect(position.x + 15, position.y, position.width - 15, EditorGUIUtility.singleLineHeight);

        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none);

        Type baseType = GetBaseType(property);
        string currentTypeName = property.managedReferenceFullTypename.Split(' ').LastOrDefault();
        if (string.IsNullOrEmpty(currentTypeName)) currentTypeName = "null";

        if (EditorGUI.DropdownButton(popupRect, new GUIContent($"{label.text} ({currentTypeName})"), FocusType.Keyboard))
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(new GUIContent("Null"), string.IsNullOrEmpty(currentTypeName), () =>
            {
                property.managedReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
            });

            if (baseType != null)
            {
                var derivedTypes = TypeCache.GetTypesDerivedFrom(baseType).Where(t => !t.IsAbstract && !t.IsGenericType);
                foreach (Type type in derivedTypes)
                {
                    menu.AddItem(new GUIContent(type.Name), currentTypeName == type.Name, () =>
                    {
                        property.managedReferenceValue = Activator.CreateInstance(type);
                        property.isExpanded = true;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                }
            }
            menu.ShowAsContext();
        }

        if (property.isExpanded && property.managedReferenceValue != null)
        {
            EditorGUI.indentLevel++;
            SerializedProperty iterator = property.Copy();

            // THE FIX: Record the exact depth of the parent object
            int startingDepth = iterator.depth;
            bool enterChildren = true;

            float yOffset = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;

                // THE FIX: If the depth drops back to or below the parent, we have left the object
                if (iterator.depth <= startingDepth) break;

                float height = EditorGUI.GetPropertyHeight(iterator, true);
                Rect childRect = new Rect(position.x, position.y + yOffset, position.width, height);
                EditorGUI.PropertyField(childRect, iterator, true);
                yOffset += height + EditorGUIUtility.standardVerticalSpacing;
            }
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (!property.isExpanded || property.managedReferenceValue == null)
            return EditorGUIUtility.singleLineHeight;

        float totalHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        SerializedProperty iterator = property.Copy();

        // THE FIX: Apply the depth check to the height calculation as well
        int startingDepth = iterator.depth;
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.depth <= startingDepth) break;

            totalHeight += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
        }

        return totalHeight;
    }

    private Type GetBaseType(SerializedProperty property)
    {
        string[] typeInfo = property.managedReferenceFieldTypename.Split(' ');
        if (typeInfo.Length == 2)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == typeInfo[0]);
            if (assembly != null) return assembly.GetType(typeInfo[1]);
        }
        return null;
    }
}