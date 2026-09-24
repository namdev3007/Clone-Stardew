using System.Collections.Generic;
using UnityEngine;

namespace World.Objects
{
    /// <summary>
    /// How one growth stage (or trellis post) is anchored inside its soil cell.
    /// The parent transform is always the centre of the cell.
    /// </summary>
    public enum CropStageAnchor
    {
        /// <summary>Visible pixels centred in the cell; frames taller than the cell stand on the ground line.</summary>
        Auto = 0,
        /// <summary>Centre of the visible pixels on the cell centre (seeds, sprouts, round heads).</summary>
        CenterInCell = 1,
        /// <summary>Bottom-centre of the visible pixels on the ground line.</summary>
        StandOnCellGround = 2,
        /// <summary>The stage's anchor pixel (stem base, post foot, trunk base) on the ground line.</summary>
        PixelOnCellGround = 3,
        /// <summary>The stage's anchor pixel on the cell centre.</summary>
        PixelOnCellCenter = 4,
        /// <summary>The sprite's own pivot (as set in the Sprite Editor) on the cell centre.</summary>
        PivotOnCellCenter = 5
    }

    /// <summary>
    /// Shared rule for placing a crop-like sprite (growth stage, trellis post)
    /// inside the soil cell whose centre is the parent transform.
    /// </summary>
    public static class CropSpriteAlignment
    {
        /// <summary>Local scale of every crop/post renderer.</summary>
        public const float CropVisualScale = 0.5f;
        /// <summary>Default distance of the ground line above the cell bottom (2 world px on a 16 px tile).</summary>
        public const float DefaultGroundInset = 0.02f;
        public const float DefaultCellHeight = 0.16f;

        private static readonly Dictionary<Sprite, Rect> visibleBoundsCache = new Dictionary<Sprite, Rect>();

        /// <summary>Anchors that read <c>anchorPixel</c>.</summary>
        public static bool UsesAnchorPixel(CropStageAnchor mode)
        {
            return mode == CropStageAnchor.PixelOnCellGround || mode == CropStageAnchor.PixelOnCellCenter;
        }

        /// <summary>
        /// Local position of a child renderer (scaled by <paramref name="scale"/>)
        /// that puts <paramref name="sprite"/> in the cell according to <paramref name="mode"/>.
        /// </summary>
        /// <param name="anchorPixel">Sprite pixel, measured from the bottom-left of the sprite rect (Sprite Editor coordinates).</param>
        /// <param name="groundInset">Height of the ground line above the cell bottom, in world units.</param>
        public static Vector2 GetAnchoredOffset(Sprite sprite, float scale, float cellHeight,
            CropStageAnchor mode, Vector2 anchorPixel, float groundInset = DefaultGroundInset)
        {
            if (sprite == null)
                return Vector2.zero;

            if (cellHeight <= 0f)
                cellHeight = DefaultCellHeight;

            float groundY = -cellHeight * 0.5f + groundInset;
            Rect visible = GetVisibleLocalBounds(sprite);

            switch (mode)
            {
                case CropStageAnchor.CenterInCell:
                    return -visible.center * scale;

                case CropStageAnchor.StandOnCellGround:
                    return new Vector2(-visible.center.x * scale, groundY - visible.yMin * scale);

                case CropStageAnchor.PixelOnCellGround:
                {
                    Vector2 local = PixelToLocal(sprite, anchorPixel);
                    return new Vector2(-local.x * scale, groundY - local.y * scale);
                }

                case CropStageAnchor.PixelOnCellCenter:
                    return -PixelToLocal(sprite, anchorPixel) * scale;

                case CropStageAnchor.PivotOnCellCenter:
                    return Vector2.zero;

                default:
                {
                    float visibleHeight = visible.height * scale;
                    float footY = Mathf.Max(-visibleHeight * 0.5f, groundY);
                    return new Vector2(-visible.center.x * scale, footY - visible.yMin * scale);
                }
            }
        }

        /// <summary>Automatic anchor (see <see cref="CropStageAnchor.Auto"/>).</summary>
        public static Vector2 GetCellCenteredOffset(Sprite sprite, float scale, float cellHeight = DefaultCellHeight)
        {
            return GetAnchoredOffset(sprite, scale, cellHeight, CropStageAnchor.Auto, Vector2.zero);
        }

        /// <summary>
        /// The previous anchor (sprite rect bottom-centre on the cell centre).
        /// Used to move hand-authored colliders by the same amount as the art.
        /// </summary>
        public static Vector2 GetLegacyBottomCenterOffset(Sprite sprite, float scale)
        {
            if (sprite == null)
                return Vector2.zero;

            float x = ((sprite.pivot.x - sprite.rect.width * 0.5f) / sprite.pixelsPerUnit) * scale;
            float y = (sprite.pivot.y / sprite.pixelsPerUnit) * scale;
            return new Vector2(x, y);
        }

        /// <summary>Sprite pixel (from the rect's bottom-left) to sprite-local units (pivot at the origin).</summary>
        public static Vector2 PixelToLocal(Sprite sprite, Vector2 pixel)
        {
            return (pixel - sprite.pivot) / sprite.pixelsPerUnit;
        }

        /// <summary>Sprite-local units (pivot at the origin) to a sprite pixel from the rect's bottom-left.</summary>
        public static Vector2 LocalToPixel(Sprite sprite, Vector2 local)
        {
            return local * sprite.pixelsPerUnit + sprite.pivot;
        }

        /// <summary>
        /// Bounds of the sprite's opaque pixels in sprite-local units (pivot at
        /// the origin). Crop sheets import with a Tight mesh whose vertices hug
        /// the opaque pixels, so no texture read access is needed.
        /// </summary>
        public static Rect GetVisibleLocalBounds(Sprite sprite)
        {
            if (visibleBoundsCache.TryGetValue(sprite, out Rect cached))
                return cached;

            Vector2[] vertices = sprite.vertices;
            Rect bounds;
            if (vertices == null || vertices.Length == 0)
            {
                Bounds full = sprite.bounds;
                bounds = Rect.MinMaxRect(full.min.x, full.min.y, full.max.x, full.max.y);
            }
            else
            {
                Vector2 min = vertices[0];
                Vector2 max = vertices[0];
                for (int i = 1; i < vertices.Length; i++)
                {
                    min = Vector2.Min(min, vertices[i]);
                    max = Vector2.Max(max, vertices[i]);
                }
                bounds = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }

            visibleBoundsCache[sprite] = bounds;
            return bounds;
        }
    }
}
