using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BiomeData))]
public class BiomeDataCustomEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // ターゲットオブジェクトを取得
        BiomeData biomeData = (BiomeData)target;

        // 変更を監視開始
        serializedObject.Update();

        // デフォルトのインスペクター表示を描画（一部のプロパティ）
        EditorGUILayout.PropertyField(serializedObject.FindProperty("biomeName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("biomeType"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minHeight"));

        // availableBlocksリストを標準的な方法で描画
        // Unityが自動的にBlockDistributionDrawerを使用して各要素を描画する
        EditorGUILayout.PropertyField(serializedObject.FindProperty("availableBlocks"), true);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("backgroundTexture"));

        // 変更を適用
        serializedObject.ApplyModifiedProperties();
    }
}
