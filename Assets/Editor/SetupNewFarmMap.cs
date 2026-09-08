#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TileMap.Smart_Tiles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using World;

/// <summary>
/// Installs the authored Map Layout Preview as Level_Farm's visible map while
/// retaining GridManager's farming overlays and save-compatible tile actions.
/// </summary>
public static class SetupNewFarmMap
{
    private const string LevelPath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string PreviewPath = "Assets/Tiles/Map Layout/Map Layout Preview.prefab";
    private const string OutputFolder = "Assets/Tiles/Farming/New Map";
    private const string DryTilePath = OutputFolder + "/Dirt Hoed Variant.asset";
    private const string WetTilePath = OutputFolder + "/Dirt Watered Variant.asset";
    private const string LogicTilePath = OutputFolder + "/Farmable Logic Tile.asset";
    private const string WaterLogicTilePath = OutputFolder + "/Water Logic Tile.asset";
    private const string DrySpriteGuid = "5b8c2a60f988e3147997330788e4c4ac";
    private const string WetSpriteGuid = "2105b1b2aa988db4ab9ea2383a5109dc";
    private const string MarkerName = "Map Layout Base";
    private const int CurrentMapVersion = 3;

    [InitializeOnLoadMethod]
    private static void InstallOnceAfterCompile()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            if (EditorPrefs.GetInt(GetVersionKey(), 0) < CurrentMapVersion)
                Install(false);
        };
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode &&
            EditorPrefs.GetInt(GetVersionKey(), 0) < CurrentMapVersion)
        {
            EditorApplication.delayCall += () => Install(false);
        }
    }

    [MenuItem("Tools/Map/Install New Map Into Level Farm")]
    private static void InstallFromMenu()
    {
        Install(true);
    }

    private static string GetVersionKey()
    {
        return "Meadom.NewFarmMap." + Application.dataPath.GetHashCode();
    }

    private static void Install(bool force)
    {
        GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPath);
        if (preview == null)
        {
            Debug.LogError("New farm map setup: Map Layout Preview prefab was not found.");
            return;
        }

        Tilemap source = preview.GetComponentInChildren<Tilemap>(true);
        if (source == null)
        {
            Debug.LogError("New farm map setup: preview prefab contains no Tilemap.");
            return;
        }

        EnsureFolder(OutputFolder);
        FarmGroundVariantTile dryTile = CreateVariantTile(DryTilePath, DrySpriteGuid);
        FarmGroundVariantTile wetTile = CreateVariantTile(WetTilePath, WetSpriteGuid);
        FarmGroundVariantTile farmableLogicTile = CreateVariantTile(LogicTilePath, null);
        FarmGroundVariantTile waterLogicTile = CreateVariantTile(WaterLogicTilePath, null, true);

        Scene scene = SceneManager.GetSceneByPath(LevelPath);
        bool openedByTool = !scene.IsValid() || !scene.isLoaded;
        if (openedByTool)
            scene = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Additive);

        try
        {
            GridManager manager = FindInScene<GridManager>(scene);
            if (manager == null)
                throw new InvalidOperationException("Grid Manager was not found in Level_Farm.");

            Grid grid = manager.GetComponent<Grid>();
            grid.cellSize = new Vector3(0.16f, 0.16f, 0f);
            grid.cellGap = Vector3.zero;

            Tilemap mapBase = FindOrCreateTilemap(manager.transform, MarkerName);
            CopyCentered(source, mapBase);
            ConfigureRenderer(mapBase, true, -9010);

            Tilemap dirt = GetSerializedTilemap(manager, "dirtTileMap");
            Tilemap dirtHole = GetSerializedTilemap(manager, "dirtHoleTileMap");
            Tilemap wet = GetSerializedTilemap(manager, "wateredDirtTileMap");
            Tilemap water = GetSerializedTilemap(manager, "waterTileMap");

            if (dirt == null || dirtHole == null || wet == null || water == null)
                throw new InvalidOperationException("One or more GridManager farming Tilemap references are missing.");

            dirt.ClearAllTiles();
            dirtHole.ClearAllTiles();
            wet.ClearAllTiles();
            water.ClearAllTiles();

            BuildLogicMasks(mapBase, dirt, water, farmableLogicTile, waterLogicTile);
            ConfigureRenderer(dirt, false, -9009);
            ConfigureRenderer(water, false, -9001);
            ConfigureRenderer(dirtHole, true, -8900);
            ConfigureRenderer(wet, true, -8800);

            SerializedObject serializedManager = new SerializedObject(manager);
            serializedManager.FindProperty("dirtTile").objectReferenceValue = farmableLogicTile;
            serializedManager.FindProperty("dirtHoleTile").objectReferenceValue = dryTile;
            serializedManager.FindProperty("wateredDirtTile").objectReferenceValue = wetTile;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();

            DisableLegacyMapVisuals(manager.transform, mapBase.gameObject);
            DisableLegacyWorldArt(scene);
            ConfigureMapBoundary(manager.transform, mapBase.cellBounds);
            ConfigureCameraBounds(scene, mapBase.cellBounds, grid.cellSize);
            RepositionNpcTests(scene);

            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            EditorPrefs.SetInt(GetVersionKey(), CurrentMapVersion);

            Debug.Log($"New farm map installed: {mapBase.cellBounds.size.x}x{mapBase.cellBounds.size.y}, " +
                      $"farmable cells={CountOccupiedCells(dirt)}, water cells={CountOccupiedCells(water)}.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (openedByTool && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static FarmGroundVariantTile CreateVariantTile(string path, string spriteGuid, bool gridCollider = false)
    {
        FarmGroundVariantTile tile = AssetDatabase.LoadAssetAtPath<FarmGroundVariantTile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<FarmGroundVariantTile>();
            AssetDatabase.CreateAsset(tile, path);
            tile.GenerateNewGuid();
        }

        Sprite[] sprites = string.IsNullOrEmpty(spriteGuid)
            ? new Sprite[0]
            : AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(spriteGuid))
                .OfType<Sprite>()
                .OrderBy(sprite => GetNumericSuffix(sprite.name))
                .ToArray();
        tile.Configure(sprites, gridCollider);
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static Tilemap FindOrCreateTilemap(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject target = existing != null ? existing.gameObject : new GameObject(name);
        if (existing == null)
        {
            target.transform.SetParent(parent, false);
            target.AddComponent<Tilemap>();
            target.AddComponent<TilemapRenderer>();
        }
        return target.GetComponent<Tilemap>();
    }

    private static void CopyCentered(Tilemap source, Tilemap destination)
    {
        destination.ClearAllTiles();
        BoundsInt sourceBounds = source.cellBounds;
        int offsetX = -(sourceBounds.xMin + sourceBounds.size.x / 2);
        int offsetY = -(sourceBounds.yMin + sourceBounds.size.y / 2);
        foreach (Vector3Int cell in sourceBounds.allPositionsWithin)
        {
            TileBase tile = source.GetTile(cell);
            if (tile != null)
                destination.SetTile(cell + new Vector3Int(offsetX, offsetY, 0), tile);
        }
        destination.CompressBounds();
    }

    private static void BuildLogicMasks(
        Tilemap mapBase,
        Tilemap farmable,
        Tilemap water,
        TileBase farmableTile,
        TileBase waterTile)
    {
        HashSet<string> changedTexturePaths = new HashSet<string>();
        try
        {
            HashSet<string> texturePaths = new HashSet<string>();
            foreach (Vector3Int cell in mapBase.cellBounds.allPositionsWithin)
            {
                Sprite sprite = (mapBase.GetTile(cell) as Tile)?.sprite;
                if (sprite != null)
                    texturePaths.Add(AssetDatabase.GetAssetPath(sprite.texture));
            }

            foreach (string path in texturePaths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    changedTexturePaths.Add(path);
                }
            }

            foreach (Vector3Int cell in mapBase.cellBounds.allPositionsWithin)
            {
                TileBase tileBase = mapBase.GetTile(cell);
                if (tileBase == null)
                    continue;

                string tilePath = AssetDatabase.GetAssetPath(tileBase);
                Sprite sprite = (tileBase as Tile)?.sprite;
                if (sprite == null)
                    continue;

                Color32 center = ReadSpriteCenter(sprite);
                bool pondPaletteTile = tilePath.Replace('\\', '/').Contains("/Pond Tiles/");
                if (pondPaletteTile || IsWater(center))
                    water.SetTile(cell, waterTile);
                else if (IsFarmSoil(center))
                    farmable.SetTile(cell, farmableTile);
            }
        }
        finally
        {
            foreach (string path in changedTexturePaths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }
    }

    private static Color32 ReadSpriteCenter(Sprite sprite)
    {
        Rect rect = sprite.rect;
        return sprite.texture.GetPixel(
            Mathf.Clamp(Mathf.FloorToInt(rect.x + rect.width * 0.5f), 0, sprite.texture.width - 1),
            Mathf.Clamp(Mathf.FloorToInt(rect.y + rect.height * 0.5f), 0, sprite.texture.height - 1));
    }

    private static bool IsFarmSoil(Color32 color)
    {
        float greenRatio = color.g / Mathf.Max(1f, color.r);
        float blueRatio = color.b / Mathf.Max(1f, color.r);
        return color.r >= 125 && color.r <= 195 &&
               color.g >= 75 && color.g <= 140 &&
               color.b <= 70 &&
               greenRatio >= 0.57f && greenRatio <= 0.78f &&
               blueRatio >= 0.08f && blueRatio <= 0.38f;
    }

    private static bool IsWater(Color32 color)
    {
        return color.b >= 120 && color.g >= 95 && color.b > color.r + 25 && color.g > color.r + 20;
    }

    private static Tilemap GetSerializedTilemap(GridManager manager, string propertyName)
    {
        return new SerializedObject(manager).FindProperty(propertyName)?.objectReferenceValue as Tilemap;
    }

    private static void ConfigureRenderer(Tilemap tilemap, bool enabled, int order)
    {
        TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
        if (renderer == null)
            renderer = tilemap.gameObject.AddComponent<TilemapRenderer>();
        renderer.enabled = enabled;
        renderer.sortingOrder = order;
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        EditorUtility.SetDirty(renderer);
    }

    private static void DisableLegacyMapVisuals(Transform manager, GameObject mapBase)
    {
        string[] legacyNames = { "Grass", "Cliffs", "Shadows", "Fence" };
        foreach (Tilemap tilemap in manager.GetComponentsInChildren<Tilemap>(true))
        {
            if (tilemap.gameObject == mapBase || !legacyNames.Contains(tilemap.name))
                continue;
            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer != null)
                renderer.enabled = false;
            Collider2D collider = tilemap.GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;
        }
    }

    private static void DisableLegacyWorldArt(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == "Tree" || transform.name == "House")
                    transform.gameObject.SetActive(false);
            }
        }
    }

    private static void ConfigureMapBoundary(Transform parent, BoundsInt bounds)
    {
        Transform existing = parent.Find("Map Boundary Collision");
        GameObject target = existing != null ? existing.gameObject : new GameObject("Map Boundary Collision");
        if (existing == null)
            target.transform.SetParent(parent, false);

        EdgeCollider2D edge = target.GetComponent<EdgeCollider2D>();
        if (edge == null)
            edge = target.AddComponent<EdgeCollider2D>();

        const float size = 0.16f;
        float left = bounds.xMin * size;
        float right = bounds.xMax * size;
        float bottom = bounds.yMin * size;
        float top = bounds.yMax * size;
        edge.points = new[]
        {
            new Vector2(left, bottom), new Vector2(right, bottom),
            new Vector2(right, top), new Vector2(left, top), new Vector2(left, bottom)
        };
    }

    private static void ConfigureCameraBounds(Scene scene, BoundsInt bounds, Vector3 cellSize)
    {
        GameObject confinement = FindGameObject(scene, "CameraConfinement");
        PolygonCollider2D polygon = confinement != null ? confinement.GetComponent<PolygonCollider2D>() : null;
        if (polygon == null)
            return;

        float left = bounds.xMin * cellSize.x;
        float right = bounds.xMax * cellSize.x;
        float bottom = bounds.yMin * cellSize.y;
        float top = bounds.yMax * cellSize.y;
        polygon.pathCount = 1;
        polygon.SetPath(0, new[]
        {
            new Vector2(left, bottom), new Vector2(left, top),
            new Vector2(right, top), new Vector2(right, bottom)
        });
        confinement.transform.position = Vector3.zero;
    }

    private static void RepositionNpcTests(Scene scene)
    {
        GameObject grandpa = FindGameObject(scene, "NPC Old Man Test");
        GameObject seller = FindGameObject(scene, "NPC Seed Seller Test");
        if (grandpa != null)
            grandpa.transform.position = new Vector3(-2.35f, 0.55f, grandpa.transform.position.z);
        if (seller != null)
            seller.transform.position = new Vector3(-3.15f, 0.55f, seller.transform.position.z);
    }

    private static GameObject FindGameObject(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (match != null)
                return match.gameObject;
        }
        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T result = root.GetComponentInChildren<T>(true);
            if (result != null)
                return result;
        }
        return null;
    }

    private static int GetNumericSuffix(string name)
    {
        int separator = name.LastIndexOf('_');
        return separator >= 0 && int.TryParse(name.Substring(separator + 1), out int value) ? value : int.MaxValue;
    }

    private static int CountOccupiedCells(Tilemap tilemap)
    {
        int count = 0;
        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.HasTile(cell))
                count++;
        }
        return count;
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
