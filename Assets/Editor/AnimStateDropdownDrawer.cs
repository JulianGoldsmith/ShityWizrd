using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AnimStateDropdownAttribute))]
public class AnimStateDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // 1. Get the Profile we are editing
        var profile = property.serializedObject.targetObject as AnimMasterProfileSO;

        if (profile == null || profile.AllStates == null || profile.AllStates.Count == 0)
        {
            // Fallback if the list is empty or something goes wrong
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        // 2. Build the list of names based on their position in the AllStates list
        string[] options = new string[profile.AllStates.Count];
        for (int i = 0; i < profile.AllStates.Count; i++)
        {
            var state = profile.AllStates[i];

            // Handle empty names gracefully
            string stateName = string.IsNullOrEmpty(state?.StateName) ? "Unnamed State" : state.StateName;

            // Format it nicely (e.g., "0: Locomotion", "1: Jumping")
            options[i] = $"{i}: {stateName}";
        }

        // 3. Get the current index (property.intValue works for bytes too)
        int currentIndex = property.intValue;

        // Clamp it just in case a state was deleted from the list
        if (currentIndex < 0 || currentIndex >= options.Length) currentIndex = 0;

        // 4. Draw the dropdown
        currentIndex = EditorGUI.Popup(position, label.text, currentIndex, options);

        // 5. Save the selected index back to the byte
        property.intValue = currentIndex;
    }
}