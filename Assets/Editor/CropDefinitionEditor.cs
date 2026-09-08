#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using World.Objects;

[CustomEditor(typeof(CropDefinition))]
public class CropDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("firstGrowthSeconds"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("regrowthSeconds"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maximumHarvests"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("regrowthStageStart"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("perennialTree"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("harvestedItem"));

        SerializedProperty sprites = serializedObject.FindProperty("growthSprites");
        SerializedProperty offsets = serializedObject.FindProperty("stagePositionOffsets");
        if (offsets.arraySize != sprites.arraySize)
            offsets.arraySize = sprites.arraySize;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Growth Stages", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Offset (0, 0) keeps the common position. Adjust X/Y only for a stage that looks shifted.", MessageType.Info);

        for (int i = 0; i < sprites.arraySize; i++)
        {
            SerializedProperty sprite = sprites.GetArrayElementAtIndex(i);
            SerializedProperty offset = offsets.GetArrayElementAtIndex(i);
            string spriteName = sprite.objectReferenceValue != null ? sprite.objectReferenceValue.name : "Missing Sprite";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Stage {i + 1}: {spriteName}", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(sprite, new GUIContent("Sprite"));
            EditorGUILayout.PropertyField(offset, new GUIContent("Position Offset"));
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
