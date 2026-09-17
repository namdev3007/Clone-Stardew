#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps Level_Farm loaded additively whenever Core 1 is being edited, so the
/// authored map is visible in Scene View before entering Play Mode.
/// </summary>
[InitializeOnLoad]
public static class FarmMapEditModeLoader
{
    private const string CoreScenePath = "Assets/MainScenes/Core 1.unity";
    private const string FarmScenePath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private static bool loadQueued;

    static FarmMapEditModeLoader()
    {
        EditorSceneManager.sceneOpened -= OnSceneOpened;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        QueueEnsureVisible();
    }

    [MenuItem("Tools/Map/Show Farm Map In Edit Mode")]
    private static void ShowFromMenu()
    {
        EnsureVisible(true);
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == CoreScenePath)
            QueueEnsureVisible();
    }

    private static void OnActiveSceneChanged(Scene previous, Scene next)
    {
        if (next.path == CoreScenePath)
            QueueEnsureVisible();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            QueueEnsureVisible();
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

        SceneView.RepaintAll();
    }
}
#endif
