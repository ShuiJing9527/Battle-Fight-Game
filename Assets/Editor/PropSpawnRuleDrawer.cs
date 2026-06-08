using UnderTheStars.GenerationMap;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PropSpawnRule))]
public class PropSpawnRuleDrawer : PropertyDrawer
{
    private const float VerticalSpacing = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float totalHeight = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return totalHeight;
        }

        SerializedProperty iterator = property.Copy();
        SerializedProperty endProperty = iterator.GetEndProperty();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
        {
            totalHeight += EditorGUI.GetPropertyHeight(iterator, true) + VerticalSpacing;
            enterChildren = false;
        }

        return totalHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty ruleNameProperty = property.FindPropertyRelative("ruleName");
        string fallbackTitle = label != null ? label.text : property.displayName;
        string customTitle = ruleNameProperty != null ? ruleNameProperty.stringValue : string.Empty;
        string displayTitle = string.IsNullOrWhiteSpace(customTitle) ? fallbackTitle : customTitle;

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, displayTitle, true);

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            float y = foldoutRect.yMax + VerticalSpacing;

            SerializedProperty iterator = property.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();
            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                float fieldHeight = EditorGUI.GetPropertyHeight(iterator, true);
                Rect fieldRect = new Rect(position.x, y, position.width, fieldHeight);
                EditorGUI.PropertyField(fieldRect, iterator, true);
                y += fieldHeight + VerticalSpacing;
                enterChildren = false;
            }

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }
}
