#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomPropertyDrawer(typeof(MaterialIDAttribute))]
public class MaterialIDDrawer : PropertyDrawer
{
    private string[] _options;
    private MasterMaterialDictionary _dictionary;

    private void LoadDictionary()
    {
        // Only load once to save performance, unless the dictionary was deleted
        if (_dictionary != null && _options != null) return;

        string[] guids = AssetDatabase.FindAssets("t:MasterMaterialDictionary");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _dictionary = AssetDatabase.LoadAssetAtPath<MasterMaterialDictionary>(path);
        }

        if (_dictionary == null)
        {
            _options = new string[] { "0: NULL (Dictionary Missing)" };
            return;
        }

        List<string> optionsList = new List<string>();
        for (int i = 0; i < _dictionary.Materials.Count; i++)
        {
            if (i == 0 || _dictionary.Materials[i] == null)
            {
                optionsList.Add($"{i}: NULL");
            }
            else
            {
                // Grabs the custom string name you assigned in the POM asset
                optionsList.Add($"{i}: {_dictionary.Materials[i].material_name}");
            }
        }
        _options = optionsList.ToArray();
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Ensure this is only used on integers (ushort saves as integer in SerializedProperties)
        if (property.propertyType != SerializedPropertyType.Integer)
        {
            EditorGUI.LabelField(position, label.text, "Use [MaterialID] on ushort or int variables.");
            return;
        }

        LoadDictionary();

        EditorGUI.BeginProperty(position, label, property);

        int currentIndex = property.intValue;

        // Safety clamp in case the dictionary shrank
        if (currentIndex < 0 || currentIndex >= _options.Length) currentIndex = 0;

        // Draw the visual dropdown
        int newIndex = EditorGUI.Popup(position, label.text, currentIndex, _options);

        // Save the raw integer ID to the variable if it changed
        if (newIndex != currentIndex)
        {
            property.intValue = newIndex;
        }

        EditorGUI.EndProperty();
    }
}
#endif