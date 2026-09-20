#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Deletes the leftover Farming Kit trees in Level_Farm. They are the disabled
/// "Tree" objects built from Tree_02 art under "-- World Objects --"; the map
/// now uses the props imported from the 16x16 reference instead.
/// </summary>
public static class RemoveLegacyKitTrees
{
    private const string LevelPath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string TreeObjectName = "Tree";
    private const string TreeArtChildName = "Tree_02_Top";
    private const string VersionKey = "Meadom.LegacyKitTrees.Removed";
    private const int Version = 1;

    [InitializeOnLoadMethod]
    private static void RemoveAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetInt(VersionKey, 0) >= Version)
                return;
            if (Remove() >= 0)
                EditorPrefs.SetInt(VersionKey, Version);
        };
    }

    [MenuItem("Tools/Map/Remove Legacy Kit Trees")]
    public static void RemoveFromMenu() => Remove();

    /// <summary>Returns how many trees were deleted, or -1 when the scene is unavailable.</summary>
    private static int Remove()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return -1;

        Scene scene = SceneManager.GetSceneByPath(LevelPath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Additive);
        if (!scene.IsValid())
            return -1;

        int removed = 0;
        try
        {
            List<GameObject> doomed = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != TreeObjectName || child.Find(TreeArtChildName) == null)
                        continue;
                    doomed.Add(child.gameObject);
                }
            }

            foreach (GameObject tree in doomed)
            {
                Undo.DestroyObjectImmediate(tree);
                removed++;
            }

            if (removed > 0)
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

        Debug.Log($"Legacy kit trees removed from Level_Farm: {removed}.");
        return removed;
    }
}
#endif
