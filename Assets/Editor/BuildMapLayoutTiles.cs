#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Converts the 16x16 cells in the map-layout reference image into a compact,
/// deduplicated tile library. The old farm scene is deliberately not changed:
/// designers can replace it one region at a time with the generated palette.
/// </summary>
public static class BuildMapLayoutTiles
{
    // Resolve by GUID so the Vietnamese filename remains safe across editor/code-page settings.
    private const string SourceGuid = "bbee6d042639dbd439e0e6914966acf4";
    private const string PondSourceGuid = "099d84ace1b1504499fbd5dd16001d7a";
    private static string SourcePath => AssetDatabase.GUIDToAssetPath(SourceGuid);
    private static string PondSourcePath => AssetDatabase.GUIDToAssetPath(PondSourceGuid);
    private const string OutputRoot = "Assets/Tiles/Map Layout";
    private const string TileFolder = OutputRoot + "/Unique Tiles";
    private const string PondTileFolder = OutputRoot + "/Pond Tiles";
    private const string PalettePath = OutputRoot + "/Map Layout Palette.prefab";
    private const string PreviewPath = OutputRoot + "/Map Layout Preview.prefab";
    private const int CellSize = 16;

    [InitializeOnLoadMethod]
    private static void BuildOnceAfterImport()
    {
        EditorApplication.delayCall += () =>
        {
            bool paletteNeedsUpgrade = !AssetDatabase.LoadAllAssetsAtPath(PalettePath)
                .Any(asset => asset is GridPalette);
            bool pondTilesMissing = AssetDatabase.LoadAssetAtPath<Tile>(PondTileFolder + "/PondTile_076.asset") == null;
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                (AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath) == null || paletteNeedsUpgrade || pondTilesMissing))
            {
                Build(false);
            }
        };
    }

    [MenuItem("Tools/Map/Build Tiles From Map Layout")]
    private static void BuildFromMenu()
    {
        Build(true);
    }

    private static void Build(bool force)
    {
        TextureImporter importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Map layout texture was not found: {SourcePath}");
            return;
        }

        EnsureFolder(OutputRoot);
        EnsureFolder(TileFolder);
        EnsureFolder(PondTileFolder);

        bool restoreReadable = !importer.isReadable;
        try
        {
            if (restoreReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(SourcePath)
                .OfType<Sprite>()
                .Where(sprite => TryGetCellIndex(sprite.name, out _))
                .OrderBy(sprite => GetCellIndex(sprite.name))
                .ToArray();

            int columns = texture.width / CellSize;
            int rows = texture.height / CellSize;
            int expectedCells = columns * rows;
            if (sprites.Length < expectedCells)
                throw new InvalidOperationException($"Expected {expectedCells} sliced sprites, but found {sprites.Length}.");

            Color32[] pixels = texture.GetPixels32();
            Dictionary<ulong, List<int>> candidates = new Dictionary<ulong, List<int>>();
            List<Color32[]> uniquePixels = new List<Color32[]>();
            List<Sprite> representativeSprites = new List<Sprite>();
            int[] uniqueIndexByCell = new int[expectedCells];

            for (int cellIndex = 0; cellIndex < expectedCells; cellIndex++)
            {
                Sprite sprite = sprites[cellIndex];
                Rect rect = sprite.rect;
                Color32[] cellPixels = ReadCell(pixels, texture.width, Mathf.RoundToInt(rect.x), Mathf.RoundToInt(rect.y));
                ulong hash = Hash(cellPixels);

                int uniqueIndex = -1;
                if (candidates.TryGetValue(hash, out List<int> matches))
                {
                    for (int i = 0; i < matches.Count; i++)
                    {
                        int candidate = matches[i];
                        if (SamePixels(cellPixels, uniquePixels[candidate]))
                        {
                            uniqueIndex = candidate;
                            break;
                        }
                    }
                }
                else
                {
                    matches = new List<int>();
                    candidates.Add(hash, matches);
                }

                if (uniqueIndex < 0)
                {
                    uniqueIndex = uniquePixels.Count;
                    uniquePixels.Add(cellPixels);
                    representativeSprites.Add(sprite);
                    matches.Add(uniqueIndex);
                }

                uniqueIndexByCell[cellIndex] = uniqueIndex;
            }

            Tile[] tiles = CreateOrUpdateTiles(representativeSprites, force);
            Sprite[] pondSprites = LoadNumberedSprites(PondSourcePath);
            Tile[] pondTiles = CreateOrUpdateNamedTiles(pondSprites, PondTileFolder, "PondTile", force);
            CreatePalettePrefab(tiles, pondTiles);
            // The preview is the designer-authored map after its first build. Do
            // not overwrite later pond/detail painting when refreshing the tile
            // library or palette.
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath) == null)
                CreatePreviewPrefab(tiles, uniqueIndexByCell, columns, rows);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Map layout tiles ready: {expectedCells} cells, {tiles.Length} unique tiles, " +
                      $"{pondTiles.Length} pond tiles. " +
                      $"Palette: {PalettePath}; Preview: {PreviewPath}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (restoreReadable)
            {
                importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
                if (importer != null)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }
    }

    private static Tile[] CreateOrUpdateTiles(IReadOnlyList<Sprite> sprites, bool force)
    {
        return CreateOrUpdateNamedTiles(sprites, TileFolder, "MapTile", force);
    }

    private static Tile[] CreateOrUpdateNamedTiles(
        IReadOnlyList<Sprite> sprites,
        string folder,
        string prefix,
        bool force)
    {
        Tile[] tiles = new Tile[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
        {
            string path = $"{folder}/{prefix}_{i:000}.asset";
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            else if (!force && tile.sprite == sprites[i])
            {
                tiles[i] = tile;
                continue;
            }

            tile.sprite = sprites[i];
            tile.color = Color.white;
            tile.transform = Matrix4x4.identity;
            tile.flags = TileFlags.LockAll;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
            tiles[i] = tile;
        }

        return tiles;
    }

    private static Sprite[] LoadNumberedSprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .Where(sprite => TryGetCellIndex(sprite.name, out _))
            .OrderBy(sprite => GetCellIndex(sprite.name))
            .ToArray();
    }

    private static void CreatePalettePrefab(TileBase[] tiles, TileBase[] pondTiles)
    {
        // A normal Grid prefab is not enough: Tile Palette only lists prefabs which
        // also contain Unity's GridPalette settings sub-asset.
        bool validPalette = AssetDatabase.LoadAllAssetsAtPath(PalettePath)
            .Any(asset => asset is GridPalette);

        if (!validPalette)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath) != null)
                AssetDatabase.DeleteAsset(PalettePath);

            GridPaletteUtility.CreateNewPalette(
                OutputRoot,
                "Map Layout Palette",
                GridLayout.CellLayout.Rectangle,
                (GridPalette.CellSizing)0,
                new Vector3(0.16f, 0.16f, 0f),
                GridLayout.CellSwizzle.XYZ);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PalettePath);
        Grid grid = root.GetComponent<Grid>();
        grid.cellSize = new Vector3(0.16f, 0.16f, 0f);

        Tilemap tilemap = root.GetComponentInChildren<Tilemap>(true);
        tilemap.ClearAllTiles();
        tilemap.gameObject.name = "Tiles";
        const int paletteColumns = 22;
        for (int i = 0; i < tiles.Length; i++)
            tilemap.SetTile(new Vector3Int(i % paletteColumns, -(i / paletteColumns), 0), tiles[i]);

        // Keep the pond sheet together as its original 11 x 7 block, separated
        // from the map-derived tiles by one empty row.
        const int pondColumns = 11;
        int pondStartY = -Mathf.CeilToInt(tiles.Length / (float)paletteColumns) - 1;
        for (int i = 0; i < pondTiles.Length; i++)
            tilemap.SetTile(new Vector3Int(i % pondColumns, pondStartY - (i / pondColumns), 0), pondTiles[i]);
        tilemap.CompressBounds();
        PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static void CreatePreviewPrefab(TileBase[] tiles, IReadOnlyList<int> tileIndices, int columns, int rows)
    {
        GameObject root = new GameObject("Map Layout Preview", typeof(Grid));
        Grid grid = root.GetComponent<Grid>();
        grid.cellSize = new Vector3(0.16f, 0.16f, 0f);

        GameObject child = new GameObject("Map Layout", typeof(Tilemap), typeof(TilemapRenderer));
        child.transform.SetParent(root.transform, false);
        Tilemap tilemap = child.GetComponent<Tilemap>();
        for (int i = 0; i < tileIndices.Count; i++)
        {
            int x = i % columns;
            int y = rows - 1 - (i / columns);
            tilemap.SetTile(new Vector3Int(x, y, 0), tiles[tileIndices[i]]);
        }
        tilemap.CompressBounds();
        PrefabUtility.SaveAsPrefabAsset(root, PreviewPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static Color32[] ReadCell(Color32[] source, int textureWidth, int startX, int startY)
    {
        Color32[] result = new Color32[CellSize * CellSize];
        for (int y = 0; y < CellSize; y++)
            Array.Copy(source, (startY + y) * textureWidth + startX, result, y * CellSize, CellSize);
        return result;
    }

    private static ulong Hash(IReadOnlyList<Color32> pixels)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        for (int i = 0; i < pixels.Count; i++)
        {
            Color32 pixel = pixels[i];
            hash = (hash ^ pixel.r) * prime;
            hash = (hash ^ pixel.g) * prime;
            hash = (hash ^ pixel.b) * prime;
            hash = (hash ^ pixel.a) * prime;
        }
        return hash;
    }

    private static bool SamePixels(IReadOnlyList<Color32> left, IReadOnlyList<Color32> right)
    {
        if (left.Count != right.Count)
            return false;
        for (int i = 0; i < left.Count; i++)
        {
            if (!left[i].Equals(right[i]))
                return false;
        }
        return true;
    }

    private static int GetCellIndex(string name)
    {
        TryGetCellIndex(name, out int index);
        return index;
    }

    private static bool TryGetCellIndex(string name, out int index)
    {
        index = -1;
        int separator = name.LastIndexOf('_');
        if (separator < 0)
            return false;

        return int.TryParse(name.Substring(separator + 1), out index);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
