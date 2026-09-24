#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Utility;

/// <summary>
/// Keeps Level_Farm loaded additively whenever Core 1 is being edited, so the
/// authored map is visible in Scene View before entering Play Mode.
/// </summary>
[InitializeOnLoad]
public static class FarmMapEditModeLoader
{
    private const string CoreScenePath = "Assets/MainScenes/Core 1.unity";
    private const string FarmScenePath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string PreserveAuthoredFenceOrderName = "hàng rào_0 (1)";
    private static readonly string[] FarmAuthoredRootNames =
    {
        "ruộng dưa chuột",
        "Ruộng thanh long",
        "NPC house",
        "Rào ruộng thường",
        "house-Sheet_0",
        "NPC house-Sheet_0",
        "giếng_0",
        "giếng_0 (1)"
    };
    private static readonly string[] WellRootNames = { "giếng_0", "giếng_0 (1)" };
    private static bool loadQueued;

    static FarmMapEditModeLoader()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        QueueEnsureVisible();
    }

    [MenuItem("Tools/Map/Show Farm Map In Edit Mode")]
    private static void ShowFromMenu()
    {
        EnsureVisible(true);
    }

    [MenuItem("Tools/Map/Save All Open Farm Scenes %#s")]
    public static void SaveAllScenesFromMenu()
    {
        Scene coreScene = SceneManager.GetSceneByPath(CoreScenePath);
        if (coreScene.IsValid() && coreScene.isLoaded && coreScene.isDirty)
            EditorSceneManager.SaveScene(coreScene);

        Scene farmScene = SceneManager.GetSceneByPath(FarmScenePath);
        if (farmScene.IsValid() && farmScene.isLoaded)
        {
            ConfigureMapPropSorting(farmScene);
            if (farmScene.isDirty)
                EditorSceneManager.SaveScene(farmScene);
        }

        Debug.Log("[FarmMapEditModeLoader] Saved all open farm scenes.");
    }

    [MenuItem("Tools/Map/Sync Authored Farm Props And Wells")]
    private static void SyncAuthoredPropsFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before syncing authored farm props and wells.");
            return;
        }

        Scene coreScene = SceneManager.GetSceneByPath(CoreScenePath);
        Scene farmScene = SceneManager.GetSceneByPath(FarmScenePath);
        if (!coreScene.IsValid() || !coreScene.isLoaded ||
            !farmScene.IsValid() || !farmScene.isLoaded)
        {
            Debug.LogWarning("Open Core 1 and Level_Farm additively before syncing farm props and wells.");
            return;
        }

        MoveAuthoredMapObjectsToFarm(coreScene, farmScene);
        ConfigureFarmWells(farmScene);
        ConfigureFarmFenceSorting(farmScene);
        ConfigureMapPropSorting(farmScene);
        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Map/Sync All Map Props Sorting")]
    public static void SyncMapPropsFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before syncing map props.");
            return;
        }

        Scene farmScene = SceneManager.GetSceneByPath(FarmScenePath);
        bool openedHere = false;
        if (!farmScene.IsValid() || !farmScene.isLoaded)
        {
            farmScene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Additive);
            openedHere = true;
        }

        if (!farmScene.IsValid() || !farmScene.isLoaded)
        {
            Debug.LogError("Could not open Level_Farm for syncing prop sorting.");
            return;
        }

        bool changed = ConfigureMapPropSorting(farmScene);
        Debug.Log(changed ? "Finished syncing Map Prop sorting orders in Level_Farm." : "All Map Props sorting orders in Level_Farm are already up to date.");

        if (openedHere)
        {
            EditorSceneManager.CloseScene(farmScene, true);
        }
        else
        {
            SceneView.RepaintAll();
        }
    }

    [InitializeOnLoadMethod]
    private static void QueuePropSortingCheck()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            SyncMapPropsFromMenu();
        };
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == CoreScenePath)
            QueueEnsureVisible();
        else if (scene.path == FarmScenePath)
            ConfigureMapPropSorting(scene);
    }

    private static void OnActiveSceneChanged(Scene previous, Scene next)
    {
        if (next.path == CoreScenePath)
            QueueEnsureVisible();
        else if (next.path == FarmScenePath)
            ConfigureMapPropSorting(next);
    }

    private static bool isSavingScenes;

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            QueueEnsureVisible();
        else if (state == PlayModeStateChange.ExitingEditMode)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene openScene = SceneManager.GetSceneAt(i);
                if (openScene.IsValid() && openScene.isLoaded)
                {
                    if (string.Equals(openScene.path, FarmScenePath, StringComparison.OrdinalIgnoreCase))
                        ConfigureMapPropSorting(openScene);

                    if (openScene.isDirty)
                    {
                        EditorSceneManager.SaveScene(openScene);
                        Debug.Log($"[FarmMapEditModeLoader] Auto-saved modified {openScene.name} before entering Play Mode.");
                    }
                }
            }
        }
    }

    private static void OnSceneSaving(Scene scene, string path)
    {
        if (isSavingScenes)
            return;

        if (string.Equals(path, CoreScenePath, StringComparison.OrdinalIgnoreCase))
        {
            Scene farmScene = SceneManager.GetSceneByPath(FarmScenePath);
            if (farmScene.IsValid() && farmScene.isLoaded && farmScene.isDirty)
            {
                try
                {
                    isSavingScenes = true;
                    EditorSceneManager.SaveScene(farmScene);
                    Debug.Log("[FarmMapEditModeLoader] Auto-saved modified Level_Farm along with Core 1.");
                }
                finally
                {
                    isSavingScenes = false;
                }
            }
        }
        else if (string.Equals(path, FarmScenePath, StringComparison.OrdinalIgnoreCase) && scene.IsValid() && scene.isLoaded)
        {
            ConfigureMapPropSorting(scene);
        }
    }

    private static void QueueEnsureVisible()
    {
        if (loadQueued)
            return;
        loadQueued = true;
        EditorApplication.delayCall += () =>
        {
            loadQueued = false;
            EnsureVisible(false);
        };
    }

    private static void EnsureVisible(bool explicitRequest)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            return;

        Scene coreScene = SceneManager.GetSceneByPath(CoreScenePath);
        Scene activeScene = SceneManager.GetActiveScene();
        if (!explicitRequest && (!coreScene.IsValid() || !coreScene.isLoaded || activeScene.path != CoreScenePath))
            return;

        Scene farmScene = SceneManager.GetSceneByPath(FarmScenePath);
        if (!farmScene.IsValid() || !farmScene.isLoaded)
            farmScene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Additive);

        if (!farmScene.IsValid() || !farmScene.isLoaded)
        {
            Debug.LogError("Could not load Level_Farm for Edit Mode preview.");
            return;
        }

        // Keep Core 1 as the active scene so newly created UI objects are not
        // accidentally authored into Level_Farm.
        if (coreScene.IsValid() && coreScene.isLoaded)
            SceneManager.SetActiveScene(coreScene);

        foreach (GameObject root in farmScene.GetRootGameObjects())
            SceneVisibilityManager.instance.Show(root, true);

        MoveAuthoredMapObjectsToFarm(coreScene, farmScene);
        ConfigureFarmWells(farmScene);
        ConfigureFarmFenceSorting(farmScene);
        ConfigureMapPropSorting(farmScene);

        SceneView.RepaintAll();
    }

    /// <summary>
    /// These objects are map geometry authored while Core 1 was the active
    /// scene. Keep them in Level_Farm so their coordinates, visibility and
    /// lifetime follow the map instead of the persistent UI/system scene.
    /// </summary>
    private static void MoveAuthoredMapObjectsToFarm(Scene coreScene, Scene farmScene)
    {
        if (!coreScene.IsValid() || !coreScene.isLoaded ||
            !farmScene.IsValid() || !farmScene.isLoaded)
            return;

        bool movedAny = false;
        GameObject[] coreRoots = coreScene.GetRootGameObjects();

        foreach (string rootName in FarmAuthoredRootNames)
        {
            GameObject mapRoot = FindRoot(coreRoots, rootName);
            if (mapRoot == null)
                continue;

            ConfigureFenceSorting(mapRoot);
            SceneManager.MoveGameObjectToScene(mapRoot, farmScene);
            movedAny = true;
        }

        if (!movedAny)
            return;

        EditorSceneManager.MarkSceneDirty(coreScene);
        EditorSceneManager.MarkSceneDirty(farmScene);
        EditorSceneManager.SaveScene(farmScene);
        EditorSceneManager.SaveScene(coreScene);
        Debug.Log("Moved authored farm objects to Level_Farm. World positions were preserved.");
    }

    private static void ConfigureFarmWells(Scene farmScene)
    {
        GameObject[] roots = farmScene.GetRootGameObjects();
        World.GridManager gridManager = null;
        foreach (GameObject root in roots)
        {
            gridManager = root.GetComponentInChildren<World.GridManager>(true);
            if (gridManager != null)
                break;
        }

        if (gridManager == null || gridManager.Grid == null)
            return;

        bool changed = false;
        foreach (string wellName in WellRootNames)
        {
            GameObject well = FindRoot(roots, wellName);
            if (well == null)
                continue;

            SpriteRenderer sprite = well.GetComponent<SpriteRenderer>();
            if (sprite == null || sprite.sprite == null)
                continue;

            // Every cell touched by the visible well can be clicked with a
            // watering can. This keeps the refill target aligned with the art.
            Bounds bounds = sprite.bounds;
            const float inset = 0.001f;
            Vector3Int min = gridManager.Grid.WorldToCell(bounds.min + new Vector3(inset, inset, 0));
            Vector3Int max = gridManager.Grid.WorldToCell(bounds.max - new Vector3(inset, inset, 0));
            List<Vector3Int> cells = new List<Vector3Int>();
            for (int y = min.y; y <= max.y; y++)
            for (int x = min.x; x <= max.x; x++)
                cells.Add(new Vector3Int(x, y, 0));

            World.WaterRefillSource source = well.GetComponent<World.WaterRefillSource>();
            bool needsUpdate = source == null;
            if (source == null)
                source = Undo.AddComponent<World.WaterRefillSource>(well);

            if (!needsUpdate)
            {
                IReadOnlyList<Vector3Int> current = source.RefillCells;
                needsUpdate = current == null || current.Count != cells.Count;
                for (int i = 0; !needsUpdate && i < cells.Count; i++)
                    needsUpdate = current[i] != cells[i];
            }

            if (!needsUpdate)
                continue;

            source.Configure(gridManager, cells);
            EditorUtility.SetDirty(source);
            changed = true;
        }

        if (!changed)
            return;

        EditorSceneManager.MarkSceneDirty(farmScene);
        EditorSceneManager.SaveScene(farmScene);
        Debug.Log("Configured both authored wells as watering-can refill sources in Level_Farm.");
    }

    private static GameObject FindRoot(GameObject[] roots, string objectName)
    {
        foreach (GameObject root in roots)
        {
            if (root != null && string.Equals(root.name, objectName, StringComparison.Ordinal))
                return root;
        }

        return null;
    }

    private static bool ConfigureFenceSorting(GameObject mapRoot)
    {
        bool changed = false;
        SpriteRenderer[] renderers = mapRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            string objectName = renderer.gameObject.name;
            bool isFence = objectName.IndexOf("hàng rào", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           objectName.IndexOf("rào", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           objectName.IndexOf("fence", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isFence)
                continue;

            // This single authored fence is intentionally kept below trees.
            if (objectName.IndexOf("layer thấp hơn cây", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            // Upright posts and boundary fences (like hàng rào_3 (1)) use a SortingGroup
            // with HeightBasedSorting to sort dynamically by Y with the player while
            // preserving authored overlapping orders.
            bool isDynamicDepthFence = string.Equals(objectName, PreserveAuthoredFenceOrderName, StringComparison.Ordinal) ||
                                       World.AuthoredFenceDepthInstaller.IsTargetFence(renderer.gameObject);
            if (isDynamicDepthFence)
            {
                if (renderer.sortingLayerName != World.MapPropSorting.SortingLayer)
                {
                    renderer.sortingLayerName = World.MapPropSorting.SortingLayer;
                    EditorUtility.SetDirty(renderer);
                    changed = true;
                }

                SortingGroup sortingGroup = renderer.GetComponent<SortingGroup>();
                if (sortingGroup == null)
                {
                    sortingGroup = Undo.AddComponent<SortingGroup>(renderer.gameObject);
                    changed = true;
                }

                if (sortingGroup.sortingLayerName != World.MapPropSorting.SortingLayer)
                {
                    sortingGroup.sortingLayerName = World.MapPropSorting.SortingLayer;
                    EditorUtility.SetDirty(sortingGroup);
                    changed = true;
                }

                HeightBasedSorting heightSorting = renderer.GetComponent<HeightBasedSorting>();
                if (heightSorting == null)
                {
                    heightSorting = Undo.AddComponent<HeightBasedSorting>(renderer.gameObject);
                    changed = true;
                }

                heightSorting.ConfigureGroundAnchor(renderer);
                EditorUtility.SetDirty(heightSorting);
                continue;
            }

            // Use the bottom of the visible fence as its ground contact point,
            // matching the -Y depth rule used by trees and the player.
            int targetOrder = Mathf.RoundToInt(-renderer.bounds.min.y * 100f);
            if (renderer.sortingLayerName == World.MapPropSorting.SortingLayer && renderer.sortingOrder == targetOrder)
                continue;

            renderer.sortingLayerName = World.MapPropSorting.SortingLayer;
            renderer.sortingOrder = targetOrder;
            EditorUtility.SetDirty(renderer);
            changed = true;
        }

        return changed;
    }

    private static void ConfigureFarmFenceSorting(Scene farmScene)
    {
        if (!farmScene.IsValid() || !farmScene.isLoaded)
            return;

        bool changed = false;
        GameObject[] roots = farmScene.GetRootGameObjects();
        foreach (string rootName in FarmAuthoredRootNames)
        {
            GameObject root = FindRoot(roots, rootName);
            if (root == null)
                continue;

            changed |= ConfigureFenceSorting(root);
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(farmScene);
            EditorSceneManager.SaveScene(farmScene);
        }
    }

    public static bool ConfigureMapPropSorting(Scene farmScene)
    {
        if (!farmScene.IsValid() || !farmScene.isLoaded)
            return false;

        bool changed = false;
        GameObject[] roots = farmScene.GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            if (root == null)
                continue;

            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || renderer.sprite == null)
                    continue;

                string name = renderer.gameObject.name;
                bool isMapProp = name.StartsWith("Map Prop", StringComparison.OrdinalIgnoreCase) ||
                                 (renderer.transform.parent != null &&
                                  string.Equals(renderer.transform.parent.name, "Map Props (Generated)", StringComparison.OrdinalIgnoreCase));

                if (!isMapProp)
                    continue;

                int targetOrder = World.MapPropSorting.GetSortingOrder(renderer.sprite, renderer.bounds.min.y);

                if (renderer.sortingLayerName == World.MapPropSorting.SortingLayer && renderer.sortingOrder == targetOrder)
                    continue;

                Undo.RecordObject(renderer, "Update Map Prop Sorting Order");
                renderer.sortingLayerName = World.MapPropSorting.SortingLayer;
                renderer.sortingOrder = targetOrder;
                EditorUtility.SetDirty(renderer);
                changed = true;
            }
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(farmScene);
            EditorSceneManager.SaveScene(farmScene);
            Debug.Log("Synchronized Map Prop sorting orders in Level_Farm.");
        }

        return changed;
    }
}
#endif
