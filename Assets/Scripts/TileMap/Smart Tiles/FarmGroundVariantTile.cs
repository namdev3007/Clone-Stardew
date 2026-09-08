using Referencing.Scriptable_Assets;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TileMap.Smart_Tiles
{
    /// <summary>
    /// A save-compatible farming tile which picks one visual variant per cell.
    /// Empty variants make it suitable for invisible logic masks as well.
    /// </summary>
    [CreateAssetMenu(fileName = "Farm Ground Variant Tile", menuName = "Tiles/Farm Ground Variant Tile")]
    public sealed class FarmGroundVariantTile : ScriptableTileBase
    {
        [SerializeField] private Sprite[] variants = new Sprite[0];
        [SerializeField] private bool useGridCollider;

        public void Configure(Sprite[] sprites, bool gridCollider = false)
        {
            variants = sprites ?? new Sprite[0];
            useGridCollider = gridCollider;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            tileData.color = Color.white;
            tileData.transform = Matrix4x4.identity;
            tileData.flags = TileFlags.LockAll;
            tileData.colliderType = useGridCollider
                ? Tile.ColliderType.Grid
                : Tile.ColliderType.None;

            if (variants == null || variants.Length == 0)
            {
                tileData.sprite = null;
                return;
            }

            // Stable hash: a cell keeps the same variation after saving/loading.
            unchecked
            {
                int hash = position.x * 73856093 ^ position.y * 19349663 ^ position.z * 83492791;
                int index = (hash & int.MaxValue) % variants.Length;
                tileData.sprite = variants[index];
            }
        }
    }
}
