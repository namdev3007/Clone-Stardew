#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using World;

public sealed class MapCellAndPropTool : EditorWindow
{
    private const string PropRoot = "Assets/Sprites/props-items";
    private const string PropGroupName = "Map Props";
    private const string RegionAssetFolder = "Assets/Settings/Map Regions";
    private const string RegionAssetPath = RegionAssetFolder + "/Map Region Collection.asset";
    private const string BananaTreePath = "Assets/Sprites/cây trồng/cây lâu năm/banana/bananatree_200.png";
    private const string DecorationPropsPath = "Assets/Sprites/props-items/trang trí map-tách riêng";
    private const string GeneratedRegionRootName = "Map Region Generated Props";
    private const string RegionOverlayModePreference = "Meadom.MapCellTool.RegionOverlayMode";

    private static readonly Color FarmableColor = new Color(0.2f, 1f, 0.25f, 0.22f);
    private static readonly Color WaterColor = new Color(0.2f, 0.65f, 1f, 0.28f);
    private static readonly Color HoedColor = new Color(1f, 0.65f, 0.1f, 0.3f);
    private static readonly Color WetColor = new Color(0.25f, 0.85f, 1f, 0.32f);
    private static readonly Color BlockedColor = new Color(1f, 0.2f, 0.2f, 0.18f);
    private static readonly Color RegionFillColor = new Color(0.75f, 0.25f, 1f, 0.18f);
    private static readonly Color RegionOutlineColor = new Color(0.95f, 0.65f, 1f, 1f);
    private static readonly Color RegionGridColor = new Color(0.95f, 0.65f, 1f, 0.35f);

    private static bool cursorEnabled;
    private static bool propBrushEnabled;
    private static bool eraseEnabled;
    private static bool regionSelectionEnabled;
    private static bool hasRegionStart;
    private static bool hasRegion;
    private static Vector3Int regionStart;
    private static Vector3Int regionEnd;
    private static Vector3Int hoveredCell;
    private static Vector3 hoveredWorld;
    private static bool hasHover;
    private static double nextWindowRepaintTime;

    private Grid grid;
    private Tilemap visibleMap;
    private Tilemap farmable;
    private Tilemap water;
    private Tilemap hoed;
    private Tilemap wet;
    private readonly List<Sprite> propSprites = new List<Sprite>();
    private string[] propNames = Array.Empty<string>();
    private int selectedProp;
    private bool addCollider = true;
    private Vector2 propOffset;
    private float propScale = 1f;
    private MapRegionCollection regionCollection;
    private string regionName = "New Region";
    private string[] savedRegionNames = Array.Empty<string>();
    private int selectedSavedRegion;
    private int regionOverlayMode = 2;
    private string loadedScatterRegionName;
    private MapRegionScatterSource scatterSource = MapRegionScatterSource.BananaGrowthStages;
    private float scatterDensity = 0.15f;
    private int scatterSeed = 12345;
    private int scatterMinimumSpacing = 1;
    private int scatterMinimumBananaStage;
    private int scatterMaximumBananaStage = 7;
    private Vector2 scatterScaleRange = Vector2.one;
    private bool scatterAddCollider = true;
    private bool scatterRequireVisibleTile = true;
    private bool scatterSkipWater = true;
    private bool scatterAutoSaveScene = true;
    private string status = "Bật con trỏ để kiểm tra ô trên Scene.";

    [MenuItem("Tools/Map/Cell Debug & Prop Tool")]
    public static void Open()
    {
        GetWindow<MapCellAndPropTool>("Map Cell & Props");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGui;
        EditorApplication.hierarchyChanged += RefreshSceneReferences;
        RefreshSceneReferences();
        LoadProps();
        LoadRegionCollection(false);
        LoadScatterSettingsFromSelected();
        regionOverlayMode = Mathf.Clamp(EditorPrefs.GetInt(RegionOverlayModePreference, 2), 0, 2);
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGui;
        EditorApplication.hierarchyChanged -= RefreshSceneReferences;
        cursorEnabled = false;
        propBrushEnabled = false;
        eraseEnabled = false;
        regionSelectionEnabled = false;
        SceneView.RepaintAll();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Map Cell Debug", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Rê chuột trong Scene để xem ô. Chuột trái đặt prop khi bật Prop Brush; chuột phải xóa prop trong ô. Mọi thao tác đều Undo được.",
            MessageType.Info);

        bool newCursor = EditorGUILayout.ToggleLeft("Bật con trỏ debug ô", cursorEnabled);
        if (newCursor != cursorEnabled)
        {
            cursorEnabled = newCursor;
            if (!cursorEnabled)
            {
                propBrushEnabled = false;
                eraseEnabled = false;
                regionSelectionEnabled = false;
            }
            SceneView.RepaintAll();
        }

        using (new EditorGUI.DisabledScope(!cursorEnabled || !hasHover))
        {
            EditorGUILayout.LabelField("Ô đang trỏ", hasHover ? FormatCell(hoveredCell) : "—");
            EditorGUILayout.LabelField("World", hasHover ? FormatWorld(hoveredWorld) : "—");
            EditorGUILayout.LabelField("Trạng thái", hasHover ? GetCellState(hoveredCell) : "—");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy tọa độ ô"))
                CopyToClipboard($"{hoveredCell.x},{hoveredCell.y}");
            if (GUILayout.Button("Copy world position"))
                CopyToClipboard($"{hoveredWorld.x:0.###},{hoveredWorld.y:0.###},0");
            EditorGUILayout.EndHorizontal();
        }

