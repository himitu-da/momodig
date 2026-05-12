using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TerrainGenerationEntry))]
public class TerrainGenerationEntryDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(line, property.FindPropertyRelative("resultType"));

        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(line, property.FindPropertyRelative("blockData"));

        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(line, property.FindPropertyRelative("baseWeight"));

        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(line, property.FindPropertyRelative("depthCurve"));

        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(line, property.FindPropertyRelative("horizontalCurve"));

        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(line, property.FindPropertyRelative("specialWeightFunction"));

        line.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(line, property.FindPropertyRelative("specialFunctionK"));

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 7 + EditorGUIUtility.standardVerticalSpacing * 6;
    }
}
