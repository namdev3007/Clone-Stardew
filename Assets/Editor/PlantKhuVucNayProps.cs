#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using World;

/// <summary>
/// Plants trees, bushes, and grass in 'khu vực này 1' and 'khu vực này 2'
/// avoiding overlaps with existing props and with each other.
/// </summary>
public static class PlantKhuVucNayProps
{
    private const string ScenePath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string RootName = "Khu Vuc Nay Props (Generated)";
    private const string SpriteFolder = "Assets/Sprites/props-items/trang trí map-tách riêng";
    private const string VersionKey = "Meadom.KhuVucNay.Props.Version";
    private const int TargetVersion = 1;
    private const float CellSize = 0.16f;

    private readonly struct PropDefinition
    {
        public readonly int x;
        public readonly int y;
        public readonly string spriteName;
        public readonly string regionName;
        public readonly bool isTreeOrBush;

        public PropDefinition(int x, int y, string spriteName, string regionName, bool isTreeOrBush)
        {
            this.x = x;
            this.y = y;
            this.spriteName = spriteName;
            this.regionName = regionName;
            this.isTreeOrBush = isTreeOrBush;
        }
    }

    private static readonly PropDefinition[] Placements =
    {
        // Region 1: (-30, 28) to (7, 30) - Trees
        new(-28, 29, "cay-lon-1", "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-24, 28, "cay-vua-1", "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-20, 30, "cay-lon-2", "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-16, 28, "cay-nho-1", "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-12, 29, "cay-lon-1", "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-8,  30, "cay-vua-1", "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-3,  29, "cay-lon-2", "trồng thêm cây và cỏ cho khu vực này 1", true),
        new( 1,  28, "cay-nho-1", "trồng thêm cây và cỏ cho khu vực này 1", true),
        new( 5,  30, "cay-lon-1", "trồng thêm cây và cỏ cho khu vực này 1", true),

        // Region 1 - Bushes
        new(-26, 30, "bui-cay",   "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-22, 29, "bui-cay",   "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-18, 29, "bui-cay",   "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-14, 30, "bui-cay",   "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-10, 28, "bui-cay",   "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-5,  28, "bui-cay",   "trồng thêm cây và cỏ cho khu vực này 1", true),
        new(-1,  30, "bui-cay",   "trồng thêm cây và cỏ cho khu vực này 1", true),
        new( 3,  29, "bui-cay",   "trồng thêm cây và cỏ cho khu vực này 1", true),

        // Region 1 - Grass
        new(-30, 28, "co-cao",    "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-29, 30, "co-nho-1",  "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-27, 28, "co-nho-2",  "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-25, 29, "co-cao",    "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-23, 30, "co-nho-1",  "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-21, 28, "co-cao",    "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-19, 30, "co-nho-2",  "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-17, 29, "co-cao",    "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-15, 28, "co-nho-1",  "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-13, 30, "co-cao",    "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-11, 29, "co-nho-2",  "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-9,  28, "co-cao",    "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-7,  29, "co-nho-1",  "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-6,  30, "co-nho-2",  "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-4,  28, "co-cao",    "trồng thêm cây và cỏ cho khu vực này 1", false),
        new(-2,  28, "co-nho-1",  "trồng thêm cây và cỏ cho khu vực này 1", false),
        new( 0,  29, "co-cao",    "trồng thêm cây và cỏ cho khu vực này 1", false),
        new( 2,  30, "co-nho-2",  "trồng thêm cây và cỏ cho khu vực này 1", false),
        new( 4,  28, "co-cao",    "trồng thêm cây và cỏ cho khu vực này 1", false),
        new( 6,  29, "co-nho-1",  "trồng thêm cây và cỏ cho khu vực này 1", false),
        new( 7,  28, "co-cao",    "trồng thêm cây và cỏ cho khu vực này 1", false),

        // Region 2: (-14, 44) to (7, 44) - Trees
        new(-7, 44, "cay-lon-1", "trồng thêm cây và cỏ cho khu vực này 2", true),
        new(-1, 44, "cay-vua-1", "trồng thêm cây và cỏ cho khu vực này 2", true),
        new( 5, 44, "cay-lon-2", "trồng thêm cây và cỏ cho khu vực này 2", true),

        // Region 2 - Bushes
        new(-12, 44, "bui-cay",  "trồng thêm cây và cỏ cho khu vực này 2", true),
        new(-9,  44, "bui-cay",  "trồng thêm cây và cỏ cho khu vực này 2", true),
        new( 2,  44, "bui-cay",  "trồng thêm cây và cỏ cho khu vực này 2", true),

        // Region 2 - Grass
        new(-14, 44, "co-cao",   "trồng thêm cây và cỏ cho khu vực này 2", false),
        new(-11, 44, "co-nho-1", "trồng thêm cây và cỏ cho khu vực này 2", false),
        new(-8,  44, "co-nho-2", "trồng thêm cây và cỏ cho khu vực này 2", false),
        new(-6,  44, "co-cao",   "trồng thêm cây và cỏ cho khu vực này 2", false),
        new(-3,  44, "co-nho-1", "trồng thêm cây và cỏ cho khu vực này 2", false),
        new(-2,  44, "co-nho-2", "trồng thêm cây và cỏ cho khu vực này 2", false),
        new( 0,  44, "co-cao",   "trồng thêm cây và cỏ cho khu vực này 2", false),
        new( 1,  44, "co-nho-1", "trồng thêm cây và cỏ cho khu vực này 2", false),
        new( 3,  44, "co-nho-2", "trồng thêm cây và cỏ cho khu vực này 2", false),
        new( 4,  44, "co-cao",   "trồng thêm cây và cỏ cho khu vực này 2", false),
    };

    [InitializeOnLoadMethod]
    private static void ApplyOnceAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetInt(VersionKey, 0) >= TargetVersion ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Apply();
            EditorPrefs.SetInt(VersionKey, TargetVersion);
        };
    }

    [MenuItem("Tools/Map/Plant Props in Khu Vuc Nay 1 and 2")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            GridManager gridManager = scene.GetRootGameObjects()
                .Select(root => root.GetComponentInChildren<GridManager>(true))
                .FirstOrDefault(manager => manager != null);

            if (gridManager == null)
            {
                Debug.LogError("Could not find GridManager in Level_Farm.");
                return;
            }

            Transform mapPropsGroup = gridManager.transform.Find("Map Props (Generated)");
            Transform parentTransform = mapPropsGroup != null ? mapPropsGroup : gridManager.transform;

            // Remove previous generated group if any
            Transform oldRoot = parentTransform.Find(RootName);
            if (oldRoot != null)
                Undo.DestroyObjectImmediate(oldRoot.gameObject);

            GameObject rootObject = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(rootObject, "Plant Props in Khu Vuc Nay");
            rootObject.transform.SetParent(parentTransform, false);

            GameObject r1Group = new GameObject("Khu Vuc Nay 1");
            r1Group.transform.SetParent(rootObject.transform, false);

            GameObject r2Group = new GameObject("Khu Vuc Nay 2");
            r2Group.transform.SetParent(rootObject.transform, false);

            Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            Material defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

            int placedCount = 0;
            foreach (PropDefinition def in Placements)
            {
                if (!spriteCache.TryGetValue(def.spriteName, out Sprite sprite))
                {
                    string path = $"{SpriteFolder}/{def.spriteName}.png";
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    if (sprite == null)
                    {
                        Debug.LogWarning($"Could not load sprite at {path}");
                        continue;
                    }
                    spriteCache[def.spriteName] = sprite;
                }

                Transform parent = def.y == 44 ? r2Group.transform : r1Group.transform;
                Vector3 worldAnchor = new Vector3(
                    def.x * CellSize + CellSize * 0.5f,
                    def.y * CellSize,
                    0f);

                Bounds bounds = sprite.bounds;
                Vector3 bottomCenterOffset = new Vector3(bounds.center.x, bounds.min.y, 0f);
                Vector3 position = worldAnchor - bottomCenterOffset;

                GameObject prop = new GameObject($"[{def.x},{def.y}] {sprite.name}");
                prop.transform.SetParent(parent, false);
                prop.transform.position = position;
                prop.transform.localScale = Vector3.one;

                SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = Color.white;
                if (defaultMaterial != null)
                    renderer.sharedMaterial = defaultMaterial;
                renderer.sortingLayerName = MapPropSorting.SortingLayer;
                renderer.sortingOrder = MapPropSorting.GetSortingOrder(sprite, worldAnchor.y);

                if (def.isTreeOrBush)
                {
                    BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
                    collider.size = new Vector2(
                        Mathf.Max(0.12f, bounds.size.x * 0.42f),
                        Mathf.Clamp(bounds.size.y * 0.16f, 0.08f, 0.24f));
                    collider.offset = new Vector2(bounds.center.x, bounds.min.y + collider.size.y * 0.5f);
                }

                MapRegionGeneratedProp tracking = prop.AddComponent<MapRegionGeneratedProp>();
                Vector3Int cell = new Vector3Int(def.x, def.y, 0);
                tracking.Initialize(def.regionName, cell, cell, $"khuvucnay_{def.x}_{def.y}");

                placedCount++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"SUCCESS: Planted {placedCount} props in 'khu vực này 1' and 'khu vực này 2' in Level_Farm.unity.");
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }
}
#endif
