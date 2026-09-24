#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class AddConcreteToMapPalette
{
    private const string TexturePath = "Assets/Tiles/Map Layout/4 goc concrete.png";
    private const string TileFolder = "Assets/Tiles/Map Layout/Unique Tiles";
    private const string PalettePath = "Assets/Tiles/Map Layout/Map Layout Palette.prefab";
    private const string CompletionKey = "Meadom.ConcretePalette4Tiles.v4";

    static AddConcreteToMapPalette()
    {
        if (!SessionState.GetBool(CompletionKey, false))
            EditorApplication.delayCall += Run;
    }

    public static void Run()
    {
        SessionState.SetBool(CompletionKey, true);
        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (importer == null || texture == null || texture.width != 48 || texture.height != 48)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100f;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;

        // The file is a 3x3 sheet. Its four 16x16 corner cells are the four
        // concrete corner brushes requested by the user.
        SpriteMetaData[] slices =
        {
            Slice("4 goc concrete_TopLeft",     0, 32),
            Slice("4 goc concrete_TopRight",   32, 32),
            Slice("4 goc concrete_BottomLeft", 0, 0),
            Slice("4 goc concrete_BottomRight",32, 0)
        };
#pragma warning disable CS0618
        importer.spritesheet = slices;
#pragma warning restore CS0618
        importer.SaveAndReimport();

        Sprite[] sprites =
        {
            LoadSprite("4 goc concrete_TopLeft"),
            LoadSprite("4 goc concrete_TopRight"),
            LoadSprite("4 goc concrete_BottomLeft"),
            LoadSprite("4 goc concrete_BottomRight")
        };
        if (sprites.Any(sprite => sprite == null)) return;

        // Remove the previous oversized single tile.
        AssetDatabase.DeleteAsset(TileFolder + "/4 goc concrete.asset");
        Tile[] tiles = new Tile[4];
        for (int i = 0; i < sprites.Length; i++)
        {
            string path = TileFolder + "/" + sprites[i].name + ".asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprites[i];
            tile.color = Color.white;
            tile.transform = Matrix4x4.identity;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            tiles[i] = tile;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PalettePath);
        try
        {
            Tilemap map = root.GetComponentInChildren<Tilemap>(true);
            if (map == null) return;

            // Remove every concrete entry from earlier import attempts, including
            // the old 48x48 tile which visually covered several palette cells.
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
            {
                TileBase existing = map.GetTile(cell);
                if (existing != null && existing.name.StartsWith("4 goc concrete"))
                    map.SetTile(cell, null);
            }

            map.CompressBounds();
            int y = map.cellBounds.yMin - 2;
            int x = map.cellBounds.xMin;
            for (int i = 0; i < tiles.Length; i++)
                map.SetTile(new Vector3Int(x + i, y, 0), tiles[i]);
            map.RefreshAllTiles();
            PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("Added four sliced 16x16 concrete corner tiles to Map Layout Palette.");
    }

    private static Sprite LoadSprite(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(TexturePath)
            .OfType<Sprite>()
            .FirstOrDefault(sprite => sprite.name == name);
    }

    private static SpriteMetaData Slice(string name, int x, int y)
    {
        return new SpriteMetaData
        {
            name = name,
            rect = new Rect(x, y, 16, 16),
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f)
        };
    }
}
#endif