        DrawRegionSelectionControls();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Prop Brush (GameObject)", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(!cursorEnabled || propSprites.Count == 0))
        {
            bool newPropBrushEnabled = EditorGUILayout.ToggleLeft("Đặt vật trang trí bằng chuột trái", propBrushEnabled);
            if (newPropBrushEnabled != propBrushEnabled)
            {
                propBrushEnabled = newPropBrushEnabled;
                if (propBrushEnabled)
                    regionSelectionEnabled = false;
            }
            eraseEnabled = EditorGUILayout.ToggleLeft("Chế độ xóa bằng chuột trái", eraseEnabled);
            selectedProp = EditorGUILayout.Popup("Sprite", Mathf.Clamp(selectedProp, 0, Mathf.Max(0, propNames.Length - 1)), propNames);
            propOffset = EditorGUILayout.Vector2Field("Lệch trong ô", propOffset);
            propScale = Mathf.Max(0.05f, EditorGUILayout.FloatField("Tỉ lệ", propScale));
            addCollider = EditorGUILayout.Toggle("Thêm collider", addCollider);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Nạp lại props"))
            LoadProps();
        if (GUILayout.Button("Chọn nhóm Map Props"))
            Selection.activeGameObject = FindOrCreatePropRoot(false)?.gameObject;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5f);
        EditorGUILayout.HelpBox(status, MessageType.None);
    }

    private void DrawRegionSelectionControls()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Đánh dấu vùng bằng 2 điểm", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Bật chế độ này rồi click chuột trái vào 2 ô trong Scene. Tool sẽ ghim vùng hình chữ nhật, tô overlay và cho copy tọa độ. Click lần thứ 3 sẽ bắt đầu một vùng mới.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(!cursorEnabled))
        {
            bool newRegionSelectionEnabled = EditorGUILayout.ToggleLeft("Chọn vùng bằng 2 click", regionSelectionEnabled);
            if (newRegionSelectionEnabled != regionSelectionEnabled)
            {
                regionSelectionEnabled = newRegionSelectionEnabled;
                if (regionSelectionEnabled)
                {
                    propBrushEnabled = false;
                    eraseEnabled = false;
                    status = hasRegion
                        ? "Vùng đang được ghim. Click một ô để bắt đầu chọn vùng mới."
                        : "Click ô thứ nhất trong Scene.";
                }
                SceneView.RepaintAll();
            }
        }

        if (hasRegion)
        {
            GetRegionBounds(out Vector3Int min, out Vector3Int max);
            int width = max.x - min.x + 1;
            int height = max.y - min.y + 1;
            long cellCount = (long)width * height;

            EditorGUILayout.LabelField("Góc nhỏ nhất", FormatCell(min));
            EditorGUILayout.LabelField("Góc lớn nhất", FormatCell(max));
            EditorGUILayout.LabelField("Kích thước", $"{width} x {height} = {cellCount} ô");
            regionName = EditorGUILayout.TextField("Tên vùng", regionName);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(regionName)))
            {
                if (GUILayout.Button("Lưu / cập nhật vùng đã đặt tên"))
                    SaveNamedRegion(min, max);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy 2 góc"))
                CopyToClipboard(BuildRegionBoundsText(min, max, width, height, cellCount));
            using (new EditorGUI.DisabledScope(cellCount > 50000))
            {
                if (GUILayout.Button("Copy toàn bộ ô"))
                    CopyToClipboard(BuildRegionCellList(min, max));
            }
            EditorGUILayout.EndHorizontal();

            if (cellCount > 50000)
                EditorGUILayout.HelpBox("Vùng có hơn 50.000 ô. Hãy copy 2 góc thay vì toàn bộ danh sách.", MessageType.Warning);

            if (GUILayout.Button("Xóa vùng đã ghim"))
                ClearRegionSelection();
        }
        else if (hasRegionStart)
        {
            EditorGUILayout.LabelField("Điểm 1", FormatCell(regionStart));
            EditorGUILayout.LabelField("Điểm 2", "Chưa chọn — click thêm một ô trong Scene");
            if (GUILayout.Button("Hủy điểm 1"))
                ClearRegionSelection();
        }
        else
        {
            EditorGUILayout.LabelField("Vùng đã ghim", "Chưa có");
        }

        DrawSavedRegionsControls();
    }

    private void DrawSavedRegionsControls()
    {
        EditorGUILayout.Space(5f);
        EditorGUILayout.LabelField("Các vùng đã lưu", EditorStyles.boldLabel);

        if (regionCollection == null)
            LoadRegionCollection(false);
        RefreshSavedRegionNames();

        if (savedRegionNames.Length == 0)
        {
            EditorGUILayout.LabelField("Chưa có vùng nào được lưu.");
            EditorGUILayout.LabelField("File", RegionAssetPath, EditorStyles.miniLabel);
            return;
        }

        int previousSelection = selectedSavedRegion;
        selectedSavedRegion = EditorGUILayout.Popup(
            "Vùng",
            Mathf.Clamp(selectedSavedRegion, 0, savedRegionNames.Length - 1),
            savedRegionNames);
        if (selectedSavedRegion != previousSelection)
        {
            LoadScatterSettingsFromSelected();
            SceneView.RepaintAll();
        }

        int previousOverlayMode = regionOverlayMode;
        regionOverlayMode = GUILayout.Toolbar(regionOverlayMode,
            new[] { "Tắt overlay", "Vùng đang chọn", "Tất cả vùng" });
        if (regionOverlayMode != previousOverlayMode)
        {
            EditorPrefs.SetInt(RegionOverlayModePreference, regionOverlayMode);
            SceneView.RepaintAll();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Nạp và ghim vùng"))
            LoadSelectedNamedRegion();
        if (GUILayout.Button("Camera tới vùng"))
            FrameSelectedNamedRegion();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy vùng"))
            CopySelectedNamedRegion();
        if (GUILayout.Button("Xóa vùng"))
            DeleteSelectedNamedRegion();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("File", RegionAssetPath, EditorStyles.miniLabel);
        DrawRegionScatterControls();
    }

    private void DrawRegionScatterControls()
    {
        MapRegionDefinition selectedRegion = GetSelectedNamedRegion();
        if (selectedRegion == null)
            return;

        if (!string.Equals(loadedScatterRegionName, selectedRegion.RegionName, StringComparison.OrdinalIgnoreCase))
            LoadScatterSettingsFromSelected();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Rải cây / vật trang trí theo vùng", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Rải lại dùng seed cố định nên cho kết quả lặp lại. Các GameObject được lưu trong scene; cấu hình mật độ được lưu trong Map Region Collection.",
            MessageType.Info);

        scatterSource = (MapRegionScatterSource)EditorGUILayout.EnumPopup("Nguồn sprite", scatterSource);
        scatterDensity = EditorGUILayout.Slider("Mật độ", scatterDensity, 0f, 1f);
        EditorGUILayout.LabelField("Tỉ lệ dự kiến", $"{Mathf.RoundToInt(scatterDensity * 100f)}% số ô hợp lệ");
        scatterSeed = Mathf.Max(0, EditorGUILayout.IntField("Random seed", scatterSeed));
        scatterMinimumSpacing = Mathf.Max(0, EditorGUILayout.IntField("Cách nhau tối thiểu (ô)", scatterMinimumSpacing));

        bool usesBanana = scatterSource != MapRegionScatterSource.DecorationProps;
        using (new EditorGUI.DisabledScope(!usesBanana))
        {
            scatterMinimumBananaStage = EditorGUILayout.IntSlider("Giai đoạn Banana nhỏ nhất", scatterMinimumBananaStage, 0, 7);
            scatterMaximumBananaStage = EditorGUILayout.IntSlider("Giai đoạn Banana lớn nhất", scatterMaximumBananaStage, 0, 7);
            if (scatterMaximumBananaStage < scatterMinimumBananaStage)
                scatterMaximumBananaStage = scatterMinimumBananaStage;
        }

        scatterScaleRange = EditorGUILayout.Vector2Field("Khoảng scale ngẫu nhiên", scatterScaleRange);
        scatterScaleRange.x = Mathf.Max(0.05f, scatterScaleRange.x);
        scatterScaleRange.y = Mathf.Max(scatterScaleRange.x, scatterScaleRange.y);
        scatterAddCollider = EditorGUILayout.Toggle("Thêm collider", scatterAddCollider);
        scatterRequireVisibleTile = EditorGUILayout.Toggle("Chỉ đặt trên tile đang có", scatterRequireVisibleTile);
        scatterSkipWater = EditorGUILayout.Toggle("Bỏ qua ô nước", scatterSkipWater);
        scatterAutoSaveScene = EditorGUILayout.Toggle("Tự lưu scene sau khi rải", scatterAutoSaveScene);

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            EditorGUILayout.HelpBox("Hãy thoát Play Mode trước khi rải. Thay đổi trong Play Mode sẽ không được lưu.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Lưu cấu hình vùng"))
                SaveScatterSettings(selectedRegion);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rải lại cây / props trong vùng"))
                ScatterSelectedRegion(selectedRegion);
            if (GUILayout.Button("Xóa cây / props đã rải"))
                ClearGeneratedRegionProps(selectedRegion, true);
            EditorGUILayout.EndHorizontal();
        }
    }

    private void SaveNamedRegion(Vector3Int min, Vector3Int max)
    {
        string cleanName = regionName.Trim();
        if (string.IsNullOrEmpty(cleanName))
            return;

        MapRegionCollection collection = LoadRegionCollection(true);
        Undo.RecordObject(collection, "Save named map region");
        collection.AddOrUpdate(cleanName, SceneManager.GetActiveScene().name, min, max);
        EditorUtility.SetDirty(collection);
        AssetDatabase.SaveAssets();
        RefreshSavedRegionNames();
        selectedSavedRegion = Array.FindIndex(savedRegionNames,
            name => string.Equals(name, cleanName, StringComparison.OrdinalIgnoreCase));
        selectedSavedRegion = Mathf.Max(0, selectedSavedRegion);
        status = $"Đã lưu vùng '{cleanName}' vào {RegionAssetPath}.";
        Repaint();
    }

    private MapRegionCollection LoadRegionCollection(bool createIfMissing)
    {
        regionCollection = AssetDatabase.LoadAssetAtPath<MapRegionCollection>(RegionAssetPath);
        if (regionCollection != null || !createIfMissing)
        {
            RefreshSavedRegionNames();
            return regionCollection;
        }

        EnsureAssetFolder("Assets/Settings", "Map Regions");
        regionCollection = CreateInstance<MapRegionCollection>();
        AssetDatabase.CreateAsset(regionCollection, RegionAssetPath);
        AssetDatabase.SaveAssets();
        RefreshSavedRegionNames();
        return regionCollection;
    }

    private static void EnsureAssetFolder(string parent, string child)
    {
        string fullPath = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parent, child);
    }

    private void RefreshSavedRegionNames()
    {
        savedRegionNames = regionCollection == null
            ? Array.Empty<string>()
            : regionCollection.Regions.Where(region => region != null).Select(region => region.RegionName).ToArray();
        selectedSavedRegion = savedRegionNames.Length == 0
            ? 0
            : Mathf.Clamp(selectedSavedRegion, 0, savedRegionNames.Length - 1);
    }

    private MapRegionDefinition GetSelectedNamedRegion()
    {
        if (regionCollection == null || savedRegionNames.Length == 0)
            return null;

        return regionCollection.Find(savedRegionNames[Mathf.Clamp(selectedSavedRegion, 0, savedRegionNames.Length - 1)]);
    }

    private void LoadSelectedNamedRegion()
    {
        MapRegionDefinition saved = GetSelectedNamedRegion();
        if (saved == null)
            return;

        regionStart = saved.MinCell;
        regionEnd = saved.MaxCell;
        hasRegionStart = true;
        hasRegion = true;
        regionName = saved.RegionName;
        cursorEnabled = true;
        status = $"Đã nạp và ghim vùng '{saved.RegionName}' ({saved.SceneName}).";
        Repaint();
        SceneView.RepaintAll();
    }

    private void CopySelectedNamedRegion()
    {
        MapRegionDefinition saved = GetSelectedNamedRegion();
        if (saved == null)
            return;

        CopyToClipboard(BuildNamedRegionText(saved));
    }

    private void FrameSelectedNamedRegion()
    {
        MapRegionDefinition saved = GetSelectedNamedRegion();
        if (saved == null)
            return;

        if (grid == null)
            RefreshSceneReferences();
        if (grid == null || SceneView.lastActiveSceneView == null)
            return;

        Vector3 minCenter = grid.GetCellCenterWorld(saved.MinCell);
        Vector3 maxCenter = grid.GetCellCenterWorld(saved.MaxCell);
        Vector3 size = new Vector3(
            Mathf.Abs(maxCenter.x - minCenter.x) + grid.cellSize.x,
            Mathf.Abs(maxCenter.y - minCenter.y) + grid.cellSize.y,
            1f);
        SceneView.lastActiveSceneView.Frame(new Bounds((minCenter + maxCenter) * 0.5f, size), false);
        SceneView.RepaintAll();
    }

    private void DeleteSelectedNamedRegion()
    {
        MapRegionDefinition saved = GetSelectedNamedRegion();
        if (saved == null)
            return;

        string deletedName = saved.RegionName;
        Undo.RecordObject(regionCollection, "Delete named map region");
        regionCollection.Remove(deletedName);
        EditorUtility.SetDirty(regionCollection);
        AssetDatabase.SaveAssets();
        RefreshSavedRegionNames();
        status = $"Đã xóa vùng '{deletedName}' khỏi file lưu.";
        Repaint();
    }

    private static string BuildNamedRegionText(MapRegionDefinition region)
    {
        return $"REGION_NAME = {region.RegionName}\n" +
               $"SCENE = {region.SceneName}\n" +
               BuildRegionBoundsText(region.MinCell, region.MaxCell, region.Width, region.Height, region.CellCount);
    }

    private void LoadScatterSettingsFromSelected()
    {
        MapRegionDefinition region = GetSelectedNamedRegion();
        if (region == null)
        {
            loadedScatterRegionName = null;
            return;
        }

        MapRegionScatterSettings settings = region.ScatterSettings;
        loadedScatterRegionName = region.RegionName;
        scatterSource = settings.Source;
        scatterDensity = settings.Density;
        scatterSeed = settings.RandomSeed;
        scatterMinimumSpacing = settings.MinimumCellSpacing;
        scatterMinimumBananaStage = Mathf.Clamp(settings.MinimumBananaStage, 0, 7);
        scatterMaximumBananaStage = Mathf.Clamp(settings.MaximumBananaStage, scatterMinimumBananaStage, 7);
        scatterScaleRange = settings.RandomScaleRange;
        scatterAddCollider = settings.AddCollider;
        scatterRequireVisibleTile = settings.RequireVisibleMapTile;
        scatterSkipWater = settings.SkipWaterCells;
        scatterAutoSaveScene = settings.AutoSaveScene;
    }

    private void SaveScatterSettings(MapRegionDefinition region)
    {
        if (regionCollection == null || region == null)
            return;

        Undo.RecordObject(regionCollection, "Save map region scatter settings");
        region.ScatterSettings.Update(
            scatterSource,
            scatterDensity,
            scatterSeed,
            scatterMinimumSpacing,
            scatterMinimumBananaStage,
            scatterMaximumBananaStage,
            scatterScaleRange,
            scatterAddCollider,
            scatterRequireVisibleTile,
            scatterSkipWater,
            scatterAutoSaveScene);
        EditorUtility.SetDirty(regionCollection);
        AssetDatabase.SaveAssets();
        loadedScatterRegionName = region.RegionName;
        status = $"Đã lưu cấu hình rải cây của vùng '{region.RegionName}'.";
        Repaint();
    }

    private void ScatterSelectedRegion(MapRegionDefinition region)
    {
        if (grid == null)
            RefreshSceneReferences();
        if (grid == null || region == null)
        {
            status = "Không tìm thấy Grid hoặc vùng đã lưu.";
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!string.IsNullOrEmpty(region.SceneName) &&
            !string.Equals(region.SceneName, activeScene.name, StringComparison.Ordinal))
        {
            status = $"Vùng '{region.RegionName}' thuộc scene '{region.SceneName}', nhưng scene hiện tại là '{activeScene.name}'.";
            return;
        }

        SaveScatterSettings(region);
        List<Sprite> bananaSprites = LoadBananaStageSprites(region.ScatterSettings);
        List<Sprite> decorationSprites = LoadDecorationSprites();
        List<Sprite> sourceSprites = new List<Sprite>();
        switch (region.ScatterSettings.Source)
        {
            case MapRegionScatterSource.BananaGrowthStages:
                sourceSprites.AddRange(bananaSprites);
                break;
            case MapRegionScatterSource.DecorationProps:
                sourceSprites.AddRange(decorationSprites);
                break;
            case MapRegionScatterSource.BananaAndDecoration:
                sourceSprites.AddRange(bananaSprites);
                sourceSprites.AddRange(decorationSprites);
                break;
        }

        if (sourceSprites.Count == 0)
        {
            status = "Không tìm thấy sprite phù hợp để rải.";
            return;
        }

        List<Vector3Int> candidates = BuildScatterCandidates(region, region.ScatterSettings);
        System.Random random = new System.Random(region.ScatterSettings.RandomSeed);
        Shuffle(candidates, random);
        int targetCount = Mathf.Clamp(
            Mathf.RoundToInt(candidates.Count * region.ScatterSettings.Density),
            0,
            candidates.Count);
        List<Vector3Int> chosenCells = new List<Vector3Int>(targetCount);
        foreach (Vector3Int candidate in candidates)
        {
            if (chosenCells.Count >= targetCount)
                break;
            if (!HasMinimumSpacing(candidate, chosenCells, region.ScatterSettings.MinimumCellSpacing))
                continue;
            chosenCells.Add(candidate);
        }

        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName($"Scatter props in {region.RegionName}");
        ClearGeneratedRegionProps(region, false);
        Transform regionRoot = CreateGeneratedRegionRoot(region.RegionName);

        foreach (Vector3Int cell in chosenCells)
        {
            Sprite sprite = sourceSprites[random.Next(sourceSprites.Count)];
            float scale = Mathf.Lerp(
                region.ScatterSettings.RandomScaleRange.x,
                region.ScatterSettings.RandomScaleRange.y,
                (float)random.NextDouble());
            CreateGeneratedProp(region, regionRoot, cell, sprite, scale, region.ScatterSettings.AddCollider);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManagerCompat.MarkActiveSceneDirty();
        SaveActiveSceneIfRequested(region.ScatterSettings.AutoSaveScene);
        status = $"Đã rải {chosenCells.Count}/{targetCount} cây/prop trong vùng '{region.RegionName}' từ {candidates.Count} ô hợp lệ.";
        Repaint();
        SceneView.RepaintAll();
    }

    private List<Vector3Int> BuildScatterCandidates(MapRegionDefinition region, MapRegionScatterSettings settings)
    {
        List<Vector3Int> candidates = new List<Vector3Int>(Mathf.Max(0, region.CellCount));
        for (int y = region.MinCell.y; y <= region.MaxCell.y; y++)
        {
            for (int x = region.MinCell.x; x <= region.MaxCell.x; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (settings.SkipWaterCells && water != null && water.HasTile(cell))
                    continue;
                if (settings.RequireVisibleMapTile && !HasVisibleTileAt(cell))
                    continue;
                candidates.Add(cell);
            }
        }
        return candidates;
    }

    private bool HasVisibleTileAt(Vector3Int cell)
    {
        if (visibleMap != null && visibleMap.HasTile(cell))
            return true;

        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        foreach (Tilemap tilemap in tilemaps)
        {
            if (tilemap == null || tilemap == farmable || tilemap == water || tilemap == hoed || tilemap == wet)
                continue;
            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer != null && renderer.enabled && tilemap.gameObject.activeInHierarchy && tilemap.HasTile(cell))
                return true;
        }
        return false;
    }

    private static bool HasMinimumSpacing(Vector3Int candidate, List<Vector3Int> chosen, int minimumSpacing)
    {
        if (minimumSpacing <= 0)
            return true;

        foreach (Vector3Int existing in chosen)
        {
            if (Mathf.Abs(candidate.x - existing.x) <= minimumSpacing &&
                Mathf.Abs(candidate.y - existing.y) <= minimumSpacing)
                return false;
        }
        return true;
    }

    private static void Shuffle<T>(IList<T> values, System.Random random)
    {
        for (int index = values.Count - 1; index > 0; index--)
        {
            int swapIndex = random.Next(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }

    private static List<Sprite> LoadBananaStageSprites(MapRegionScatterSettings settings)
    {
        List<Sprite> allStages = AssetDatabase.LoadAllAssetsAtPath(BananaTreePath)
            .OfType<Sprite>()
            .OrderBy(sprite => ExtractTrailingNumber(sprite.name))
            .ToList();
        if (allStages.Count == 0)
            return allStages;

        int min = Mathf.Clamp(settings.MinimumBananaStage, 0, allStages.Count - 1);
        int max = Mathf.Clamp(settings.MaximumBananaStage, min, allStages.Count - 1);
        return allStages.GetRange(min, max - min + 1);
    }

    private static List<Sprite> LoadDecorationSprites()
    {
        List<Sprite> sprites = new List<Sprite>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { DecorationPropsPath }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            sprites.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>());
        }
        sprites.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        return sprites;
    }

    private static int ExtractTrailingNumber(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;
        int separator = value.LastIndexOf('_');
        return separator >= 0 && int.TryParse(value.Substring(separator + 1), out int number) ? number : 0;
    }

    private Transform CreateGeneratedRegionRoot(string regionNameValue)
    {
        GameObject master = GameObject.Find(GeneratedRegionRootName);
        if (master == null)
        {
            master = new GameObject(GeneratedRegionRootName);
            Undo.RegisterCreatedObjectUndo(master, "Create generated map region root");
        }

        GameObject child = new GameObject($"Region - {regionNameValue}");
        Undo.RegisterCreatedObjectUndo(child, "Create generated region group");
        child.transform.SetParent(master.transform, false);
        return child.transform;
    }

    private void CreateGeneratedProp(
        MapRegionDefinition region,
        Transform parent,
        Vector3Int cell,
        Sprite sprite,
        float scale,
        bool withCollider)
    {
        Vector3 anchor = grid.GetCellCenterWorld(cell);
        Bounds bounds = sprite.bounds;
        Vector3 bottomCenterOffset = new Vector3(bounds.center.x, bounds.min.y, 0f);

        GameObject prop = new GameObject($"[{cell.x},{cell.y}] {sprite.name}");
        Undo.RegisterCreatedObjectUndo(prop, "Create generated region prop");
        prop.transform.SetParent(parent, true);
        prop.transform.position = anchor - bottomCenterOffset * scale;
        prop.transform.localScale = Vector3.one * scale;

        SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        renderer.sortingLayerName = "Dynamic";
        renderer.sortingOrder = MapPropSorting.GetSortingOrder(sprite, anchor.y);

        MapRegionGeneratedProp marker = prop.AddComponent<MapRegionGeneratedProp>();
        marker.Initialize(region.RegionName, cell);

        if (withCollider)
        {
            BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(
                Mathf.Max(0.06f, bounds.size.x * 0.32f),
                Mathf.Max(0.05f, bounds.size.y * 0.16f));
            collider.offset = new Vector2(bounds.center.x, bounds.min.y + collider.size.y * 0.5f);
        }
    }

    private void ClearGeneratedRegionProps(MapRegionDefinition region, bool saveAfterClear)
    {
        GameObject master = GameObject.Find(GeneratedRegionRootName);
        if (master == null || region == null)
        {
            if (saveAfterClear)
                status = $"Vùng '{region?.RegionName}' chưa có cây/prop được rải.";
            return;
        }

        string expectedName = $"Region - {region.RegionName}";
        Transform target = null;
        foreach (Transform child in master.transform)
        {
            if (string.Equals(child.name, expectedName, StringComparison.Ordinal))
            {
                target = child;
                break;
            }
        }

        if (target == null)
        {
            if (saveAfterClear)
                status = $"Vùng '{region.RegionName}' chưa có cây/prop được rải.";
            return;
        }

        Undo.DestroyObjectImmediate(target.gameObject);
        if (master.transform.childCount == 0)
            Undo.DestroyObjectImmediate(master);
        EditorSceneManagerCompat.MarkActiveSceneDirty();
        if (saveAfterClear)
        {
            SaveActiveSceneIfRequested(region.ScatterSettings.AutoSaveScene);
            status = $"Đã xóa cây/prop được rải trong vùng '{region.RegionName}'.";
            Repaint();
            SceneView.RepaintAll();
        }
    }

    private static void SaveActiveSceneIfRequested(bool autoSave)
    {
        if (!autoSave)
            return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    private void DuringSceneGui(SceneView sceneView)
    {
        if (!cursorEnabled && regionOverlayMode == 0)
            return;

        if (grid == null)
            RefreshSceneReferences();
        if (grid == null)
        {
            Handles.BeginGUI();
            GUI.Label(new Rect(12f, 12f, 300f, 24f), "Không tìm thấy GridManager/Grid trong scene.", EditorStyles.helpBox);
            Handles.EndGUI();
            return;
        }

        UnityEngine.Event current = UnityEngine.Event.current;
        bool isRepaint = current.type == EventType.Repaint;
        DrawSavedRegionOverlays(isRepaint);

        if (!cursorEnabled)
            return;

        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
        Plane plane = new Plane(Vector3.forward, new Vector3(0f, 0f, grid.transform.position.z));
        if (!plane.Raycast(ray, out float distance))
            return;

        Vector3 world = ray.GetPoint(distance);
        Vector3Int cell = grid.WorldToCell(world);
        bool hoveredDifferentCell = !hasHover || hoveredCell != cell;
        if (hoveredDifferentCell)
        {
            hoveredCell = cell;
            hoveredWorld = grid.GetCellCenterWorld(cell);
            hasHover = true;
        }

        if (isRepaint)
        {
            DrawHoveredCell(cell);
            DrawSelectedRegion(cell);
            DrawSceneLabel(cell);
        }

        // Repainting the EditorWindow and SceneView on every mouse event caused
        // a feedback loop on large maps. Refresh only when the cursor enters a
        // different grid cell, and throttle the small inspector readout.
        if (hoveredDifferentCell && EditorApplication.timeSinceStartup >= nextWindowRepaintTime)
        {
            nextWindowRepaintTime = EditorApplication.timeSinceStartup + 0.05d;
            Repaint();
        }

        if (current.alt || current.button != 0 || current.type != EventType.MouseDown)
            return;

        if (regionSelectionEnabled)
        {
            SelectRegionPoint(cell);
            current.Use();
            return;
        }

        if (!propBrushEnabled)
            return;

        if (eraseEnabled)
            ErasePropAtCell(cell);
        else
            PlaceSelectedProp(cell);

        current.Use();
    }

    private void DrawSavedRegionOverlays(bool drawGeometry)
    {
        if (regionOverlayMode == 0 || regionCollection == null || grid == null)
            return;

        MapRegionDefinition selected = GetSelectedNamedRegion();
        IEnumerable<MapRegionDefinition> regions = regionOverlayMode == 1
            ? new[] { selected }
            : regionCollection.Regions;
        string activeSceneName = SceneManager.GetActiveScene().name;
        List<MapRegionDefinition> visibleRegions = regions
            .Where(region => region != null &&
                (string.IsNullOrEmpty(region.SceneName) ||
                 string.Equals(region.SceneName, activeSceneName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (drawGeometry)
        {
            for (int i = 0; i < visibleRegions.Count; i++)
            {
                MapRegionDefinition region = visibleRegions[i];
                bool isSelected = region == selected;
                Color outline = GetRegionOutlineColor(i, isSelected);
                Color fill = new Color(outline.r, outline.g, outline.b, isSelected ? 0.20f : 0.09f);
                DrawNamedRegionOverlay(region, fill, outline);
            }
        }

        Handles.BeginGUI();
        for (int i = 0; i < visibleRegions.Count; i++)
        {
            MapRegionDefinition region = visibleRegions[i];
            bool isSelected = region == selected;
            DrawNamedRegionButton(region, GetRegionOutlineColor(i, isSelected), isSelected);
        }
        Handles.EndGUI();
    }

    private static Color GetRegionOutlineColor(int colorIndex, bool selected)
    {
        return selected
            ? new Color(1f, 0.82f, 0.1f, 1f)
            : Color.HSVToRGB((colorIndex * 0.137f) % 1f, 0.72f, 1f);
    }

    private void DrawNamedRegionOverlay(MapRegionDefinition region, Color fill, Color outline)
    {
        Vector3 minCenter = grid.GetCellCenterWorld(region.MinCell);
        Vector3 maxCenter = grid.GetCellCenterWorld(region.MaxCell);
        Vector3 cellSize = grid.cellSize;
        float left = minCenter.x - cellSize.x * 0.5f;
        float right = maxCenter.x + cellSize.x * 0.5f;
        float bottom = minCenter.y - cellSize.y * 0.5f;
        float top = maxCenter.y + cellSize.y * 0.5f;
        Vector3[] corners =
        {
            new Vector3(left, bottom, minCenter.z),
            new Vector3(left, top, minCenter.z),
            new Vector3(right, top, minCenter.z),
            new Vector3(right, bottom, minCenter.z)
        };

        Handles.DrawSolidRectangleWithOutline(corners, fill, outline);
    }

    private void DrawNamedRegionButton(MapRegionDefinition region, Color outline, bool selected)
    {
        Vector3 minCenter = grid.GetCellCenterWorld(region.MinCell);
        Vector3 maxCenter = grid.GetCellCenterWorld(region.MaxCell);
        Vector3 cellSize = grid.cellSize;
        float top = maxCenter.y + cellSize.y * 0.5f;
        Vector2 guiPoint = HandleUtility.WorldToGUIPoint(new Vector3(
            (minCenter.x + maxCenter.x) * 0.5f,
            top + cellSize.y * 0.25f,
            minCenter.z));

        string label = $"{region.RegionName}\n({region.MinCell.x},{region.MinCell.y}) → " +
                       $"({region.MaxCell.x},{region.MaxCell.y}) · {region.Width}x{region.Height}";
        GUIStyle style = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = selected ? 12 : 10,
            fontStyle = selected ? FontStyle.Bold : FontStyle.Normal,
            normal = { textColor = Color.white },
            hover = { textColor = Color.yellow }
        };
        Vector2 contentSize = style.CalcSize(new GUIContent(label));
        float width = Mathf.Clamp(contentSize.x + 18f, 150f, 420f);
        float height = Mathf.Max(38f, contentSize.y + 10f);
        Rect buttonRect = new Rect(guiPoint.x - width * 0.5f, guiPoint.y - height, width, height);

        Color previousColor = GUI.color;
        GUI.color = selected
            ? new Color(1f, 0.92f, 0.55f, 1f)
            : new Color(outline.r, outline.g, outline.b, 0.92f);
        bool clicked = GUI.Button(buttonRect, label, style);
        GUI.color = previousColor;
        if (clicked)
            SelectSavedRegionFromOverlay(region);
    }

    private void SelectSavedRegionFromOverlay(MapRegionDefinition region)
    {
        RefreshSavedRegionNames();
        selectedSavedRegion = Array.FindIndex(savedRegionNames,
            name => string.Equals(name, region.RegionName, StringComparison.OrdinalIgnoreCase));
        if (selectedSavedRegion < 0)
            return;

        LoadSelectedNamedRegion();
        LoadScatterSettingsFromSelected();
        Focus();
        status = $"Đã chọn vùng '{region.RegionName}' từ bảng trong Scene.";
        Repaint();
        SceneView.RepaintAll();
    }

    private void SelectRegionPoint(Vector3Int cell)
    {
        if (!hasRegionStart || hasRegion)
        {
            regionStart = cell;
            regionEnd = cell;
            hasRegionStart = true;
            hasRegion = false;
            status = $"Đã chọn điểm 1: {cell.x},{cell.y}. Click ô thứ hai.";
        }
        else
        {
            regionEnd = cell;
            hasRegion = true;
            GetRegionBounds(out Vector3Int min, out Vector3Int max);
            int width = max.x - min.x + 1;
            int height = max.y - min.y + 1;
            status = $"Đã ghim vùng {width} x {height} ({width * height} ô).";
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private void ClearRegionSelection()
    {
        hasRegionStart = false;
        hasRegion = false;
        status = regionSelectionEnabled ? "Đã xóa vùng. Click ô thứ nhất trong Scene." : "Đã xóa vùng đã ghim.";
        Repaint();
        SceneView.RepaintAll();
    }

    private void DrawSelectedRegion(Vector3Int currentHover)
    {
        if (!hasRegionStart)
            return;

        // "Tắt overlay" hides the pinned box too. Only the in-progress rectangle
        // stays visible while the second point of a new region is being picked.
        bool pickingSecondPoint = regionSelectionEnabled && !hasRegion;
        if (regionOverlayMode == 0 && !pickingSecondPoint)
            return;

        // A loaded saved region is also the pinned region. When its saved-region
        // overlay is visible, drawing the pinned overlay again produces two labels
        // at exactly the same position (saved name + "VÙNG ĐÃ GHIM").
        if (hasRegion && IsPinnedRegionAlreadyShown())
            return;

        Vector3Int drawEnd = hasRegion ? regionEnd : currentHover;
        GetBounds(regionStart, drawEnd, out Vector3Int min, out Vector3Int max);

        Vector3 minCenter = grid.GetCellCenterWorld(min);
        Vector3 maxCenter = grid.GetCellCenterWorld(max);
        Vector3 cellSize = grid.cellSize;
        float left = minCenter.x - cellSize.x * 0.5f;
        float right = maxCenter.x + cellSize.x * 0.5f;
        float bottom = minCenter.y - cellSize.y * 0.5f;
        float top = maxCenter.y + cellSize.y * 0.5f;
        Vector3[] corners =
        {
            new Vector3(left, bottom, minCenter.z),
            new Vector3(left, top, minCenter.z),
            new Vector3(right, top, minCenter.z),
            new Vector3(right, bottom, minCenter.z)
        };

        Handles.DrawSolidRectangleWithOutline(corners, RegionFillColor, RegionOutlineColor);

        int width = max.x - min.x + 1;
        int height = max.y - min.y + 1;
        // A dense grid is useful for small selections, but thousands of Handle
        // lines made Scene View appear frozen while choosing the second point.
        if ((long)width * height <= 400 && width + height <= 80)
        {
            Handles.color = RegionGridColor;
            for (int x = 1; x < width; x++)
            {
                float worldX = left + x * cellSize.x;
                Handles.DrawLine(new Vector3(worldX, bottom), new Vector3(worldX, top));
            }
            for (int y = 1; y < height; y++)
            {
                float worldY = bottom + y * cellSize.y;
                Handles.DrawLine(new Vector3(left, worldY), new Vector3(right, worldY));
            }
            Handles.color = Color.white;
        }

        GUIStyle style = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            normal = { textColor = Color.white }
        };
        string phase = hasRegion ? "VÙNG ĐÃ GHIM" : "CLICK ĐIỂM 2";
        Handles.Label(new Vector3((left + right) * 0.5f, top + cellSize.y * 0.45f),
            $"{phase}\nMIN ({min.x},{min.y})  MAX ({max.x},{max.y})  ·  {width}x{height} = {width * height} ô", style);
    }

    private bool IsPinnedRegionAlreadyShown()
    {
        if (regionOverlayMode == 0 || regionCollection == null)
            return false;

        GetRegionBounds(out Vector3Int pinnedMin, out Vector3Int pinnedMax);
        IEnumerable<MapRegionDefinition> visibleRegions = regionOverlayMode == 1
            ? new[] { GetSelectedNamedRegion() }
            : regionCollection.Regions;
        string activeSceneName = SceneManager.GetActiveScene().name;

        return visibleRegions.Any(region => region != null &&
            (string.IsNullOrEmpty(region.SceneName) ||
             string.Equals(region.SceneName, activeSceneName, StringComparison.OrdinalIgnoreCase)) &&
            region.MinCell == pinnedMin && region.MaxCell == pinnedMax);
    }

    private void GetRegionBounds(out Vector3Int min, out Vector3Int max)
    {
        GetBounds(regionStart, regionEnd, out min, out max);
    }

    private static void GetBounds(Vector3Int first, Vector3Int second, out Vector3Int min, out Vector3Int max)
    {
        min = new Vector3Int(Mathf.Min(first.x, second.x), Mathf.Min(first.y, second.y), 0);
        max = new Vector3Int(Mathf.Max(first.x, second.x), Mathf.Max(first.y, second.y), 0);
    }

    private static string BuildRegionBoundsText(Vector3Int min, Vector3Int max, int width, int height, long cellCount)
    {
        return $"HOME_ORCHARD_MIN_CELL = ({min.x},{min.y})\n" +
               $"HOME_ORCHARD_MAX_CELL = ({max.x},{max.y})\n" +
               $"SIZE = {width}x{height}\n" +
               $"CELL_COUNT = {cellCount}";
    }

    private static string BuildRegionCellList(Vector3Int min, Vector3Int max)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        builder.AppendLine($"MIN=({min.x},{min.y}); MAX=({max.x},{max.y})");
        builder.AppendLine("CELLS:");
        for (int y = max.y; y >= min.y; y--)
        {
            for (int x = min.x; x <= max.x; x++)
            {
                if (x > min.x)
                    builder.Append(' ');
                builder.Append('(').Append(x).Append(',').Append(y).Append(')');
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private void DrawHoveredCell(Vector3Int cell)
    {
        Vector3 center = grid.GetCellCenterWorld(cell);
        Vector3 size = grid.cellSize;
        Color color = GetCellColor(cell);
        Vector3[] corners =
        {
            center + new Vector3(-size.x * 0.5f, -size.y * 0.5f),
            center + new Vector3(-size.x * 0.5f, size.y * 0.5f),
            center + new Vector3(size.x * 0.5f, size.y * 0.5f),
            center + new Vector3(size.x * 0.5f, -size.y * 0.5f)
        };
        Handles.DrawSolidRectangleWithOutline(corners, color, new Color(color.r, color.g, color.b, 1f));
    }

    private void DrawSceneLabel(Vector3Int cell)
    {
        Vector3 labelPosition = grid.GetCellCenterWorld(cell) + Vector3.up * grid.cellSize.y * 0.75f;
        GUIStyle style = new GUIStyle(EditorStyles.helpBox)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11,
            normal = { textColor = Color.white }
        };
        Handles.Label(labelPosition, $"Cell {cell.x}, {cell.y}\n{GetCellState(cell)}", style);
    }

    private void PlaceSelectedProp(Vector3Int cell)
    {
        if (selectedProp < 0 || selectedProp >= propSprites.Count)
            return;

        Transform root = FindOrCreatePropRoot(true);
        if (root == null)
            return;

        Sprite sprite = propSprites[selectedProp];
        GameObject prop = new GameObject($"Prop {sprite.name}");
        Undo.RegisterCreatedObjectUndo(prop, "Place map prop");
        prop.transform.SetParent(root, true);
        Vector3 anchor = grid.GetCellCenterWorld(cell) + (Vector3)propOffset;
        Bounds spriteBounds = sprite.bounds;
        Vector3 bottomCenterOffset = new Vector3(spriteBounds.center.x, spriteBounds.min.y, 0f);
        prop.transform.position = anchor - bottomCenterOffset * propScale;
        prop.transform.localScale = Vector3.one * propScale;

        SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        renderer.sortingLayerName = "Dynamic";
        renderer.sortingOrder = MapPropSorting.GetSortingOrder(sprite, anchor.y);

        if (addCollider)
        {
            BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
            Bounds bounds = sprite.bounds;
            collider.size = new Vector2(Mathf.Max(0.08f, bounds.size.x * 0.45f), Mathf.Max(0.05f, bounds.size.y * 0.18f));
            collider.offset = new Vector2(bounds.center.x, bounds.min.y + collider.size.y * 0.5f);
        }

        Selection.activeGameObject = prop;
        status = $"Đã đặt {sprite.name} tại ô {cell.x}, {cell.y}.";
        EditorSceneManagerCompat.MarkActiveSceneDirty();
        Repaint();
    }

    private void ErasePropAtCell(Vector3Int cell)
    {
        Transform root = FindOrCreatePropRoot(false);
        if (root == null)
            return;

        Vector3 center = grid.GetCellCenterWorld(cell);
        Transform closest = null;
        float closestDistance = float.MaxValue;
        foreach (Transform child in root)
        {
            float distance = Vector2.Distance(child.position, center);
            if (distance < closestDistance && distance <= Mathf.Max(grid.cellSize.x, grid.cellSize.y) * 0.75f)
            {
                closest = child;
                closestDistance = distance;
            }
        }

        if (closest == null)
        {
            status = $"Ô {cell.x}, {cell.y} không có prop để xóa.";
            return;
        }

        string removedName = closest.name;
        Undo.DestroyObjectImmediate(closest.gameObject);
        status = $"Đã xóa {removedName} tại ô {cell.x}, {cell.y}.";
        EditorSceneManagerCompat.MarkActiveSceneDirty();
        Repaint();
    }

    private void RefreshSceneReferences()
    {
        GridManager manager = FindFirstObjectByType<GridManager>();
        grid = manager != null ? manager.GetComponent<Grid>() : FindFirstObjectByType<Grid>();
        if (manager != null)
        {
            visibleMap = manager.transform.Find("Map Layout Base")?.GetComponent<Tilemap>();
            SerializedObject serialized = new SerializedObject(manager);
            farmable = serialized.FindProperty("dirtTileMap")?.objectReferenceValue as Tilemap;
            water = serialized.FindProperty("waterTileMap")?.objectReferenceValue as Tilemap;
            hoed = serialized.FindProperty("dirtHoleTileMap")?.objectReferenceValue as Tilemap;
            wet = serialized.FindProperty("wateredDirtTileMap")?.objectReferenceValue as Tilemap;
        }
        Repaint();
    }

    private void LoadProps()
    {
        propSprites.Clear();
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { PropRoot });
        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            propSprites.AddRange(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>());
        }

        // Banana stages live outside props-items, but are also available in the
        // single-cell Prop Brush for hand-authored placement.
        propSprites.AddRange(AssetDatabase.LoadAllAssetsAtPath(BananaTreePath).OfType<Sprite>());

        propSprites.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        propNames = propSprites.Select(sprite => sprite.name).ToArray();
        selectedProp = Mathf.Clamp(selectedProp, 0, Mathf.Max(0, propSprites.Count - 1));
        status = $"Đã nạp {propSprites.Count} sprite từ props-items và các giai đoạn Banana.";
        Repaint();
    }

    private Transform FindOrCreatePropRoot(bool create)
    {
        GameObject existing = GameObject.Find(PropGroupName);
        if (existing != null)
            return existing.transform;
        if (!create)
            return null;

        GameObject root = new GameObject(PropGroupName);
        Undo.RegisterCreatedObjectUndo(root, "Create map props group");
        return root.transform;
    }

    private string GetCellState(Vector3Int cell)
    {
        List<string> states = new List<string>();
        if (wet != null && wet.HasTile(cell)) states.Add("đã tưới");
        if (hoed != null && hoed.HasTile(cell)) states.Add("đã cuốc");
        if (water != null && water.HasTile(cell)) states.Add("nước");
        if (farmable != null && farmable.HasTile(cell)) states.Add("trồng được");
        if (states.Count == 0 && visibleMap != null && visibleMap.HasTile(cell)) states.Add("không trồng được");
        if (states.Count == 0) states.Add("ngoài map/trống");
        return string.Join(" · ", states);
    }

    private Color GetCellColor(Vector3Int cell)
    {
        if (wet != null && wet.HasTile(cell)) return WetColor;
        if (hoed != null && hoed.HasTile(cell)) return HoedColor;
        if (water != null && water.HasTile(cell)) return WaterColor;
        if (farmable != null && farmable.HasTile(cell)) return FarmableColor;
        return BlockedColor;
    }

    private static string FormatCell(Vector3Int cell) => $"X {cell.x}   Y {cell.y}";
    private static string FormatWorld(Vector3 world) => $"X {world.x:0.###}   Y {world.y:0.###}";

    private void CopyToClipboard(string value)
    {
        EditorGUIUtility.systemCopyBuffer = value;
        status = $"Đã copy: {value}";
        Repaint();
    }

    private static class EditorSceneManagerCompat
    {
        public static void MarkActiveSceneDirty()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
