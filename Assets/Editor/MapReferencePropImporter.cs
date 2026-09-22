#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using World;

namespace World.Editor
{
    /// <summary>
    /// Implements the 16x16 pixel reference map analysis and prop placement workflow
    /// specified in docs/MAP_TREE_PLACEMENT_16X16_HANDOFF.md.
    /// Scans 'full trang tri-1.png', aligns by 16x16 grid cells to anchor positions
    /// (ground contact at trunk/base), validates forbidden zones/paths, generates
    /// a manifest ScriptableObject, and instantiates GameObjects into Level_Farm.unity.
    /// </summary>
    public sealed class MapReferencePropImporter : EditorWindow
    {
        public const string ReferencePath = "Assets/Sprites/tileset/full trang tri-1.png";
        public const string AltReferencePath = "Assets/Sprites/tileset/full trang tri map.png";
        public const string BaseMapPath = "Assets/Sprites/bố cục map.png";
        public const string PreviewMapPath = "Assets/Tiles/Map Layout/Map Layout Preview.prefab";
        public const string LevelFarmPath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
        public const string ManifestFolder = "Assets/Settings/Map Decorations";
        public const string ManifestPath = ManifestFolder + "/Map Prop Placement Manifest.asset";
        public const string RegionCollectionPath = "Assets/Settings/Map Regions/Map Region Collection.asset";
        public const string BananaSeparatedFolder = "Assets/Sprites/cây trồng/cây lâu năm/banana/bananatree_200-tách riêng";
        public const string GeneratedGroupName = "Map Props (Generated)";

        public const float PixelsPerUnit = 100f;
        public const float MapLeftWorld = -8.96f;
        public const float MapTopWorld = 7.20f;
        public const int ReferenceTopCrop = 0; // The newly painted six top rows are now part of the map.
        private const int LegacyBaseMapTopPadding = 96;
        private const int PreviewCellOffsetX = -56;
        private const int PreviewCellOffsetY = -38;
        public const float CellWorldSize = 0.16f; // 16 pixels / 100 PPU

        private static readonly string[] PropTexturePaths =
        {
            "Assets/Sprites/props-items/trang trí map-tách riêng/cay-nho-1.png",
            "Assets/Sprites/props-items/trang trí map-tách riêng/cay-vua-1.png",
            "Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-1.png",
            "Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-2.png",
            "Assets/Sprites/props-items/trang trí map-tách riêng/co-cao.png",
            "Assets/Sprites/props-items/trang trí map-tách riêng/co-nho-1.png",
            "Assets/Sprites/props-items/trang trí map-tách riêng/co-nho-2.png",
            "Assets/Sprites/props-items/trang trí map-tách riêng/bui-cay.png",
            "Assets/Sprites/props-items/cay hoa gạo-Sheet.png",
            "Assets/Sprites/props-items/cây-chết.png",
            "Assets/Sprites/props-items/lu nước.png",
            "Assets/Sprites/props-items/ụ lúa-tách riêng/u-lua-vang.png",
            "Assets/Sprites/props-items/ụ lúa-tách riêng/u-lua-xanh.png"
        };

        private MapPropPlacementManifest manifest;
        private bool previewAnchors;
        private Vector2 scrollPos;
        private string statusMessage = "Sẵn sàng phân tích hoặc nạp manifest.";

        private readonly struct PixelPoint
        {
            public readonly int x;
            public readonly int y;
            public readonly Color32 color;

            public PixelPoint(int x, int y, Color32 color)
            {
                this.x = x;
                this.y = y;
                this.color = color;
            }
        }

        private readonly struct RawMatch
        {
            public readonly Sprite sprite;
            public readonly int left;
            public readonly int bottom;
            public readonly float scale;

            public RawMatch(Sprite sprite, int left, int bottom, float scale = 1f)
            {
                this.sprite = sprite;
                this.left = left;
                this.bottom = bottom;
                this.scale = scale;
            }

            public int Width => Mathf.RoundToInt(sprite.rect.width * scale);
            public int Height => Mathf.RoundToInt(sprite.rect.height * scale);
        }

        private const string VersionKey = "Meadom.16x16ReferenceProps.LayoutVersion";
        private const int TargetLayoutVersion = 13;
        private const string NamedForestGrassVersionKey = "Meadom.NamedForestGrass.Version";
        private const int NamedForestGrassTargetVersion = 1;
        private const string ForestDetailsVersionKey = "Meadom.ForestDetails.Version";
        private const int ForestDetailsTargetVersion = 2;

