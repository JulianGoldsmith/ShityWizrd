using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AnimParameterAttribute))]
public class AnimParameterDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        AnimParameterAttribute paramAttribute = attribute as AnimParameterAttribute;

        // 1. Get the Profile we are currently editing
        AnimMasterProfileSO profile = property.serializedObject.targetObject as AnimMasterProfileSO;

        if (profile == null)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // 2. Fetch the correct list of names based on the attribute
        List<string> options = new List<string>();

        if (paramAttribute.ParameterType == AnimParamType.Float)
        {
            options.AddRange(profile.FloatParameters.Select(p => p.Name));
        }
        else if (paramAttribute.ParameterType == AnimParamType.Bool)
        {
            options.AddRange(profile.BoolParameters.Select(p => p.Name));
        }
        else if (paramAttribute.ParameterType == AnimParamType.Trigger) 
        {
            options.AddRange(profile.TriggerParameters.Select(p => p.Name));
        }

        // 3. Handle empty lists gracefully
        if (options.Count == 0)
        {
            EditorGUI.LabelField(position, label.text, "No Parameters Found");
            return;
        }

        // 4. Find the current index so the dropdown shows the correct selected item
        int currentIndex = Mathf.Max(0, options.IndexOf(property.stringValue));

        // 5. Draw the Dropdown Menu!
        currentIndex = EditorGUI.Popup(position, label.text, currentIndex, options.ToArray());

        // 6. Save the selected string back to the hidden private variable
        property.stringValue = options[currentIndex];
    }
}