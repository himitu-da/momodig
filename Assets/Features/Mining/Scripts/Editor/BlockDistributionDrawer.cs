using UnityEngine;
using UnityEditor;

[CustomPropertyDrawer(typeof(BlockDistribution))]
public class BlockDistributionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // BiomeDataオブジェクトを取得して、min/maxHeightの値を使用
        BiomeData biomeData = property.serializedObject.targetObject as BiomeData;
        if (biomeData == null)
        {
            EditorGUI.LabelField(position, "Error: Could not find BiomeData.");
            EditorGUI.EndProperty();
            return;
        }

        // プロパティの描画位置を調整
        Rect contentPosition = EditorGUI.PrefixLabel(position, label);
        
        // blockDataフィールドを描画
        SerializedProperty blockDataProp = property.FindPropertyRelative("blockData");
        Rect blockDataRect = new Rect(contentPosition.x, contentPosition.y, contentPosition.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(blockDataRect, blockDataProp, GUIContent.none);

        // distributionCurveフィールドを描画
        SerializedProperty curveProp = property.FindPropertyRelative("distributionCurve");
        Rect curveRect = new Rect(contentPosition.x, contentPosition.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, contentPosition.width, EditorGUIUtility.singleLineHeight * 2);
        
        // 描画範囲を定義
        float range = Mathf.Abs(biomeData.maxHeight - biomeData.minHeight);
        Rect viewRect = new Rect(biomeData.minHeight, 0, range, 1); // 縦軸を0-1に設定

        curveProp.animationCurveValue = EditorGUI.CurveField(
            curveRect,
            "Distribution",
            curveProp.animationCurveValue,
            Color.green,
            viewRect
        );

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // 2つのフィールドとスペース分の高さを返す
        return EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 2;
    }
}
