#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using World.Objects;

[CustomEditor(typeof(CropDefinition))]
public class CropDefinitionEditor : Editor
{
    private const float PreviewTilePixels = 28f;
    private const int PreviewColumns = 4;
    private const int PreviewRows = 5;
    // The planted cell sits in this row (from the top), leaving room above for tall stages.
    private const int PreviewCellRow = 3;

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
        SerializedProperty modes = serializedObject.FindProperty("stageAnchorModes");
        SerializedProperty pixels = serializedObject.FindProperty("stageAnchorPixels");
        SerializedProperty offsets = serializedObject.FindProperty("stagePositionOffsets");
        SerializedProperty groundInset = serializedObject.FindProperty("groundInset");
        if (modes.arraySize != sprites.arraySize)
            modes.arraySize = sprites.arraySize;
        if (pixels.arraySize != sprites.arraySize)
            pixels.arraySize = sprites.arraySize;
        if (offsets.arraySize != sprites.arraySize)
            offsets.arraySize = sprites.arraySize;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Growth Stages", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(groundInset, new GUIContent("Ground Line Inset"));
        EditorGUILayout.HelpBox(
            "Each stage has its own anchor in the soil cell (yellow box).\n" +
            "• Center In Cell: seeds, sprouts and round heads.\n" +
            "• Pixel On Cell Ground: stem base / post foot / trunk base (red cross) on the ground line (blue).\n" +
            "Click the preview to pick the anchor pixel. Offset is an extra nudge after the anchor.",
            MessageType.Info);

        for (int i = 0; i < sprites.arraySize; i++)
        {
            SerializedProperty sprite = sprites.GetArrayElementAtIndex(i);
            SerializedProperty mode = modes.GetArrayElementAtIndex(i);
            SerializedProperty pixel = pixels.GetArrayElementAtIndex(i);
            SerializedProperty offset = offsets.GetArrayElementAtIndex(i);
            Sprite stageSprite = sprite.objectReferenceValue as Sprite;
            string spriteName = stageSprite != null ? stageSprite.name : "Missing Sprite";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Stage {i + 1}: {spriteName}", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.BeginVertical();
            EditorGUILayout.PropertyField(sprite, new GUIContent("Sprite"));
            EditorGUILayout.PropertyField(mode, new GUIContent("Anchor"));
            CropStageAnchor anchorMode = (CropStageAnchor)mode.enumValueIndex;
            using (new EditorGUI.DisabledScope(!CropSpriteAlignment.UsesAnchorPixel(anchorMode)))
                EditorGUILayout.PropertyField(pixel, new GUIContent("Anchor Pixel"));
            EditorGUILayout.PropertyField(offset, new GUIContent("Position Offset"));
            EditorGUILayout.EndVertical();

            if (stageSprite != null)
                DrawStagePreview(stageSprite, anchorMode, pixel, offset.vector2Value, groundInset.floatValue);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Draws the stage exactly as Crop places it in a soil cell and lets a
    /// click on the preview set the anchor pixel for the Pixel modes.
    /// </summary>
    private static void DrawStagePreview(Sprite sprite, CropStageAnchor mode, SerializedProperty pixel,
        Vector2 offset, float groundInset)
    {
        float width = PreviewColumns * PreviewTilePixels;
        float height = PreviewRows * PreviewTilePixels;
        Rect area = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
        bool usesPixel = CropSpriteAlignment.UsesAnchorPixel(mode);

        Vector2 rendererPosition = CropSpriteAlignment.GetAnchoredOffset(
            sprite, CropSpriteAlignment.CropVisualScale, CropStagePreviewDrawer.CellSize, mode,
            pixel.vector2Value, groundInset) + offset;
        Vector2 cellCenter = CropStagePreviewDrawer.Draw(area, PreviewTilePixels, PreviewCellRow, sprite,
            rendererPosition, groundInset, usesPixel ? pixel.vector2Value : (Vector2?)null);

        UnityEngine.Event current = UnityEngine.Event.current;
        if (usesPixel && current.type == EventType.MouseDown && current.button == 0 && area.Contains(current.mousePosition))
        {
            Vector2 picked = CropStagePreviewDrawer.GuiToSpritePixel(
                sprite, current.mousePosition, cellCenter, rendererPosition, PreviewTilePixels);
            picked.x = Mathf.Clamp(Mathf.Round(picked.x * 2f) * 0.5f, 0f, sprite.rect.width);
            picked.y = Mathf.Clamp(Mathf.Round(picked.y * 2f) * 0.5f, 0f, sprite.rect.height);
            pixel.vector2Value = picked;
            current.Use();
        }
    }
}
#endif
