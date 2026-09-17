#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using World;

/// <summary>
/// Moves every grass decoration (co-cao, co-nho-1, co-nho-2) in Level_Farm to
/// the lowest order on the Dynamic layer so the player is always drawn on top
/// of grass. Trees and bushes keep their Y-based order.
/// </summary>
public static class ApplyGrassBelowPlayer
{
    private const string FarmScenePath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string VersionKey = "Meadom.GrassBelowPlayer.Version";
    private const int Version = 1;

    [InitializeOnLoadMethod]
    private static void ApplyAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetInt(VersionKey, 0) >= Version)
                return;
            if (Apply() >= 0)
                EditorPrefs.SetInt(VersionKey, Version);
        };
    }

    [MenuItem("Tools/Map/Put Grass Below Player")]
    public static void ApplyFromMenu() => Apply();

    /// <summary>Returns the number of grass renderers changed, or -1 if the scene could not be opened.</summary>
    private static int Apply()
    {
        Scene scene = SceneManager.GetSceneByPath(FarmScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Additive);
        if (!scene.IsValid())
            return -1;

        int changed = 0;
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!MapPropSorting.IsGrass(renderer.sprite))
                        continue;
                    if (renderer.sortingLayerName == MapPropSorting.SortingLayer &&
                        renderer.sortingOrder == MapPropSorting.GrassSortingOrder)
                        continue;

                    Undo.RecordObject(renderer, "Put Grass Below Player");
                    renderer.sortingLayerName = MapPropSorting.SortingLayer;
                    renderer.sortingOrder = MapPropSorting.GrassSortingOrder;
                    EditorUtility.SetDirty(renderer);
                    changed++;
                }
            }

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        Debug.Log($"Grass below player: updated {changed} grass renderers in Level_Farm.");
        return changed;
    }
}
#endif