        [InitializeOnLoadMethod]
        private static void AutoRunOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetInt(VersionKey, 0) >= TargetLayoutVersion)
                    return;

                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.playModeStateChanged -= ApplyPendingLayoutAfterPlayMode;
                    EditorApplication.playModeStateChanged += ApplyPendingLayoutAfterPlayMode;
                    EditorApplication.isPlaying = false;
                    return;
                }

                ExecutePendingLayout();
            };
        }

        [InitializeOnLoadMethod]
        private static void AutoApplyNamedForestGrassOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetInt(NamedForestGrassVersionKey, 0) >= NamedForestGrassTargetVersion)
                    return;

                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.playModeStateChanged -= ApplyPendingNamedForestGrassAfterPlayMode;
                    EditorApplication.playModeStateChanged += ApplyPendingNamedForestGrassAfterPlayMode;
                    EditorApplication.isPlaying = false;
                    return;
                }

                ExecutePendingNamedForestGrass();
            };
        }

        [InitializeOnLoadMethod]
        private static void AutoApplyForestDetailsOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorPrefs.GetInt(ForestDetailsVersionKey, 0) >= ForestDetailsTargetVersion)
                    return;
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.playModeStateChanged -= ApplyPendingForestDetailsAfterPlayMode;
                    EditorApplication.playModeStateChanged += ApplyPendingForestDetailsAfterPlayMode;
                    return;
                }
                ExecutePendingForestDetails();
            };
        }

        private static void ApplyPendingForestDetailsAfterPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;
            EditorApplication.playModeStateChanged -= ApplyPendingForestDetailsAfterPlayMode;
            EditorApplication.delayCall += ExecutePendingForestDetails;
        }

        private static void ExecutePendingForestDetails()
        {
            if (EditorPrefs.GetInt(ForestDetailsVersionKey, 0) >= ForestDetailsTargetVersion)
                return;
            if (ApplyForestDetailsToScene())
                EditorPrefs.SetInt(ForestDetailsVersionKey, ForestDetailsTargetVersion);
        }

        private static void ApplyPendingNamedForestGrassAfterPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            EditorApplication.playModeStateChanged -= ApplyPendingNamedForestGrassAfterPlayMode;
            EditorApplication.delayCall += ExecutePendingNamedForestGrass;
        }

        private static void ExecutePendingNamedForestGrass()
        {
            if (EditorPrefs.GetInt(NamedForestGrassVersionKey, 0) >= NamedForestGrassTargetVersion)
                return;

            ApplyNamedForestGrassOnly();
            EditorPrefs.SetInt(NamedForestGrassVersionKey, NamedForestGrassTargetVersion);
        }

        private static void ApplyPendingLayoutAfterPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            EditorApplication.playModeStateChanged -= ApplyPendingLayoutAfterPlayMode;
            EditorApplication.delayCall += ExecutePendingLayout;
        }

        private static void ExecutePendingLayout()
        {
            if (EditorPrefs.GetInt(VersionKey, 0) >= TargetLayoutVersion)
                return;
            ExecuteFullWorkflow();
            EditorPrefs.SetInt(VersionKey, TargetLayoutVersion);
        }

        [MenuItem("Tools/Map/Import Trees From 16x16 Reference")]
        public static void OpenWindow()
        {
            GetWindow<MapReferencePropImporter>("16x16 Prop Importer");
        }

        [MenuItem("Tools/Map/Run 16x16 Reference Import (Batch)")]
        public static void RunBatchImport()
        {
            ExecuteFullWorkflow();
            EditorPrefs.SetInt(VersionKey, TargetLayoutVersion);
        }

        [MenuItem("Tools/Map/Add Grass To Named Forest Regions")]
        public static void ApplyNamedForestGrassOnly()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play Mode before adding forest grass.");
                return;
            }

            MapRegionCollection regions = AssetDatabase.LoadAssetAtPath<MapRegionCollection>(RegionCollectionPath);
            Texture2D reference = AssetDatabase.LoadAssetAtPath<Texture2D>(ReferencePath);
            if (regions == null || reference == null)
                throw new InvalidOperationException("Missing forest regions or reference image.");

            List<TextureImporter> importersToRestore = new List<TextureImporter>();
            MakeReadable(BaseMapPath, importersToRestore);
            MakeReadable(ReferencePath, importersToRestore);
            MakeReadable(AltReferencePath, importersToRestore);

            Scene scene = SceneManager.GetSceneByPath(LevelFarmPath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(LevelFarmPath, OpenSceneMode.Additive);

            try
            {
                GridManager manager = FindInScene<GridManager>(scene);
                Transform generated = manager != null ? manager.transform.Find(GeneratedGroupName) : null;
                if (generated == null)
                    throw new InvalidOperationException("Map Props (Generated) is missing from Level_Farm.");

                const string grassGroupName = "Named Forest Grass (Generated)";
                HashSet<Vector3Int> occupied = new HashSet<Vector3Int>(
                    generated.GetComponentsInChildren<MapRegionGeneratedProp>(true)
                        .Select(prop => prop.TargetCell));
                List<RawMatch> grasses = ScatterForestGrass(
                    regions, LoadGrassCellsFromLevelFarm(), occupied, reference.height);

                Transform grassGroup = generated.Find(grassGroupName);
                if (grassGroup == null)
                {
                    GameObject group = new GameObject(grassGroupName);
                    Undo.RegisterCreatedObjectUndo(group, "Add forest grass");
                    group.transform.SetParent(generated, false);
                    grassGroup = group.transform;
                }
                Material defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

                foreach (RawMatch match in grasses)
                {
                    Sprite sprite = match.sprite;
                    Vector3Int cell = CalculateTargetCell(match, reference.height);
                    float anchorPixelX = match.left + match.Width * 0.5f;
                    float bottomFromTop = reference.height - match.bottom - ReferenceTopCrop;
                    Vector3 anchor = new Vector3(
                        MapLeftWorld + anchorPixelX / PixelsPerUnit,
                        MapTopWorld - bottomFromTop / PixelsPerUnit, 0f);

                    GameObject prop = new GameObject($"Forest Grass {cell.x},{cell.y} - {sprite.name}");
                    prop.transform.SetParent(grassGroup, false);
                    prop.transform.position = anchor - new Vector3(sprite.bounds.center.x, sprite.bounds.min.y, 0f);
                    SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.sortingLayerName = "Dynamic";
                    renderer.sortingOrder = MapPropSorting.GetSortingOrder(sprite, anchor.y);
                    if (defaultMaterial != null)
                        renderer.sharedMaterial = defaultMaterial;

                    MapRegionGeneratedProp tracking = prop.AddComponent<MapRegionGeneratedProp>();
                    tracking.Initialize("Named Forest Grass", cell, cell, $"forest_grass_{cell.x}_{cell.y}");
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Added {grasses.Count} grass props to the named sparse and dense forest regions.");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                foreach (TextureImporter importer in importersToRestore)
                {
                    if (importer == null)
                        continue;
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }

        [MenuItem("Tools/Map/Add Grass Cluster And Sparse Forest Bushes")]
        public static void ApplyForestDetailsMenu() => ApplyForestDetailsToScene();

        [MenuItem("Tools/Map/Add Trees Around Map Prop 019")]
        public static void AddTreesAroundMapProp019Menu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Exit Play Mode before adding trees around Map Prop 019.");
                return;
            }

            List<TextureImporter> importersToRestore = new List<TextureImporter>();
            MakeReadable("Assets/Sprites/tileset/full trang tri map.png", importersToRestore);
            Scene scene = SceneManager.GetSceneByPath(LevelFarmPath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(LevelFarmPath, OpenSceneMode.Additive);

            try
            {
                GridManager manager = FindInScene<GridManager>(scene);
                Transform generated = manager != null ? manager.transform.Find(GeneratedGroupName) : null;
                if (generated == null)
                {
                    Debug.LogWarning("Cannot add trees: Map Props (Generated) is missing.");
                    return;
                }

                MapRegionGeneratedProp[] existing = generated.GetComponentsInChildren<MapRegionGeneratedProp>(true);
                if (!existing.Any(prop => prop.name == "Map Prop 019 - cay-vua-1" &&
                                          prop.TargetCell == new Vector3Int(-29, 41, 0)))
                {
                    Debug.LogWarning("Cannot add trees: Map Prop 019 is not at cell (-29, 41).");
                    return;
                }

                Tilemap[] tilemaps = manager.GetComponentsInChildren<Tilemap>(true);
                Tilemap grass = tilemaps.FirstOrDefault(tilemap => tilemap.name == "Grass");
                Tilemap layout = tilemaps.FirstOrDefault(tilemap => tilemap.name == "Map Layout Base");
                Dictionary<Texture2D, Color32[]> pixelCache = new Dictionary<Texture2D, Color32[]>();
                List<Vector3Int> treeCells = existing.Where(prop =>
                    {
                        string name = prop.GetComponentInChildren<SpriteRenderer>()?.sprite?.name ?? string.Empty;
                        return name.StartsWith("cay-") || name.StartsWith("bananatree");
                    }).Select(prop => prop.TargetCell).ToList();
                HashSet<Vector3Int> occupiedByLargerProps = existing.Where(prop =>
                    !(prop.GetComponentInChildren<SpriteRenderer>()?.sprite?.name ?? string.Empty)
                    .StartsWith("co-")).Select(prop => prop.TargetCell).ToHashSet();

                Sprite[] sprites =
                {
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/cay-vua-1.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/cay-nho-1.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-1.png")
                };
                if (sprites.Any(sprite => sprite == null))
                {
                    Debug.LogWarning("Cannot add trees: one or more decoration sprites are missing.");
                    return;
                }

                const string groupName = "Trees Around Map Prop 019 (Generated)";
                Transform group = generated.Find(groupName);
                if (group == null)
                {
                    GameObject container = new GameObject(groupName);
                    Undo.RegisterCreatedObjectUndo(container, "Add trees around Map Prop 019");
                    group = container.transform;
                    group.SetParent(generated, false);
                }

                Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                int left = 0;
                int right = 0;
                // Check both sides of the reference trunk, using nearby rows only
                // when the centre row already contains a tree or another large prop.
                for (int x = -47; x <= -11; x += 3)
                {
                    if (Mathf.Abs(x + 29) <= 2)
                        continue;
                    foreach (int y in new[] { 41, 42, 40 })
                    {
                        Vector3Int cell = new Vector3Int(x, y, 0);
                        Vector3 worldAnchor = new Vector3(
                            (cell.x + 0.5f) * CellWorldSize,
                            cell.y * CellWorldSize + 0.02f, 0f);
                        bool isGrass = grass != null && grass.HasTile(grass.WorldToCell(worldAnchor));
                        if (!isGrass && layout != null)
                            isGrass = IsMostlyGrassTile(layout.GetSprite(layout.WorldToCell(worldAnchor)), pixelCache);
                        if (!isGrass)
                            continue;
                        if (occupiedByLargerProps.Contains(cell))
                            continue;
                        if (treeCells.Any(tree => Mathf.Abs(tree.x - x) <= 2 && Mathf.Abs(tree.y - y) <= 1))
                            continue;

                        int spriteIndex = Mathf.Abs(x / 3) % sprites.Length;
                        AddForestDetail(group, sprites[spriteIndex], cell, "Top Tree Row", material, true);
                        treeCells.Add(cell);
                        occupiedByLargerProps.Add(cell);
                        if (x < -29) left++; else right++;
                        break;
                    }
                }

                if (left + right > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                Debug.Log($"Map Prop 019: added {left} trees to the left and {right} to the right in Level_Farm.");
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                foreach (TextureImporter importer in importersToRestore)
                {
                    if (importer == null)
                        continue;
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }

        private static bool ApplyForestDetailsToScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return false;

            Scene scene = SceneManager.GetSceneByPath(LevelFarmPath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(LevelFarmPath, OpenSceneMode.Additive);

            try
            {
                GridManager manager = FindInScene<GridManager>(scene);
                Transform generated = manager != null ? manager.transform.Find(GeneratedGroupName) : null;
                if (generated == null)
                {
                    Debug.LogWarning("Cannot add forest details: Map Props (Generated) is missing.");
                    return false;
                }

                int added = AddForestDetails(generated, LoadGrassCellsFromLevelFarm());
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"Added {added} grass and bush props to Level_Farm.");
                return true;
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static int AddForestDetails(Transform generated, HashSet<Vector3Int> grassCells)
        {
            MapRegionCollection regions = AssetDatabase.LoadAssetAtPath<MapRegionCollection>(RegionCollectionPath);
            MapRegionDefinition sparseForest = regions?.Regions.FirstOrDefault(IsSparseForestRegion);
            Sprite bush = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Sprites/props-items/trang trí map-tách riêng/bui-cay.png");
            Sprite tallGrass = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Sprites/props-items/trang trí map-tách riêng/co-cao.png");
            Sprite shortGrass = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Sprites/props-items/trang trí map-tách riêng/co-nho-1.png");
            if (sparseForest == null || bush == null || tallGrass == null || shortGrass == null || grassCells == null)
                return 0;

            const string groupName = "Sparse Forest Details (Generated)";
            Transform previous = generated.Find(groupName);
            if (previous != null)
            {
                if (IsSelectionInside(Selection.activeObject, previous))
                    Selection.activeObject = null;
                Undo.DestroyObjectImmediate(previous.gameObject);
            }

            HashSet<Vector3Int> occupied = new HashSet<Vector3Int>();
            HashSet<Vector3Int> occupiedByLargerProps = new HashSet<Vector3Int>();
            List<Vector3Int> treeCells = new List<Vector3Int>();
            List<Vector3Int> bushCells = new List<Vector3Int>();
            foreach (MapRegionGeneratedProp prop in generated.GetComponentsInChildren<MapRegionGeneratedProp>(true))
            {
                Vector3Int cell = prop.TargetCell;
                occupied.Add(cell);
                string spriteName = prop.GetComponentInChildren<SpriteRenderer>()?.sprite?.name ?? string.Empty;
                if (spriteName.StartsWith("cay-") || spriteName.StartsWith("bananatree"))
                    treeCells.Add(cell);
                if (!spriteName.StartsWith("co-"))
                    occupiedByLargerProps.Add(cell);
                if (spriteName == "bui-cay")
                    bushCells.Add(cell);
            }

            GameObject group = new GameObject(groupName);
            Undo.RegisterCreatedObjectUndo(group, "Add sparse forest details");
            group.transform.SetParent(generated, false);
            Material material = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            int added = 0;

            // A few bushes across the named sparse forest. Keep space around tree
            // trunks and other bushes so the forest remains noticeably sparse.
            for (int y = sparseForest.MinCell.y; y <= sparseForest.MaxCell.y; y++)
            for (int x = sparseForest.MinCell.x; x <= sparseForest.MaxCell.x; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (!grassCells.Contains(cell) || occupied.Contains(cell) ||
                    ForestDetailHash(x, y, 0x42555348u) % 100u >= 6u ||
                    treeCells.Any(tree => Mathf.Abs(tree.x - x) <= 2 && Mathf.Abs(tree.y - y) <= 2) ||
                    bushCells.Any(other => Mathf.Abs(other.x - x) <= 2 && Mathf.Abs(other.y - y) <= 2))
                    continue;

                AddForestDetail(group.transform, bush, cell, "Sparse Forest Bush", material);
                bushCells.Add(cell);
                occupied.Add(cell);
                added++;
            }

            // Fill a compact patch around Forest Grass 27,32 while retaining the
            // existing tall grass there. Only paint actual grass-ground cells.
            for (int y = 27; y <= 37; y++)
            for (int x = 22; x <= 32; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                int distance = Mathf.Abs(x - 27) + Mathf.Abs(y - 32);
                int threshold = distance <= 3 ? 83 : distance <= 6 ? 60 : 36;
                uint hash = ForestDetailHash(x, y, 0x47524153u);
                if (!sparseForest.Contains(cell) || !grassCells.Contains(cell) || occupied.Contains(cell) ||
                    hash % 100u >= threshold ||
                    treeCells.Any(tree => Mathf.Abs(tree.x - x) <= 1 && Mathf.Abs(tree.y - y) <= 1))
                    continue;

                Sprite sprite = hash % 4u == 0u ? shortGrass : tallGrass;
                AddForestDetail(group.transform, sprite, cell, "Forest Grass Cluster", material);
                occupied.Add(cell);
                added++;
            }

            // Map Prop 019 is the authored tree at (-29,41). Thicken its row
            // horizontally in both directions without changing the reference tree.
            if (generated.GetComponentsInChildren<MapRegionGeneratedProp>(true).Any(prop =>
                    prop.name == "Map Prop 019 - cay-vua-1" &&
                    prop.TargetCell == new Vector3Int(-29, 41, 0)))
            {
                Sprite[] rowTrees =
                {
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        "Assets/Sprites/props-items/trang trí map-tách riêng/cay-vua-1.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        "Assets/Sprites/props-items/trang trí map-tách riêng/cay-nho-1.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>(
                        "Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-1.png")
                };
                for (int x = -50; x <= 1; x += 3)
                {
                    Vector3Int cell = new Vector3Int(x, 41, 0);
                    if (!grassCells.Contains(cell) || occupiedByLargerProps.Contains(cell) ||
                        treeCells.Any(tree => Mathf.Abs(tree.x - x) <= 2 && Mathf.Abs(tree.y - 41) <= 1))
                        continue;
                    Sprite sprite = rowTrees[(x + 50) / 3 % rowTrees.Length];
                    if (sprite == null)
                        continue;
                    AddForestDetail(group.transform, sprite, cell, "Top Tree Row", material, true);
                    treeCells.Add(cell);
                    occupiedByLargerProps.Add(cell);
                    added++;
                }
            }

            return added;
        }

        private static uint ForestDetailHash(int x, int y, uint salt)
        {
            uint hash = unchecked((uint)(x * 73856093 ^ y * 19349663)) ^ salt;
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            hash ^= hash >> 15;
            return hash;
        }

        private static void AddForestDetail(Transform parent, Sprite sprite, Vector3Int cell,
            string regionName, Material material, bool addCollider = false)
        {
            Vector3 anchor = new Vector3(
                (cell.x + 0.5f) * CellWorldSize,
                cell.y * CellWorldSize + 0.02f, 0f);
            GameObject prop = new GameObject($"{regionName} {cell.x},{cell.y} - {sprite.name}");
            prop.transform.SetParent(parent, false);
            prop.transform.position = anchor - new Vector3(sprite.bounds.center.x, sprite.bounds.min.y, 0f);
            SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Dynamic";
            renderer.sortingOrder = MapPropSorting.GetSortingOrder(sprite, anchor.y);
            if (material != null)
                renderer.sharedMaterial = material;

            if (addCollider)
            {
                Bounds bounds = sprite.bounds;
                BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(Mathf.Max(0.12f, bounds.size.x * 0.42f),
                    Mathf.Clamp(bounds.size.y * 0.16f, 0.08f, 0.24f));
                collider.offset = new Vector2(bounds.center.x,
                    bounds.min.y + collider.size.y * 0.5f);
            }

            MapRegionGeneratedProp tracking = prop.AddComponent<MapRegionGeneratedProp>();
            tracking.Initialize(regionName, cell, cell, $"forest_detail_{cell.x}_{cell.y}");
        }

        private void OnEnable()
        {
            LoadManifestAsset();
            SceneView.duringSceneGui += DuringSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
        }

        private void OnGUI()
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.LabelField("Nhập cây & vật trang trí từ lưới 16×16", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Công cụ đọc ảnh 'full trang tri-1.png' theo lưới 16×16, lấy điểm tiếp đất giữa gốc làm anchor, " +
                "kiểm định loại bỏ cây trên đường / vùng canh tác / NPC, và sinh GameObject vào Level_Farm.unity.",
                MessageType.Info);

            EditorGUILayout.Space(6f);
            if (manifest == null)
            {
                EditorGUILayout.HelpBox("Chưa có Manifest asset. Hãy bấm '1. Phân tích ảnh tham chiếu' để tạo.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Manifest hiện tại:", AssetDatabase.GetAssetPath(manifest));
                EditorGUILayout.LabelField("Thời gian phân tích:", string.IsNullOrEmpty(manifest.LastAnalysisTimestamp) ? "Chưa có" : manifest.LastAnalysisTimestamp);
                EditorGUILayout.LabelField("Tổng số prop:", manifest.Entries.Count.ToString());
                EditorGUILayout.LabelField("Hợp lệ (Valid):", manifest.ValidCount.ToString(), EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Bị loại (Skipped):", manifest.SkippedCount.ToString());
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Quy trình thực hiện", EditorStyles.boldLabel);

            if (GUILayout.Button("1. Phân tích ảnh tham chiếu & Sinh Manifest (Analyze)", GUILayout.Height(30)))
            {
                AnalyzeReference();
            }

            EditorGUI.BeginChangeCheck();
            previewAnchors = EditorGUILayout.ToggleLeft("Bật Preview Anchor trên SceneView", previewAnchors);
            if (EditorGUI.EndChangeCheck())
            {
                SceneView.RepaintAll();
            }

            using (new EditorGUI.DisabledScope(manifest == null || manifest.ValidCount == 0))
            {
                if (GUILayout.Button("2. Trồng cây vào Level_Farm.unity (Apply)", GUILayout.Height(30)))
                {
                    ApplyToScene();
                }
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Thực hiện toàn bộ quy trình tự động (Analyze + Apply)", GUILayout.Height(32)))
            {
                ExecuteFullWorkflow();
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Xóa nhóm Map Props (Generated) trong scene", GUILayout.Height(24)))
            {
                RemoveGeneratedProps();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(statusMessage, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        private void LoadManifestAsset()
        {
            manifest = AssetDatabase.LoadAssetAtPath<MapPropPlacementManifest>(ManifestPath);
        }

        public static void ExecuteFullWorkflow()
        {
            // The preview grew by six rows. Install it with the stable cell
            // origin before reading visible/water cells or placing any props.
            SetupNewFarmMap.InstallCurrentLayout();
            MapReferencePropImporter importer = CreateInstance<MapReferencePropImporter>();
            importer.AnalyzeReference();
            importer.ApplyToScene();
            DestroyImmediate(importer);
        }

        /// <summary>
        /// Phase 1 & 2: Reads textures, matches candidate sprites, calculates 16x16 anchors,
        /// validates forbidden areas / paths / forest rules, and writes the Manifest asset.
        /// </summary>
        public void AnalyzeReference()
        {
            List<TextureImporter> importersToRestore = new List<TextureImporter>();
            try
            {
                statusMessage = "Đang đọc textures và nạp sprite...";
                MakeReadable(ReferencePath, importersToRestore);
                MakeReadable(BaseMapPath, importersToRestore);
                foreach (string path in PropTexturePaths)
                    MakeReadable(path, importersToRestore);

                Texture2D reference = AssetDatabase.LoadAssetAtPath<Texture2D>(ReferencePath);
                Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath);
                if (reference == null || baseMap == null)
                {
                    statusMessage = "Lỗi: Không tìm thấy Texture reference hoặc base map.";
                    Debug.LogError(statusMessage);
                    return;
                }

                Color32[] referencePixels = reference.GetPixels32();
                Color32[] basePixels = baseMap.GetPixels32();

                List<Sprite> propSprites = LoadAllPropSprites();
                List<RawMatch> rawMatches = new List<RawMatch>();

                foreach (Sprite sprite in propSprites)
                {
                    rawMatches.AddRange(FindMatchesForSprite(reference, referencePixels, sprite));
                }

                // Group matches with overlapping bottom anchors, keeping the largest authored prop
                List<RawMatch> uniqueMatches = rawMatches
                    .OrderByDescending(m => m.sprite.rect.width * m.sprite.rect.height)
                    .GroupBy(m => GetAnchorKey(m, reference.height))
                    .Select(g => g.First())
                    .OrderBy(m => reference.height - m.bottom)
                    .ThenBy(m => m.left)
                    .ToList();

                // Load map regions for area classification & validation
                MapRegionCollection regionCollection = AssetDatabase.LoadAssetAtPath<MapRegionCollection>(RegionCollectionPath);
                MapRegionDefinition homeOrchardRegion = regionCollection?.Regions.FirstOrDefault(region =>
                    region.RegionName.IndexOf("sau khi mở ruộng thanh long", StringComparison.OrdinalIgnoreCase) >= 0);
                Sprite[] homeOrchardBananas = LoadHomeOrchardBananas();

                // Load tilemaps for ground classification if available in Level_Farm
                HashSet<Vector3Int> waterCells = LoadWaterCellsFromLevelFarm();
                HashSet<Vector3Int> visibleMapCells = LoadVisibleCellsFromPreview();
                HashSet<Vector3Int> grassCells = LoadGrassCellsFromLevelFarm();

                List<MapPropEntry> entries = new List<MapPropEntry>();
                int idCounter = 1;

                // Keep the authored 16x16 reference layout exactly. Regions are still
                // used for validation/metadata, but detected anchors must not be thinned.
                MapRegionDefinition forestRegion = regionCollection != null
                    ? regionCollection.Find("Vùng trồng rừng trồng dầy dầy vào")
                    : null;
                List<RawMatch> forestMatches = new List<RawMatch>();
                List<RawMatch> nonForestMatches = new List<RawMatch>();

                foreach (RawMatch match in uniqueMatches)
                {
                    Vector3Int targetCell = CalculateTargetCell(match, reference.height);
                    if (forestRegion != null && forestRegion.Contains(targetCell))
                        forestMatches.Add(match);
                    else
                        nonForestMatches.Add(match);
                }

                // Preserve every detected forest match with its original sprite.
                List<RawMatch> filteredForestMatches = FilterForestMatches(forestMatches, forestRegion, reference.height);

                List<RawMatch> allFinalMatches = new List<RawMatch>();
                allFinalMatches.AddRange(nonForestMatches);
                allFinalMatches.AddRange(filteredForestMatches);
                allFinalMatches.AddRange(BuildNamedForestRegionFillMatches(
                    allFinalMatches, regionCollection, visibleMapCells, reference.height));
                allFinalMatches.AddRange(BuildNamedForestGrassFillMatches(
                    allFinalMatches, regionCollection, grassCells, reference.height));
                allFinalMatches.AddRange(BuildTopHorizontalTreeFillMatches(
                    allFinalMatches, reference.height));
                allFinalMatches.AddRange(BuildSouthEastGrassFillMatches(
                    allFinalMatches, reference, referencePixels, baseMap, basePixels));
                allFinalMatches.AddRange(BuildDecorativeBananaMatches(regionCollection, reference.height));

                // Build MapPropEntries
                foreach (RawMatch match in allFinalMatches)
                {
                    Sprite sprite = match.sprite;
                    float anchorPixelX = match.left + match.Width * 0.5f;
                    float referenceBottomFromTop = reference.height - match.bottom;
                    float newBottomFromTop = referenceBottomFromTop - ReferenceTopCrop;

                    Vector2Int sourcePixel = new Vector2Int(Mathf.RoundToInt(anchorPixelX), Mathf.RoundToInt(referenceBottomFromTop));
                    Vector2Int sourceCell = new Vector2Int(Mathf.FloorToInt(anchorPixelX / 16f), Mathf.FloorToInt(referenceBottomFromTop / 16f));

                    Vector3 worldAnchor = new Vector3(
                        MapLeftWorld + anchorPixelX / PixelsPerUnit,
                        MapTopWorld - newBottomFromTop / PixelsPerUnit,
                        0f);

                    Vector3Int targetCell = new Vector3Int(
                        Mathf.FloorToInt(worldAnchor.x / CellWorldSize),
                        Mathf.FloorToInt(worldAnchor.y / CellWorldSize),
                        0);

                    bool isHomeOrchard = homeOrchardRegion != null && homeOrchardRegion.Contains(targetCell);
                    bool isBanana = AssetDatabase.GetAssetPath(sprite).Contains(BananaSeparatedFolder);
                    if (isHomeOrchard && IsTreeLike(sprite) && homeOrchardBananas.Length > 0 &&
                        ShouldUseBananaAt(targetCell))
                    {
                        int bananaIndex = (Mathf.Abs(targetCell.x * 397 ^ targetCell.y * 613)) % homeOrchardBananas.Length;
                        sprite = homeOrchardBananas[bananaIndex];
                        isBanana = true;
                    }

                    // Check bounds against the actual authored Tilemap. The old
                    // background PNG is six rows shorter than the expanded map.
                    int referenceTop = reference.height - (match.bottom + match.Height);
                    int newTop = referenceTop - ReferenceTopCrop;
                    int newBottom = newTop + match.Height;

                    PropPlacementStatus status = PropPlacementStatus.Valid;
                    string matchedRegionName = isHomeOrchard ? homeOrchardRegion.RegionName : string.Empty;

                    if (!visibleMapCells.Contains(targetCell))
                    {
                        status = PropPlacementStatus.SkippedOutsideMap;
                    }
                    else if (waterCells.Contains(targetCell))
                    {
                        status = PropPlacementStatus.SkippedInWater;
                    }
                    else if (targetCell.y <= 38 && !isHomeOrchard && IsVegetation(sprite) && !IsAlwaysPhysicalProp(sprite) &&
                             IsPathAtGround(baseMap, basePixels, match, reference.height))
                    {
                        status = PropPlacementStatus.SkippedOnPath;
                    }
                    else
                    {
                        // Check forbidden regions from MapRegionCollection
                        if (regionCollection != null && !isHomeOrchard)
                        {
                            foreach (MapRegionDefinition region in regionCollection.Regions)
                            {
                                if (region.Contains(targetCell))
                                {
                                    matchedRegionName = region.RegionName;
                                    string lowerName = region.RegionName.ToLowerInvariant();

                                    // Cucumber, Dragonfruit, or normal farm plot must stay clear
                                    if (lowerName.Contains("dưa chuột") && lowerName.Contains("vùng trồng"))
                                        status = PropPlacementStatus.SkippedInCropArea;
                                    else if (lowerName.Contains("thanh long") && (lowerName.Contains("đất trồng") || lowerName.Contains("vùng trồng")))
                                        status = PropPlacementStatus.SkippedInCropArea;
                                    else if (lowerName.Contains("vùng trồng cây bình thường"))
                                        status = PropPlacementStatus.SkippedInCropArea;
                                    else if (lowerName.Contains("ông nội") || lowerName.Contains("bán hạt giống") || lowerName.Contains("cắm biển"))
                                        status = PropPlacementStatus.SkippedAtNpcOrSign;
                                    else if (lowerName.Contains("hàng rào") || lowerName.Contains("rào chắn"))
                                        status = PropPlacementStatus.SkippedAtNpcOrSign;

                                    if (status != PropPlacementStatus.Valid)
                                        break;
                                }
                            }
                        }
                    }

                    bool isGroundDetail = !isBanana && (sprite.name.StartsWith("co-") || sprite.name.EndsWith("_4") ||
                                         sprite.name.EndsWith("_5") || sprite.name.EndsWith("_6"));
                    bool addCollider = !isGroundDetail && sprite.bounds.size.x >= 0.25f && sprite.bounds.size.y >= 0.25f;
                    int sortingOrder = MapPropSorting.GetSortingOrder(sprite, worldAnchor.y);

                    string id = $"prop_{idCounter:0000}_{sprite.name}";
                    idCounter++;

                    entries.Add(new MapPropEntry(
                        id,
                        sourcePixel,
                        sourceCell,
                        targetCell,
                        worldAnchor,
                        sprite,
                        isBanana ? 0.5f : match.scale,
                        false,
                        addCollider,
                        matchedRegionName,
                        sortingOrder,
                        status));
                }

                // Save or update the manifest ScriptableObject
                if (!Directory.Exists(ManifestFolder))
                    Directory.CreateDirectory(ManifestFolder);

                manifest = AssetDatabase.LoadAssetAtPath<MapPropPlacementManifest>(ManifestPath);
                if (manifest == null)
                {
                    manifest = ScriptableObject.CreateInstance<MapPropPlacementManifest>();
                    AssetDatabase.CreateAsset(manifest, ManifestPath);
                }

                manifest.SetEntries(entries);
                EditorUtility.SetDirty(manifest);
                AssetDatabase.SaveAssets();

                statusMessage = $"Phân tích hoàn tất! Tổng cộng: {manifest.Entries.Count} prop. " +
                                $"Hợp lệ: {manifest.ValidCount}, Bị bỏ qua: {manifest.SkippedCount}.";
                Debug.Log(statusMessage);
            }
            catch (Exception ex)
            {
                statusMessage = $"Lỗi khi phân tích: {ex.Message}";
                Debug.LogException(ex);
            }
            finally
            {
                foreach (TextureImporter importer in importersToRestore)
                {
                    if (importer != null)
                    {
                        importer.isReadable = false;
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        /// <summary>
        /// Phase 3: Instantiates GameObjects into Level_Farm.unity according to the Manifest.
        /// Replaces 'Map Props (Generated)' cleanly. Idempotent.
        /// </summary>
        public void ApplyToScene()
        {
            if (manifest == null)
                LoadManifestAsset();

            if (manifest == null || manifest.ValidCount == 0)
            {
                statusMessage = "Chưa có dữ liệu hợp lệ trong Manifest để áp dụng. Hãy chạy Analyze trước.";
                Debug.LogWarning(statusMessage);
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(LevelFarmPath);
            bool openedByTool = !scene.IsValid() || !scene.isLoaded;
            if (openedByTool)
                scene = EditorSceneManager.OpenScene(LevelFarmPath, OpenSceneMode.Additive);

            try
            {
                GridManager gridManager = FindInScene<GridManager>(scene);
                if (gridManager == null)
                    throw new InvalidOperationException("Không tìm thấy GridManager trong scene Level_Farm.");

                // Cleanly remove any existing generated root to prevent duplication
                Transform oldRoot = gridManager.transform.Find(GeneratedGroupName);
                if (oldRoot != null)
                {
                    // The Inspector may currently be displaying a generated SpriteRenderer.
                    // Destroying that selected object and immediately saving the scene makes
                    // Unity rebuild MaterialEditor outside an OnGUI pass, which emits the
                    // "named GUIStyle without a current skin" error. Clear only selections
                    // owned by the root that is about to be replaced.
                    if (IsSelectionInside(Selection.activeObject, oldRoot))
                        Selection.activeObject = null;
                    Undo.DestroyObjectImmediate(oldRoot.gameObject);
                }

                GameObject group = new GameObject(GeneratedGroupName);
                Undo.RegisterCreatedObjectUndo(group, "Create Map Props (Generated)");
                group.transform.SetParent(gridManager.transform, false);

                int placedCount = 0;
                Material defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

                foreach (MapPropEntry entry in manifest.Entries)
                {
                    if (entry.status != PropPlacementStatus.Valid)
                        continue;

                    Sprite sprite = entry.sprite;
                    if (sprite == null)
                        continue;

                    GameObject prop = new GameObject($"Map Prop {placedCount:000} - {sprite.name}");
                    prop.transform.SetParent(group.transform, false);

                    Bounds bounds = sprite.bounds;
                    float scale = entry.scale > 0f ? entry.scale : 1f;
                    prop.transform.localScale = new Vector3(scale, scale, 1f);

                    // Anchor is bottom center of ground contact
                    Vector3 bottomCenterOffset = new Vector3(bounds.center.x * scale, bounds.min.y * scale, 0f);
                    prop.transform.position = entry.worldAnchor - bottomCenterOffset;

                    SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
                    renderer.sprite = sprite;
                    renderer.color = Color.white;
                    if (defaultMaterial != null)
                        renderer.sharedMaterial = defaultMaterial;
                    renderer.sortingLayerName = "Dynamic";
                    renderer.sortingOrder = entry.sortingOrder;
                    renderer.flipX = entry.flipX;

                    if (entry.addCollider)
                    {
                        BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
                        collider.size = new Vector2(
                            Mathf.Max(0.12f / scale, bounds.size.x * 0.42f),
                            Mathf.Clamp(bounds.size.y * 0.16f, 0.08f / scale, 0.24f / scale));
                        collider.offset = new Vector2(bounds.center.x, bounds.min.y + collider.size.y * 0.5f);
                    }

                    MapRegionGeneratedProp tracking = prop.AddComponent<MapRegionGeneratedProp>();
                    tracking.Initialize(entry.regionName, new Vector3Int(entry.sourceCell.x, entry.sourceCell.y, 0), entry.targetCell, entry.id);

                    placedCount++;
                }

                AddForestDetails(group.transform, LoadGrassCellsFromLevelFarm());

                EditorUtility.SetDirty(group);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                // The generated root is replaced above, so restore the denser
                // tree row whenever the reference props are rebuilt.
                AddTreesAroundMapProp019Menu();

                statusMessage = $"Đã trồng thành công {placedCount} cây & vật trang trí vào Level_Farm.unity!";
                Debug.Log(statusMessage);
            }
            finally
            {
                if (openedByTool && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        public void RemoveGeneratedProps()
        {
            Scene scene = SceneManager.GetSceneByPath(LevelFarmPath);
            bool openedByTool = !scene.IsValid() || !scene.isLoaded;
            if (openedByTool)
                scene = EditorSceneManager.OpenScene(LevelFarmPath, OpenSceneMode.Additive);

            try
            {
                GridManager gridManager = FindInScene<GridManager>(scene);
                if (gridManager == null) return;

                Transform root = gridManager.transform.Find(GeneratedGroupName);
                if (root != null)
                {
                    Undo.DestroyObjectImmediate(root.gameObject);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    statusMessage = "Đã xóa nhóm Map Props (Generated) khỏi Level_Farm.";
                }
                else
                {
                    statusMessage = "Không có nhóm Map Props (Generated) nào trong scene để xóa.";
                }
            }
            finally
            {
                if (openedByTool && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private void DuringSceneGui(SceneView sceneView)
        {
            if (!previewAnchors || manifest == null)
                return;

            Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
            foreach (MapPropEntry entry in manifest.Entries)
            {
                if (entry.status == PropPlacementStatus.Valid)
                {
                    Handles.color = entry.addCollider ? new Color(0.1f, 0.9f, 0.2f, 0.8f) : new Color(0.2f, 0.6f, 1f, 0.8f);
                    Handles.DrawWireDisc(entry.worldAnchor, Vector3.forward, 0.08f);
                }
                else
                {
                    Handles.color = new Color(0.9f, 0.2f, 0.2f, 0.5f);
                    Handles.DrawLine(entry.worldAnchor - new Vector3(0.05f, 0.05f, 0), entry.worldAnchor + new Vector3(0.05f, 0.05f, 0));
                    Handles.DrawLine(entry.worldAnchor - new Vector3(-0.05f, 0.05f, 0), entry.worldAnchor + new Vector3(-0.05f, 0.05f, 0));
                }
            }
        }

        private static List<Sprite> LoadAllPropSprites()
        {
            List<Sprite> result = new List<Sprite>();
            foreach (string path in PropTexturePaths)
            {
                result.AddRange(AssetDatabase.LoadAllAssetsAtPath(path)
                    .OfType<Sprite>()
                    .Where(s => !s.name.Contains("ban-khoa") && s.rect.width > 8f && s.rect.height > 8f));
            }
            return result;
        }

        private static IEnumerable<RawMatch> FindMatchesForSprite(Texture2D reference, Color32[] referencePixels, Sprite sprite)
        {
            if (IsFlowerTree(sprite) && reference.width == 1808 && reference.height == 1328)
            {
                yield return new RawMatch(sprite, 487, 639);
                yield break;
            }

            Rect rect = sprite.rect;
            float renderScale = IsWaterJar(sprite) ? 0.5f : 1f;
            int width = Mathf.RoundToInt(rect.width * renderScale);
            int height = Mathf.RoundToInt(rect.height * renderScale);
            int spriteStartX = Mathf.RoundToInt(rect.x);
            int spriteStartY = Mathf.RoundToInt(rect.y);
            Color32[] sourcePixels = sprite.texture.GetPixels32();
            List<PixelPoint> opaque = new List<PixelPoint>();

            bool matchByTrunk = IsTreeLike(sprite);
            bool isWaterJar = IsWaterJar(sprite);
            bool isFlowerTree = IsFlowerTree(sprite);
            bool isRicePile = IsRicePile(sprite);
            bool isDecorationVegetation = IsVegetation(sprite);
            bool allowPartialMatch = isWaterJar || isFlowerTree || isRicePile;
            int colorTolerance = isFlowerTree ? 24 : isWaterJar ? 12 : 8;
            float requiredOpaqueRatio = isFlowerTree ? 0.82f : isWaterJar || isRicePile ? 0.88f : 1f;
            int matchHeight = matchByTrunk ? Mathf.Min(12, height) : height;
            int sourceStep = renderScale < 1f ? 2 : 1;

            for (int y = 0; y < matchHeight; y++)
            {
                int sourceRow = (spriteStartY + y * sourceStep) * sprite.texture.width + spriteStartX;
                for (int x = 0; x < width; x++)
                {
                    Color32 color = sourcePixels[sourceRow + x * sourceStep];
                    if (color.a >= 200)
                        opaque.Add(new PixelPoint(x, y, color));
                }
            }

            if (opaque.Count < 8)
                yield break;

            PixelPoint[] probes = SelectProbes(opaque);
            List<Vector2Int> accepted = new List<Vector2Int>();
            for (int bottom = 0; bottom <= reference.height - height; bottom++)
            {
                for (int left = 0; left <= reference.width - width; left++)
                {
                    // The south-east corner around target cell (51,-35) contains overlapping
                    // grass layers. Only that requested corner receives tolerant matching;
                    // the north-west corner deliberately keeps strict matching so it stays
                    // lighter instead of being flooded by duplicate grass props.
                    int anchorBottomFromTop = reference.height - bottom;
                    int anchorX = left + width / 2;
                    int anchorXMod = PositiveModulo(anchorX, 16);
                    int anchorYMod = PositiveModulo(anchorBottomFromTop, 16);
                    bool alignedToAuthoredGrid = Mathf.Abs(anchorXMod - 8) <= 3 &&
                                                 (anchorYMod <= 3 || anchorYMod >= 11);
                    bool denseSouthEast = isDecorationVegetation && left > 1420 &&
                                          anchorBottomFromTop > 1080 && alignedToAuthoredGrid;
                    bool allowCandidatePartial = allowPartialMatch || denseSouthEast;
                    float candidateOpaqueRatio = denseSouthEast
                        ? (matchByTrunk ? 0.45f : 0.58f)
                        : requiredOpaqueRatio;
                    int candidateTolerance = denseSouthEast ? Mathf.Max(colorTolerance, 12) : colorTolerance;

                    int matchingProbes = 0;
                    for (int i = 0; i < probes.Length; i++)
                    {
                        PixelPoint probe = probes[i];
                        if (SimilarRgb(referencePixels[(bottom + probe.y) * reference.width + left + probe.x], probe.color, candidateTolerance))
                            matchingProbes++;
                    }

                    float requiredProbeRatio = denseSouthEast ? 0.5f : 0.67f;
                    int requiredProbes = allowCandidatePartial ? Mathf.CeilToInt(probes.Length * requiredProbeRatio) : probes.Length;
                    if (matchingProbes < requiredProbes ||
                        !AllOpaquePixelsMatch(referencePixels, reference.width, left, bottom, opaque, candidateTolerance, candidateOpaqueRatio))
                        continue;

                    Vector2Int candidate = new Vector2Int(left, bottom);
                    if (accepted.Any(existing => Mathf.Abs(existing.x - candidate.x) <= 2 && Mathf.Abs(existing.y - candidate.y) <= 2))
                        continue;

                    accepted.Add(candidate);
                    yield return new RawMatch(sprite, left, bottom, renderScale);
                }
            }
        }

        private static PixelPoint[] SelectProbes(IReadOnlyList<PixelPoint> opaque)
        {
            int[] indices = { 0, opaque.Count / 5, opaque.Count * 2 / 5, opaque.Count * 3 / 5, opaque.Count * 4 / 5, opaque.Count - 1 };
            return indices.Distinct().Select(index => opaque[index]).ToArray();
        }

        private static bool AllOpaquePixelsMatch(Color32[] referencePixels, int referenceWidth,
            int left, int bottom, IReadOnlyList<PixelPoint> opaque, int tolerance, float requiredRatio)
        {
            int matching = 0;
            for (int i = 0; i < opaque.Count; i++)
            {
                PixelPoint point = opaque[i];
                if (SimilarRgb(referencePixels[(bottom + point.y) * referenceWidth + left + point.x], point.color, tolerance))
                    matching++;
                else if (requiredRatio >= 1f)
                    return false;
            }
            return matching >= Mathf.CeilToInt(opaque.Count * requiredRatio);
        }

        private static List<RawMatch> FilterForestMatches(List<RawMatch> forestMatches, MapRegionDefinition forestRegion, int referenceHeight)
        {
            // Do not procedurally thin or replace trees here. The reference image is the
            // source of truth: one detected trunk/base anchor becomes one scene object.
            return forestMatches
                .OrderBy(m => CalculateTargetCell(m, referenceHeight).x)
                .ThenBy(m => CalculateTargetCell(m, referenceHeight).y)
                .ToList();
        }

        private static List<RawMatch> BuildNamedForestRegionFillMatches(
            IReadOnlyCollection<RawMatch> existingMatches,
            MapRegionCollection regionCollection,
            HashSet<Vector3Int> visibleMapCells,
            int referenceHeight)
        {
            List<RawMatch> result = new List<RawMatch>();
            if (regionCollection == null || visibleMapCells == null || visibleMapCells.Count == 0)
                return result;

            Sprite[] treeCycle =
            {
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Sprites/props-items/trang trí map-tách riêng/cay-nho-1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Sprites/props-items/trang trí map-tách riêng/cay-vua-1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-2.png")
            };
            treeCycle = treeCycle.Where(sprite => sprite != null).ToArray();
            if (treeCycle.Length == 0)
                return result;

            List<MapRegionDefinition> sparseRegions = regionCollection.Regions
                .Where(IsSparseForestRegion)
                .ToList();
            List<MapRegionDefinition> denseRegions = regionCollection.Regions
                .Where(IsDenseForestRegion)
                // The old dense region and the new sparse region currently have
                // identical bounds. Treat the explicitly named sparse region as
                // the intended owner instead of stacking both densities.
                .Where(dense => !sparseRegions.Any(sparse => SameBounds(dense, sparse)))
                .ToList();

            HashSet<Vector3Int> occupied = new HashSet<Vector3Int>(
                existingMatches.Select(match => CalculateTargetCell(match, referenceHeight)));
            List<Vector3Int> treeCells = existingMatches
                .Where(match => IsTreeLike(match.sprite))
                .Select(match => CalculateTargetCell(match, referenceHeight))
                .Distinct()
                .ToList();

            foreach (MapRegionDefinition region in sparseRegions)
                FillForestRegion(region, 0.025f, 3, treeCycle, visibleMapCells,
                    occupied, treeCells, result, referenceHeight);

            foreach (MapRegionDefinition region in denseRegions)
                FillForestRegion(region, 0.09f, 1, treeCycle, visibleMapCells,
                    occupied, treeCells, result, referenceHeight);

            return result;
        }

        private static void FillForestRegion(
            MapRegionDefinition region,
            float targetDensity,
            int minimumSpacing,
            Sprite[] treeCycle,
            HashSet<Vector3Int> visibleMapCells,
            HashSet<Vector3Int> occupied,
            List<Vector3Int> treeCells,
            List<RawMatch> output,
            int referenceHeight)
        {
            List<Vector3Int> candidates = new List<Vector3Int>();
            for (int y = region.MinCell.y; y <= region.MaxCell.y; y++)
            for (int x = region.MinCell.x; x <= region.MaxCell.x; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (visibleMapCells.Contains(cell) && !occupied.Contains(cell))
                    candidates.Add(cell);
            }

            int visibleCellCount = candidates.Count + occupied.Count(cell => region.Contains(cell));
            int existingTreeCount = treeCells.Count(cell => region.Contains(cell));
            int targetTreeCount = Mathf.RoundToInt(visibleCellCount * targetDensity);
            int treesToAdd = Mathf.Max(0, targetTreeCount - existingTreeCount);
            if (treesToAdd == 0)
                return;

            int seed = region.ScatterSettings.RandomSeed ^ region.RegionName.GetHashCode();
            System.Random random = new System.Random(seed);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swap = random.Next(i + 1);
                (candidates[i], candidates[swap]) = (candidates[swap], candidates[i]);
            }

            int added = 0;
            foreach (Vector3Int cell in candidates)
            {
                if (added >= treesToAdd)
                    break;
                if (treeCells.Any(existing =>
                        Mathf.Abs(existing.x - cell.x) <= minimumSpacing &&
                        Mathf.Abs(existing.y - cell.y) <= minimumSpacing))
                    continue;

                Sprite sprite = treeCycle[(existingTreeCount + added) % treeCycle.Length];
                Vector3 worldAnchor = new Vector3(
                    cell.x * CellWorldSize + CellWorldSize * 0.5f,
                    cell.y * CellWorldSize,
                    0f);
                output.Add(CreateMatchFromWorld(sprite, worldAnchor, referenceHeight, 1f));
                occupied.Add(cell);
                treeCells.Add(cell);
                added++;
            }
        }

        private static bool IsSparseForestRegion(MapRegionDefinition region)
        {
            if (region == null || string.IsNullOrWhiteSpace(region.RegionName))
                return false;
            string name = region.RegionName.ToLowerInvariant();
            return name.Contains("trồng rừng") && name.Contains("thưa hơn");
        }

        private static List<RawMatch> BuildNamedForestGrassFillMatches(
            IReadOnlyCollection<RawMatch> existingMatches,
            MapRegionCollection regionCollection,
            HashSet<Vector3Int> grassCells,
            int referenceHeight)
        {
            HashSet<Vector3Int> occupied = new HashSet<Vector3Int>(
                existingMatches.Select(match => CalculateTargetCell(match, referenceHeight)));
            return ScatterForestGrass(regionCollection, grassCells, occupied, referenceHeight);
        }

        private static List<RawMatch> ScatterForestGrass(
            MapRegionCollection regionCollection, HashSet<Vector3Int> grassCells,
            HashSet<Vector3Int> occupied, int referenceHeight)
        {
            List<RawMatch> result = new List<RawMatch>();
            if (regionCollection == null || grassCells == null || grassCells.Count == 0)
                return result;

            Sprite[] grasses =
            {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/co-cao.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/co-nho-1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/co-nho-2.png")
            };
            if (grasses.Any(sprite => sprite == null))
                return result;

            List<MapRegionDefinition> sparseRegions = regionCollection.Regions
                .Where(IsSparseForestRegion).ToList();
            List<MapRegionDefinition> denseRegions = regionCollection.Regions
                .Where(IsDenseForestRegion)
                .Where(dense => !sparseRegions.Any(sparse => SameBounds(dense, sparse)))
                .ToList();
            foreach (MapRegionDefinition region in sparseRegions)
                AddGrassInRegion(region, 22, grasses, grassCells, occupied, result, referenceHeight);
            foreach (MapRegionDefinition region in denseRegions)
                AddGrassInRegion(region, 42, grasses, grassCells, occupied, result, referenceHeight);

            return result;
        }

        private static void AddGrassInRegion(
            MapRegionDefinition region, int percent,
            Sprite[] grasses, HashSet<Vector3Int> grassCells,
            HashSet<Vector3Int> occupied, List<RawMatch> output, int referenceHeight)
        {
            for (int y = region.MinCell.y; y <= region.MaxCell.y; y++)
            for (int x = region.MinCell.x; x <= region.MaxCell.x; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (!grassCells.Contains(cell) || occupied.Contains(cell))
                    continue;

                // Stable per-cell noise keeps the result identical on every import.
                uint hash = unchecked((uint)(x * 73856093 ^ y * 19349663 ^ 0x47524153));
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                if (hash % 100u >= (uint)percent)
                    continue;

                Sprite grass = grasses[hash % 10u < 6u ? 0 : 1 + (int)(hash % 2u)];
                Vector3 anchor = new Vector3(
                    x * CellWorldSize + CellWorldSize * 0.5f,
                    y * CellWorldSize + 0.02f, 0f);
                RawMatch match = CreateMatchFromWorld(grass, anchor, referenceHeight, 1f);
                output.Add(match);
                occupied.Add(cell);
                occupied.Add(CalculateTargetCell(match, referenceHeight));
            }
        }

        private static bool IsDenseForestRegion(MapRegionDefinition region)
        {
            if (region == null || string.IsNullOrWhiteSpace(region.RegionName))
                return false;
            string name = region.RegionName.ToLowerInvariant();
            return name.Contains("trồng rừng") && !name.Contains("thưa hơn") &&
                   (name.Contains("dầy dầy") || name.Contains("dày dày"));
        }

        private static bool SameBounds(MapRegionDefinition left, MapRegionDefinition right)
        {
            return left != null && right != null &&
                   left.MinCell == right.MinCell && left.MaxCell == right.MaxCell;
        }

        private static List<RawMatch> BuildTopHorizontalTreeFillMatches(
            IReadOnlyCollection<RawMatch> existingMatches, int referenceHeight)
        {
            // Map Prop 010 (cay-lon-2) sits at the eastern end of the sparse
            // top horizontal tree line. Fill the large gaps on that same row,
            // while retaining the trees detected from the 16x16 reference.
            Sprite[] trees =
            {
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-2.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/Sprites/props-items/trang trí map-tách riêng/cay-vua-1.png")
            };
            trees = trees.Where(sprite => sprite != null).ToArray();
            if (trees.Length == 0)
                return new List<RawMatch>();

            HashSet<Vector3Int> occupied = new HashSet<Vector3Int>(
                existingMatches.Select(match => CalculateTargetCell(match, referenceHeight)));
            int[] xCells = { -50, -46, -39, -29, -18, -9, -5, 1 };
            const int yCell = 38;
            List<RawMatch> result = new List<RawMatch>();

            for (int i = 0; i < xCells.Length; i++)
            {
                Vector3Int cell = new Vector3Int(xCells[i], yCell, 0);
                if (occupied.Contains(cell))
                    continue;

                Sprite tree = trees[i % trees.Length];
                Vector3 worldAnchor = new Vector3(
                    cell.x * CellWorldSize + CellWorldSize * 0.5f,
                    cell.y * CellWorldSize,
                    0f);
                result.Add(CreateMatchFromWorld(tree, worldAnchor, referenceHeight, 1f));
                occupied.Add(cell);
            }

            return result;
        }

        private static Sprite[] LoadHomeOrchardBananas()
        {
            // Stages 5 and 6 are static wild banana decorations. They never grow or yield;
            // inside the unlocked home orchard they can be removed with three axe hits.
            return new[]
                {
                    AssetDatabase.LoadAssetAtPath<Sprite>(BananaSeparatedFolder + "/bananatree_200_5.png"),
                    AssetDatabase.LoadAssetAtPath<Sprite>(BananaSeparatedFolder + "/bananatree_200_6.png")
                }
                .Where(sprite => sprite != null)
                .ToArray();
        }

        private static List<RawMatch> BuildSouthEastGrassFillMatches(
            IReadOnlyCollection<RawMatch> existingMatches,
            Texture2D reference,
            Color32[] referencePixels,
            Texture2D baseMap,
            Color32[] basePixels)
        {
            Sprite[] grassSprites =
            {
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/co-cao.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/co-nho-1.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/co-nho-2.png")
            };
            grassSprites = grassSprites.Where(sprite => sprite != null).ToArray();
            if (grassSprites.Length == 0)
                return new List<RawMatch>();

            HashSet<Vector3Int> occupied = new HashSet<Vector3Int>(
                existingMatches.Select(match => CalculateTargetCell(match, reference.height)));
            List<RawMatch> result = new List<RawMatch>();

            // Work cell by cell around Map Prop 631 / target (51,-35). The reference
            // has overlapping grass layers here that full-sprite matching misses.
            // Every missing cell is accepted only when the corresponding 16x16 cell
            // in full trang tri-1.png contains vegetation.
            for (int y = -40; y <= -24; y++)
            for (int x = 40; x <= 55; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (occupied.Contains(cell))
                    continue;

                Vector3 worldAnchor = new Vector3(x * CellWorldSize + CellWorldSize * 0.5f,
                    y * CellWorldSize + 0.02f, 0f);
                if (!IsGrassGroundAtWorld(baseMap, basePixels, worldAnchor))
                    continue;

                bool visibleInReference = HasReferenceVegetationAtWorld(
                    reference, referencePixels, baseMap, basePixels, worldAnchor);
                if (!visibleInReference)
                    continue;

                uint hash = unchecked((uint)(x * 73856093 ^ y * 19349663 ^ 0x4D454144));

                // co-cao is the main missing layer; shorter grass breaks up repetition.
                int spriteIndex = hash % 10u < 7u ? 0 : 1 + (int)(hash % 2u);
                Sprite sprite = grassSprites[Mathf.Clamp(spriteIndex, 0, grassSprites.Length - 1)];
                result.Add(CreateMatchFromWorld(sprite, worldAnchor, reference.height, 1f));
                occupied.Add(cell);
            }

            return result;
        }

        private static bool HasReferenceVegetationAtWorld(Texture2D reference, Color32[] referencePixels,
            Texture2D baseMap, Color32[] basePixels, Vector3 worldAnchor)
        {
            int pixelX = Mathf.RoundToInt((worldAnchor.x - MapLeftWorld) * PixelsPerUnit);
            int groundFromTop = Mathf.RoundToInt((MapTopWorld - worldAnchor.y) * PixelsPerUnit);
            int baseY = baseMap.height - 1 - (groundFromTop - LegacyBaseMapTopPadding);
            int referenceY = reference.height - 1 - groundFromTop - ReferenceTopCrop;
            int foliagePixels = 0;

            // Inspect the 16x16 cell directly above the ground-contact point. A dark
            // green difference from the undecorated map indicates authored vegetation.
            for (int dy = 0; dy < 16; dy++)
            for (int dx = -8; dx < 8; dx++)
            {
                int x = pixelX + dx;
                int by = baseY + dy;
                int ry = referenceY + dy;
                if (x < 0 || x >= baseMap.width || x >= reference.width ||
                    by < 0 || by >= baseMap.height || ry < 0 || ry >= reference.height)
                    continue;

                Color32 plain = basePixels[by * baseMap.width + x];
                Color32 decorated = referencePixels[ry * reference.width + x];
                int difference = Mathf.Abs(plain.r - decorated.r) +
                                 Mathf.Abs(plain.g - decorated.g) +
                                 Mathf.Abs(plain.b - decorated.b);
                if (difference >= 36 && decorated.g >= decorated.r + 12 &&
                    decorated.g >= decorated.b + 18)
                    foliagePixels++;
            }

            return foliagePixels >= 8;
        }

        private static bool IsGrassGroundAtWorld(Texture2D baseMap, Color32[] pixels, Vector3 worldAnchor)
        {
            int pixelX = Mathf.RoundToInt((worldAnchor.x - MapLeftWorld) * PixelsPerUnit);
            int groundFromTop = Mathf.RoundToInt((MapTopWorld - worldAnchor.y) * PixelsPerUnit);
            int pixelY = baseMap.height - 1 - (groundFromTop - LegacyBaseMapTopPadding);
            int grassPixels = 0;
            int checkedPixels = 0;

            for (int oy = -3; oy <= 3; oy++)
            for (int ox = -3; ox <= 3; ox++)
            {
                int x = pixelX + ox;
                int y = pixelY + oy;
                if (x < 0 || x >= baseMap.width || y < 0 || y >= baseMap.height)
                    continue;
                Color32 color = pixels[y * baseMap.width + x];
                checkedPixels++;
                if (color.a > 200 && color.g > 90 && color.g >= color.r + 20 && color.g >= color.b + 30)
                    grassPixels++;
            }

            return checkedPixels > 0 && grassPixels >= Mathf.CeilToInt(checkedPixels * 0.7f);
        }

        private static bool ShouldUseBananaAt(Vector3Int cell)
        {
            // Replace roughly one third of authored tree anchors, deterministically.
            // This keeps the reference layout/spacing while mixing wild bananas with trees.
            uint hash = unchecked((uint)(cell.x * 397 ^ cell.y * 613));
            return hash % 3u == 0u;
        }

        private static List<RawMatch> BuildDecorativeBananaMatches(MapRegionCollection regionCollection, int referenceHeight)
        {
            List<RawMatch> result = new List<RawMatch>();
            if (regionCollection == null)
                return result;

            Sprite[] bananaSprites = LoadHomeOrchardBananas();

            if (bananaSprites.Length == 0)
                return result;

            int bananaIdx = 0;
            foreach (MapRegionDefinition region in regionCollection.Regions)
            {
                string name = region.RegionName.ToLowerInvariant();
                if (!name.Contains("chuối") || !name.Contains("trồng"))
                    continue;

                // For single cell banana regions
                if (region.CellCount == 1)
                {
                    Vector3Int cell = region.MinCell;
                    Vector3 worldPos = new Vector3(cell.x * CellWorldSize + 0.08f, cell.y * CellWorldSize, 0f);
                    result.Add(CreateBananaMatchFromWorld(bananaSprites[bananaIdx % bananaSprites.Length], worldPos, referenceHeight));
                    bananaIdx++;
                }
                else
                {
                    // Multi-cell banana region (e.g. 4 or 5 trees)
                    int targetCount = 4;
                    if (name.Contains("5")) targetCount = 5;
                    else if (name.Contains("4")) targetCount = 4;

                    System.Random rng = new System.Random(region.MinCell.x * 100 + region.MinCell.y);
                    int placed = 0;
                    int attempts = 0;
                    HashSet<Vector3Int> usedCells = new HashSet<Vector3Int>();

                    while (placed < targetCount && attempts < 50)
                    {
                        attempts++;
                        int rx = rng.Next(region.MinCell.x, region.MaxCell.x + 1);
                        int ry = rng.Next(region.MinCell.y, region.MaxCell.y + 1);
                        Vector3Int cell = new Vector3Int(rx, ry, 0);
                        if (usedCells.Contains(cell))
                            continue;

                        usedCells.Add(cell);
                        Vector3 worldPos = new Vector3(cell.x * CellWorldSize + 0.08f, cell.y * CellWorldSize, 0f);
                        result.Add(CreateBananaMatchFromWorld(bananaSprites[bananaIdx % bananaSprites.Length], worldPos, referenceHeight));
                        bananaIdx++;
                        placed++;
                    }
                }
            }

            return result;
        }

        private static RawMatch CreateBananaMatchFromWorld(Sprite sprite, Vector3 worldAnchor, int referenceHeight)
        {
            return CreateMatchFromWorld(sprite, worldAnchor, referenceHeight, 0.5f);
        }

        private static RawMatch CreateMatchFromWorld(Sprite sprite, Vector3 worldAnchor, int referenceHeight, float scale)
        {
            float anchorPixelX = (worldAnchor.x - MapLeftWorld) * PixelsPerUnit;
            float newBottomFromTop = (MapTopWorld - worldAnchor.y) * PixelsPerUnit;
            float referenceBottomFromTop = newBottomFromTop + ReferenceTopCrop;

            int width = Mathf.RoundToInt(sprite.rect.width * scale);
            int left = Mathf.RoundToInt(anchorPixelX - width * 0.5f);
            int bottom = Mathf.RoundToInt(referenceHeight - referenceBottomFromTop);

            return new RawMatch(sprite, left, bottom, scale);
        }

        private static Vector3Int CalculateTargetCell(RawMatch match, int referenceHeight)
        {
            float anchorPixelX = match.left + match.Width * 0.5f;
            float referenceBottomFromTop = referenceHeight - match.bottom;
            float newBottomFromTop = referenceBottomFromTop - ReferenceTopCrop;

            Vector3 worldAnchor = new Vector3(
                MapLeftWorld + anchorPixelX / PixelsPerUnit,
                MapTopWorld - newBottomFromTop / PixelsPerUnit,
                0f);

            return new Vector3Int(
                Mathf.FloorToInt(worldAnchor.x / CellWorldSize),
                Mathf.FloorToInt(worldAnchor.y / CellWorldSize),
                0);
        }

        private static HashSet<Vector3Int> LoadWaterCellsFromLevelFarm()
        {
            HashSet<Vector3Int> waterCells = new HashSet<Vector3Int>();
            Scene scene = SceneManager.GetSceneByPath(LevelFarmPath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(LevelFarmPath, OpenSceneMode.Additive);

            try
            {
                GridManager manager = FindInScene<GridManager>(scene);
                if (manager != null)
                {
                    SerializedObject serialized = new SerializedObject(manager);
                    Tilemap waterTileMap = serialized.FindProperty("waterTileMap")?.objectReferenceValue as Tilemap;
                    if (waterTileMap != null)
                    {
                        BoundsInt bounds = waterTileMap.cellBounds;
                        foreach (Vector3Int pos in bounds.allPositionsWithin)
                        {
                            if (waterTileMap.HasTile(pos))
                                waterCells.Add(pos);
                        }
                    }
                }
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
            return waterCells;
        }

        private static HashSet<Vector3Int> LoadGrassCellsFromLevelFarm()
        {
            HashSet<Vector3Int> grassCells = new HashSet<Vector3Int>();
            Scene scene = SceneManager.GetSceneByPath(LevelFarmPath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(LevelFarmPath, OpenSceneMode.Additive);

            try
            {
                GridManager manager = FindInScene<GridManager>(scene);
                if (manager == null)
                    return grassCells;

                Tilemap[] tilemaps = manager.GetComponentsInChildren<Tilemap>(true);
                Tilemap grass = tilemaps.FirstOrDefault(tilemap => tilemap.name == "Grass");
                Tilemap layout = tilemaps.FirstOrDefault(tilemap => tilemap.name == "Map Layout Base");

                if (grass != null)
                {
                    foreach (Vector3Int cell in grass.cellBounds.allPositionsWithin)
                    {
                        if (grass.HasTile(cell))
                            grassCells.Add(cell);
                    }
                }

                // Most of the expanded forest uses the authored Map Layout Base,
                // not the small legacy Grass tilemap. Its tiles are individual
                // 16x16 slices, so classify their pixels instead of comparing one
                // particular Tile asset.
                if (layout != null)
                {
                    Dictionary<Texture2D, Color32[]> pixelCache = new Dictionary<Texture2D, Color32[]>();
                    foreach (Vector3Int cell in layout.cellBounds.allPositionsWithin)
                    {
                        Sprite sprite = layout.GetSprite(cell);
                        if (IsMostlyGrassTile(sprite, pixelCache))
                            grassCells.Add(cell);
                    }
                }
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            return grassCells;
        }

        private static bool IsMostlyGrassTile(Sprite sprite, IDictionary<Texture2D, Color32[]> pixelCache)
        {
            if (sprite == null || sprite.texture == null)
                return false;

            Texture2D texture = sprite.texture;
            if (!pixelCache.TryGetValue(texture, out Color32[] pixels))
            {
                try
                {
                    pixels = texture.GetPixels32();
                    pixelCache[texture] = pixels;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            Rect rect = sprite.rect;
            int minX = Mathf.RoundToInt(rect.xMin) + 2;
            int maxX = Mathf.RoundToInt(rect.xMax) - 2;
            int minY = Mathf.RoundToInt(rect.yMin) + 2;
            int maxY = Mathf.RoundToInt(rect.yMax) - 2;
            int grassPixels = 0;
            int checkedPixels = 0;

            for (int y = minY; y < maxY; y += 2)
            for (int x = minX; x < maxX; x += 2)
            {
                if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                    continue;
                Color32 color = pixels[y * texture.width + x];
                checkedPixels++;
                if (color.a > 200 && color.g > 90 &&
                    color.g >= color.r + 18 && color.g >= color.b + 28)
                    grassPixels++;
            }

            return checkedPixels > 0 && grassPixels >= Mathf.CeilToInt(checkedPixels * 0.72f);
        }

        private static HashSet<Vector3Int> LoadVisibleCellsFromPreview()
        {
            HashSet<Vector3Int> cells = new HashSet<Vector3Int>();
            GameObject preview = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewMapPath);
            Tilemap tilemap = preview != null ? preview.GetComponentInChildren<Tilemap>(true) : null;
            if (tilemap == null)
                return cells;

            Vector3Int offset = new Vector3Int(PreviewCellOffsetX, PreviewCellOffsetY, 0);
            foreach (Vector3Int sourceCell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.HasTile(sourceCell))
                    cells.Add(sourceCell + offset);
            }

            return cells;
        }

        private static bool IsPathAtGround(Texture2D baseMap, Color32[] pixels, RawMatch match, int referenceHeight)
        {
            int x = Mathf.RoundToInt(match.left + match.Width * 0.5f);
            int groundFromTop = Mathf.RoundToInt(
                referenceHeight - match.bottom - ReferenceTopCrop - LegacyBaseMapTopPadding);
            int y = baseMap.height - 1 - groundFromTop;
            int pathPixels = 0;
            int checkedPixels = 0;

            for (int oy = -3; oy <= 3; oy++)
            for (int ox = -5; ox <= 5; ox++)
            {
                int sampleX = x + ox;
                int sampleY = y + oy;
                if (sampleX < 0 || sampleX >= baseMap.width || sampleY < 0 || sampleY >= baseMap.height)
                    continue;

                checkedPixels++;
                if (IsPathColor(pixels[sampleY * baseMap.width + sampleX]))
                    pathPixels++;
            }

            return checkedPixels > 0 && pathPixels >= Mathf.CeilToInt(checkedPixels * 0.55f);
        }

        private static bool IsPathColor(Color32 color)
        {
            return color.a > 200 && color.r >= 175 && color.r <= 205 &&
                   color.g >= 155 && color.g <= 190 && color.b >= 115 && color.b <= 160;
        }

        private static bool IsVegetation(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            return path.Contains("/trang trí map-tách riêng/") ||
                   path.Contains(BananaSeparatedFolder) ||
                   path.EndsWith("/trang trí map.png", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith("/cay hoa gạo-Sheet.png", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith("/cây-chết.png", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTreeLike(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            return (path.Contains("/trang trí map-tách riêng/") && sprite.rect.height >= 50f) ||
                   path.EndsWith("/cay hoa gạo-Sheet.png", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWaterJar(Sprite sprite)
        {
            return AssetDatabase.GetAssetPath(sprite).EndsWith("/lu nước.png", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFlowerTree(Sprite sprite)
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            return path.IndexOf("/cay hoa ", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   path.EndsWith("-Sheet.png", StringComparison.OrdinalIgnoreCase) &&
                   sprite.rect.width > 100f;
        }

        private static bool IsRicePile(Sprite sprite)
        {
            return AssetDatabase.GetAssetPath(sprite).IndexOf("/ụ lúa-tách riêng/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsAlwaysPhysicalProp(Sprite sprite)
        {
            return IsWaterJar(sprite) || IsFlowerTree(sprite);
        }

        private static string GetAnchorKey(RawMatch match, int referenceHeight)
        {
            int centreX = Mathf.RoundToInt(match.left + match.Width * 0.5f);
            int bottomFromTop = referenceHeight - match.bottom;
            return centreX + ":" + bottomFromTop;
        }

        private static bool SimilarRgb(Color32 left, Color32 right, int tolerance)
        {
            return Mathf.Abs(left.r - right.r) <= tolerance &&
                   Mathf.Abs(left.g - right.g) <= tolerance &&
                   Mathf.Abs(left.b - right.b) <= tolerance;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static void MakeReadable(string path, ICollection<TextureImporter> restore)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                restore.Add(importer);
            }
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

        private static bool IsSelectionInside(UnityEngine.Object selectedObject, Transform root)
        {
            if (selectedObject == null || root == null)
                return false;
            Transform selectedTransform = selectedObject switch
            {
                GameObject gameObject => gameObject.transform,
                Component component => component.transform,
                _ => null
            };
            return selectedTransform != null &&
                   (selectedTransform == root || selectedTransform.IsChildOf(root));
        }
    }
}
#endif
