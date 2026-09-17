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
        public const string LevelFarmPath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
        public const string ManifestFolder = "Assets/Settings/Map Decorations";
        public const string ManifestPath = ManifestFolder + "/Map Prop Placement Manifest.asset";
        public const string RegionCollectionPath = "Assets/Settings/Map Regions/Map Region Collection.asset";
        public const string BananaTreePath = "Assets/Sprites/cây trồng/cây lâu năm/banana/bananatree_200.png";
        public const string GeneratedGroupName = "Map Props (Generated)";

        public const float PixelsPerUnit = 100f;
        public const float MapLeftWorld = -8.96f;
        public const float MapTopWorld = 6.24f;
        public const int ReferenceTopCrop = 96; // 6 cells (96 pixels)
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
        private const int TargetLayoutVersion = 1;

        [InitializeOnLoadMethod]
        private static void AutoRunOnCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    EditorPrefs.GetInt(VersionKey, 0) < TargetLayoutVersion)
                {
                    ExecuteFullWorkflow();
                    EditorPrefs.SetInt(VersionKey, TargetLayoutVersion);
                }
            };
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
                MakeReadable(BananaTreePath, importersToRestore);

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

                // Load tilemaps for ground classification if available in Level_Farm
                HashSet<Vector3Int> waterCells = LoadWaterCellsFromLevelFarm();

                List<MapPropEntry> entries = new List<MapPropEntry>();
                int idCounter = 1;

                // Forest region subsampling context
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

                // Filter forest matches according to the rule: 2 small -> 2 large, ~25% density, evenly spread
                List<RawMatch> filteredForestMatches = FilterForestMatches(forestMatches, forestRegion, reference.height);

                List<RawMatch> allFinalMatches = new List<RawMatch>();
                allFinalMatches.AddRange(nonForestMatches);
                allFinalMatches.AddRange(filteredForestMatches);

                // Add authored decorative bananas from banana regions
                List<RawMatch> decorativeBananaMatches = BuildDecorativeBananaMatches(regionCollection, reference.height);
                allFinalMatches.AddRange(decorativeBananaMatches);

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

                    // Check bounds against cropped map
                    int referenceTop = reference.height - (match.bottom + match.Height);
                    int newTop = referenceTop - ReferenceTopCrop;
                    int newBottom = newTop + match.Height;

                    PropPlacementStatus status = PropPlacementStatus.Valid;
                    string matchedRegionName = string.Empty;

                    if (newBottom <= 0 || newTop >= baseMap.height)
                    {
                        status = PropPlacementStatus.SkippedOutsideMap;
                    }
                    else if (waterCells.Contains(targetCell))
                    {
                        status = PropPlacementStatus.SkippedInWater;
                    }
                    else if (IsVegetation(sprite) && !IsAlwaysPhysicalProp(sprite) &&
                             IsPathAtGround(baseMap, basePixels, match, reference.height))
                    {
                        status = PropPlacementStatus.SkippedOnPath;
                    }
                    else
                    {
                        // Check forbidden regions from MapRegionCollection
                        if (regionCollection != null)
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

                    bool isGroundDetail = sprite.name.StartsWith("co-") || sprite.name.EndsWith("_4") ||
                                         sprite.name.EndsWith("_5") || sprite.name.EndsWith("_6");
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
                        match.scale,
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
                    Undo.DestroyObjectImmediate(oldRoot.gameObject);

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

                EditorUtility.SetDirty(group);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

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
                    int matchingProbes = 0;
                    for (int i = 0; i < probes.Length; i++)
                    {
                        PixelPoint probe = probes[i];
                        if (SimilarRgb(referencePixels[(bottom + probe.y) * reference.width + left + probe.x], probe.color, colorTolerance))
                            matchingProbes++;
                    }

                    int requiredProbes = allowPartialMatch ? Mathf.CeilToInt(probes.Length * 0.67f) : probes.Length;
                    if (matchingProbes < requiredProbes ||
                        !AllOpaquePixelsMatch(referencePixels, reference.width, left, bottom, opaque, colorTolerance, requiredOpaqueRatio))
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
            if (forestMatches.Count == 0 || forestRegion == null)
                return forestMatches;

            // Load small and large tree sprites for the alternating pattern: 2 small -> 2 large
            Sprite smallTree1 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/cay-nho-1.png");
            Sprite smallTree2 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/cay-vua-1.png");
            Sprite largeTree1 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-1.png");
            Sprite largeTree2 = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-2.png");

            Sprite[] smallTrees = new[] { smallTree1, smallTree2 }.Where(s => s != null).ToArray();
            Sprite[] largeTrees = new[] { largeTree1, largeTree2 }.Where(s => s != null).ToArray();

            // Sort forest matches evenly across the region
            List<RawMatch> sorted = forestMatches
                .OrderBy(m => CalculateTargetCell(m, referenceHeight).x)
                .ThenBy(m => CalculateTargetCell(m, referenceHeight).y)
                .ToList();

            List<RawMatch> result = new List<RawMatch>();
            System.Random rng = new System.Random(12345);

            // Subsample by 25% (skip ~3 out of every 4)
            int stepCounter = 0;
            for (int i = 0; i < sorted.Count; i++)
            {
                // Subsampling filter: ~25%
                if (i % 4 != 0)
                    continue;

                RawMatch original = sorted[i];

                // Shape sequence: 2 small, 2 large, repeat
                int patternIdx = (stepCounter % 4);
                stepCounter++;

                Sprite replacementSprite;
                if (patternIdx < 2 && smallTrees.Length > 0)
                    replacementSprite = smallTrees[patternIdx % smallTrees.Length];
                else if (largeTrees.Length > 0)
                    replacementSprite = largeTrees[(patternIdx - 2) % largeTrees.Length];
                else
                    replacementSprite = original.sprite;

                result.Add(new RawMatch(replacementSprite, original.left, original.bottom, original.scale));
            }

            return result;
        }

        private static List<RawMatch> BuildDecorativeBananaMatches(MapRegionCollection regionCollection, int referenceHeight)
        {
            List<RawMatch> result = new List<RawMatch>();
            if (regionCollection == null)
                return result;

            Sprite[] bananaSprites = AssetDatabase.LoadAllAssetsAtPath(BananaTreePath)
                .OfType<Sprite>()
                .Where(s => s.name == "bananatree_200_5" || s.name == "bananatree_200_6")
                .ToArray();

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
            float anchorPixelX = (worldAnchor.x - MapLeftWorld) * PixelsPerUnit;
            float newBottomFromTop = (MapTopWorld - worldAnchor.y) * PixelsPerUnit;
            float referenceBottomFromTop = newBottomFromTop + ReferenceTopCrop;
            float scale = 0.5f;

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

        private static bool IsPathAtGround(Texture2D baseMap, Color32[] pixels, RawMatch match, int referenceHeight)
        {
            int x = Mathf.RoundToInt(match.left + match.Width * 0.5f);
            int groundFromTop = Mathf.RoundToInt(referenceHeight - match.bottom - ReferenceTopCrop);
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
    }
}
#endif
