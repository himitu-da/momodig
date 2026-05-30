using UnityEditor;

[CustomEditor(typeof(BiomeData))]
public class BiomeDataCustomEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("biomeName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("biomeType"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minHeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("generationRules"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("backgroundTexture"));

        serializedObject.ApplyModifiedProperties();
    }
}
