#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using World.Objects;

/// <summary>
/// Plays every crop through all its growth stages in its own soil cell,
/// using the same per-stage anchors as the runtime Crop, without Play Mode.
/// </summary>
public class CropGrowthPreviewWindow : EditorWindow
{
    private enum ViewMode
    {
        Animate,
        AllStages
    }

    private const int PatchColumns = 5;
    private const int PatchRows = 5;
    // Planted cell row from the top of a patch; leaves room above for tall stages.
    private const int PatchCellRow = 3;
    private const float LabelHeight = 18f;
    private const float Spacing = 8f;
    private const float EndHoldStages = 2f;

    private readonly List<CropDefinition> definitions = new List<CropDefinition>();
    private ViewMode viewMode = ViewMode.Animate;
    private bool playing = true;
    private bool showGuides = true;
    private float secondsPerStage = 0.8f;
    private float tilePixels = 24f;
    private float progress;
    private double lastTime;
    private Vector2 scroll;

    [MenuItem("Tools/Farming/Crop Growth Preview")]
    public static void Open()
    {
        CropGrowthPreviewWindow window = GetWindow<CropGrowthPreviewWindow>("Crop Growth Preview");
        window.minSize = new Vector2(420f, 300f);
        window.Show();
    }

    private void OnEnable()
    {
        LoadDefinitions();
        lastTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }

    private void OnDisable()
    {
        EditorApplication.update -= Tick;
    }

    private void OnProjectChange()
    {
        LoadDefinitions();
        Repaint();
    }

    private void LoadDefinitions()
    {
        definitions.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:CropDefinition"))
        {
            CropDefinition definition = AssetDatabase.LoadAssetAtPath<CropDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (definition != null && definition.GrowthSprites != null && definition.GrowthSprites.Length > 0)
                definitions.Add(definition);
        }
        definitions.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
    }

    private int MaxStageCount => definitions.Count == 0 ? 1 : definitions.Max(d => d.GrowthSprites.Length);

    /// <summary>
    /// Advances a shared 0..1 progress so every crop goes from seed to ripe
    /// together, then holds on the ripe stage briefly before looping.
    /// </summary>
    private void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float delta = (float)(now - lastTime);
        lastTime = now;
        if (!playing || viewMode != ViewMode.Animate)
            return;

        float cycle = Mathf.Max(0.05f, secondsPerStage) * MaxStageCount;
        float hold = Mathf.Max(0.05f, secondsPerStage) * EndHoldStages / cycle;
        progress += delta / cycle;
        if (progress > 1f + hold)
            progress = 0f;
        Repaint();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (definitions.Count == 0)
        {
            EditorGUILayout.HelpBox("Không tìm thấy Crop Definition nào.", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (viewMode == ViewMode.Animate)
            DrawAnimated();
        else
            DrawAllStages();
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        viewMode = (ViewMode)GUILayout.Toolbar((int)viewMode, new[] { "Chạy giai đoạn", "Tất cả giai đoạn" },
            EditorStyles.toolbarButton, GUILayout.Width(220f));
        GUILayout.FlexibleSpace();
        showGuides = GUILayout.Toggle(showGuides, "Ô / gốc", EditorStyles.toolbarButton);
        if (GUILayout.Button("Tải lại", EditorStyles.toolbarButton))
            LoadDefinitions();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        tilePixels = EditorGUILayout.Slider("Zoom (px/ô)", tilePixels, 12f, 48f);
        EditorGUILayout.EndHorizontal();

        if (viewMode != ViewMode.Animate)
            return;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(playing ? "❚❚ Dừng" : "▶ Chạy", GUILayout.Width(80f)))
            playing = !playing;
        if (GUILayout.Button("|◀", GUILayout.Width(32f)))
        {
            playing = false;
            progress = 0f;
        }
        if (GUILayout.Button("◀", GUILayout.Width(32f)))
            StepStage(-1);
        if (GUILayout.Button("▶", GUILayout.Width(32f)))
            StepStage(1);
        if (GUILayout.Button("▶|", GUILayout.Width(32f)))
        {
            playing = false;
            progress = 1f;
        }
        secondsPerStage = EditorGUILayout.Slider("Giây / giai đoạn", secondsPerStage, 0.1f, 3f);
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        float scrub = EditorGUILayout.Slider("Tiến độ", Mathf.Clamp01(progress), 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            playing = false;
            progress = scrub;
        }
    }

