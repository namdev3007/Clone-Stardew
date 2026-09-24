#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using World.Objects;

/// <summary>
/// Draws one crop growth stage inside a patch of soil tiles exactly as Crop
/// places it at runtime. Shared by the CropDefinition inspector and the
/// Crop Growth Preview window.
/// </summary>
public static class CropStagePreviewDrawer
{
    public const float CellSize = CropSpriteAlignment.DefaultCellHeight;

    private static readonly Color SoilColor = new Color(0.47f, 0.34f, 0.2f);
    private static readonly Color GridColor = new Color(0.35f, 0.24f, 0.14f);
    private static readonly Color CellColor = new Color(1f, 0.9f, 0.3f);
    private static readonly Color OutOfCellColor = new Color(1f, 0.25f, 0.25f);
    private static readonly Color GroundColor = new Color(0.3f, 0.8f, 1f, 0.8f);
    private static readonly Color AnchorColor = Color.red;

    /// <summary>
    /// Draws the soil patch, the planted cell and the sprite.
    /// </summary>
    /// <param name="cellRow">Row of the planted cell counted from the top of <paramref name="area"/>.</param>
    /// <param name="rendererPosition">Local position of the crop renderer (anchor + offset), world units.</param>
    /// <param name="anchorPixel">Anchor pixel to mark with a red cross, or null.</param>
    /// <returns>Centre of the planted cell in GUI coordinates.</returns>
    public static Vector2 Draw(Rect area, float tilePixels, int cellRow, Sprite sprite, Vector2 rendererPosition,
        float groundInset, Vector2? anchorPixel, bool showGuides = true)
    {
        int columns = Mathf.Max(1, Mathf.RoundToInt(area.width / tilePixels));
        int rows = Mathf.Max(1, Mathf.RoundToInt(area.height / tilePixels));
        float guiPerUnit = tilePixels / CellSize;
        float scale = CropSpriteAlignment.CropVisualScale;

        GUI.BeginGroup(area);
        Rect local = new Rect(0f, 0f, area.width, area.height);
        EditorGUI.DrawRect(local, SoilColor);
        for (int c = 0; c <= columns; c++)
            EditorGUI.DrawRect(new Rect(c * tilePixels, 0f, 1f, local.height), GridColor);
        for (int r = 0; r <= rows; r++)
            EditorGUI.DrawRect(new Rect(0f, r * tilePixels, local.width, 1f), GridColor);

        Rect cellRect = new Rect(Mathf.Floor(columns * 0.5f) * tilePixels, cellRow * tilePixels, tilePixels, tilePixels);
        if (columns % 2 == 0)
            cellRect.x -= tilePixels * 0.5f;
        Vector2 cellCenter = cellRect.center;

        bool outOfCell = false;
        if (sprite != null)
        {
            // Sprite rect in world units relative to the cell centre, then to GUI space (Y down).
            Vector2 spriteSize = sprite.rect.size / sprite.pixelsPerUnit * scale;
            Vector2 spriteBottomLeft = rendererPosition - sprite.pivot / sprite.pixelsPerUnit * scale;
            Rect spriteRect = new Rect(
                cellCenter.x + spriteBottomLeft.x * guiPerUnit,
                cellCenter.y - (spriteBottomLeft.y + spriteSize.y) * guiPerUnit,
                spriteSize.x * guiPerUnit,
                spriteSize.y * guiPerUnit);
            Texture2D texture = sprite.texture;
            Rect uv = new Rect(
                sprite.textureRect.x / texture.width, sprite.textureRect.y / texture.height,
                sprite.textureRect.width / texture.width, sprite.textureRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(spriteRect, texture, uv, true);

            // A crop must never reach below its own soil cell.
            float visibleBottom = rendererPosition.y + CropSpriteAlignment.GetVisibleLocalBounds(sprite).yMin * scale;
            outOfCell = visibleBottom < -CellSize * 0.5f - 0.0001f;
        }

        if (showGuides)
        {
            DrawOutline(cellRect, outOfCell ? OutOfCellColor : CellColor);
            float groundGuiY = cellCenter.y - (-CellSize * 0.5f + groundInset) * guiPerUnit;
            EditorGUI.DrawRect(new Rect(cellRect.x, groundGuiY, cellRect.width, 1f), GroundColor);

            if (sprite != null && anchorPixel.HasValue)
            {
                Vector2 anchorWorld = rendererPosition + CropSpriteAlignment.PixelToLocal(sprite, anchorPixel.Value) * scale;
                Vector2 anchorGui = new Vector2(cellCenter.x + anchorWorld.x * guiPerUnit, cellCenter.y - anchorWorld.y * guiPerUnit);
                EditorGUI.DrawRect(new Rect(anchorGui.x - 4f, anchorGui.y, 9f, 1f), AnchorColor);
                EditorGUI.DrawRect(new Rect(anchorGui.x, anchorGui.y - 4f, 1f, 9f), AnchorColor);
            }
        }

        GUI.EndGroup();
        return cellCenter + area.position;
    }

    /// <summary>GUI point to a sprite pixel of a sprite drawn by <see cref="Draw"/>.</summary>
    public static Vector2 GuiToSpritePixel(Sprite sprite, Vector2 guiPoint, Vector2 cellCenterGui,
        Vector2 rendererPosition, float tilePixels)
    {
        float guiPerUnit = tilePixels / CellSize;
        Vector2 world = new Vector2(
            (guiPoint.x - cellCenterGui.x) / guiPerUnit,
            (cellCenterGui.y - guiPoint.y) / guiPerUnit);
        return CropSpriteAlignment.LocalToPixel(sprite, (world - rendererPosition) / CropSpriteAlignment.CropVisualScale);
    }

    private static void DrawOutline(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }
}
#endif
