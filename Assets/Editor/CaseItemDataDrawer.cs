#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(CaseItemData))]
public class CaseItemDataDrawer : PropertyDrawer
{
    private const int BaseFieldCount = 7;
    private const int AdvancedFieldCount = 3;
    private static readonly string AdvancedKey = "CaseItemDataDrawer_ShowAdvanced";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        Rect lineRect = new Rect(position.x, position.y, position.width, lineHeight);

        property.isExpanded = EditorGUI.Foldout(lineRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;

            bool showAdvanced = EditorPrefs.GetBool(AdvancedKey, false);
            lineRect.y += lineHeight + spacing;
            showAdvanced = EditorGUI.ToggleLeft(lineRect, "Show Weapon/Skin Fields", showAdvanced);
            EditorPrefs.SetBool(AdvancedKey, showAdvanced);

            if (showAdvanced)
            {
                lineRect.y += lineHeight + spacing;
                EditorGUI.PropertyField(lineRect, property.FindPropertyRelative("weaponName"));

                lineRect.y += lineHeight + spacing;
                EditorGUI.PropertyField(lineRect, property.FindPropertyRelative("skinName"));

                lineRect.y += lineHeight + spacing;
                EditorGUI.PropertyField(lineRect, property.FindPropertyRelative("dropChance"));
            }

            var itemNameProperty = property.FindPropertyRelative("itemName");
            lineRect.y += lineHeight + spacing;
            EditorGUI.PropertyField(lineRect, itemNameProperty);

            var itemIdProperty = property.FindPropertyRelative("itemId");
            EnsureItemId(itemIdProperty, itemNameProperty, property.propertyPath);

            lineRect.y += lineHeight + spacing;
            EditorGUI.PropertyField(lineRect, property.FindPropertyRelative("itemIcon"));

            lineRect.y += lineHeight + spacing;
            EditorGUI.PropertyField(lineRect, property.FindPropertyRelative("minValue"));

            lineRect.y += lineHeight + spacing;
            EditorGUI.PropertyField(lineRect, property.FindPropertyRelative("maxValue"));

            lineRect.y += lineHeight + spacing;
            EditorGUI.PropertyField(lineRect, property.FindPropertyRelative("floatMin"));

            lineRect.y += lineHeight + spacing;
            EditorGUI.PropertyField(lineRect, property.FindPropertyRelative("floatMax"));

            lineRect.y += lineHeight + spacing;
            EditorGUI.PropertyField(lineRect, property.FindPropertyRelative("rarity"));

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        if (!property.isExpanded)
            return lineHeight;

        int extraFields = EditorPrefs.GetBool(AdvancedKey, false) ? AdvancedFieldCount : 0;
        int totalFields = 2 + BaseFieldCount + extraFields;
        int totalSpaces = totalFields - 1;
        return (lineHeight * totalFields) + (spacing * totalSpaces);
    }

    private static void EnsureItemId(SerializedProperty itemIdProperty, SerializedProperty itemNameProperty, string propertyPath)
    {
        if (itemIdProperty == null || itemNameProperty == null)
            return;

        if (!string.IsNullOrEmpty(itemIdProperty.stringValue))
            return;

        int index = GetArrayIndex(propertyPath);
        if (index >= 0)
        {
            itemIdProperty.stringValue = (index + 1).ToString();
            return;
        }

        if (!string.IsNullOrEmpty(itemNameProperty.stringValue))
            itemIdProperty.stringValue = itemNameProperty.stringValue.Trim();
    }

    private static int GetArrayIndex(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return -1;

        int start = propertyPath.LastIndexOf("data[", StringComparison.Ordinal);
        if (start < 0)
            return -1;

        start += 5;
        int end = propertyPath.IndexOf(']', start);
        if (end < 0)
            return -1;

        string indexText = propertyPath.Substring(start, end - start);
        return int.TryParse(indexText, out int index) ? index : -1;
    }
}
#endif