    private void StepStage(int direction)
    {
        playing = false;
        int max = MaxStageCount;
        int current = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(progress) * max), 0, max - 1);
        int next = Mathf.Clamp(current + direction, 0, max - 1);
        progress = max <= 1 ? 1f : (next + 0.5f) / max;
    }

    private static int StageAt(CropDefinition definition, float normalizedProgress)
    {
        int count = definition.GrowthSprites.Length;
        return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(normalizedProgress) * count), 0, count - 1);
    }

    private void DrawAnimated()
    {
        float patchWidth = PatchColumns * tilePixels;
        float patchHeight = PatchRows * tilePixels;
        int perRow = Mathf.Max(1, Mathf.FloorToInt((position.width - 20f) / (patchWidth + Spacing)));

        for (int start = 0; start < definitions.Count; start += perRow)
        {
            Rect row = GUILayoutUtility.GetRect(position.width - 20f, patchHeight + LabelHeight + Spacing);
            for (int i = start; i < Mathf.Min(start + perRow, definitions.Count); i++)
            {
                CropDefinition definition = definitions[i];
                int stage = StageAt(definition, progress);
                Rect patch = new Rect(row.x + (i - start) * (patchWidth + Spacing), row.y + LabelHeight, patchWidth, patchHeight);
                DrawCropLabel(new Rect(patch.x, row.y, patchWidth, LabelHeight), definition,
                    $"{definition.DisplayName}  {stage + 1}/{definition.GrowthSprites.Length}");
                DrawStage(patch, definition, stage);
            }
        }
    }

    private void DrawAllStages()
    {
        float patchWidth = PatchColumns * tilePixels;
        float patchHeight = PatchRows * tilePixels;

        foreach (CropDefinition definition in definitions)
        {
            int count = definition.GrowthSprites.Length;
            Rect row = GUILayoutUtility.GetRect(count * (patchWidth + Spacing), patchHeight + LabelHeight + Spacing,
                GUILayout.Width(count * (patchWidth + Spacing)));
            DrawCropLabel(new Rect(row.x, row.y, 300f, LabelHeight), definition, definition.DisplayName);
            for (int stage = 0; stage < count; stage++)
            {
                Rect patch = new Rect(row.x + stage * (patchWidth + Spacing), row.y + LabelHeight, patchWidth, patchHeight);
                DrawStage(patch, definition, stage);
                GUI.Label(new Rect(patch.x + 2f, patch.y + 1f, 40f, 16f), (stage + 1).ToString(), EditorStyles.whiteMiniLabel);
            }
        }
    }

    private void DrawStage(Rect patch, CropDefinition definition, int stage)
    {
        Sprite sprite = definition.GrowthSprites[stage];
        Vector2 rendererPosition = definition.GetStageLocalPosition(
            sprite, CropSpriteAlignment.CropVisualScale, CropStagePreviewDrawer.CellSize);
        CropStageAnchor mode = definition.GetStageAnchorMode(stage);
        Vector2? anchorPixel = CropSpriteAlignment.UsesAnchorPixel(mode) ? definition.GetStageAnchorPixel(stage) : (Vector2?)null;
        CropStagePreviewDrawer.Draw(patch, tilePixels, PatchCellRow, sprite, rendererPosition,
            definition.GroundInset, anchorPixel, showGuides);
    }

    /// <summary>Crop name; clicking it selects the Crop Definition for editing.</summary>
    private static void DrawCropLabel(Rect rect, CropDefinition definition, string text)
    {
        if (GUI.Button(rect, text, EditorStyles.boldLabel))
        {
            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
        }
    }
}
#endif
