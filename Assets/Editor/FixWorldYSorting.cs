#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FixWorldYSorting
{
    private const string LevelPath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string GeneratedPropsName = "Map Props (Generated)";

    [MenuItem("Tools/Map/Fix Player And Tree Y Sorting")]
    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before fixing world Y sorting.");
            return;
        }

        Scene level = SceneManager.GetSceneByPath(LevelPath);
        bool openedByTool = !level.IsValid() || !level.isLoaded;
        if (openedByTool)
            level = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Additive);

        int updated = 0;
        foreach (GameObject root in level.GetRootGameObjects())
        {
            Transform props = FindRecursively(root.transform, GeneratedPropsName);
            if (props == null)
                continue;

            foreach (SpriteRenderer renderer in props.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sortingLayerName == "Dynamic")
                    continue;

                renderer.sortingLayerName = "Dynamic";
                EditorUtility.SetDirty(renderer);
                updated++;
            }
        }

        if (updated > 0)
        {
            EditorSceneManager.MarkSceneDirty(level);
            EditorSceneManager.SaveScene(level);
        }

        if (openedByTool)
            EditorSceneManager.CloseScene(level, true);

        Debug.Log($"World Y sorting ready: {updated} generated map props moved to Dynamic sorting layer.");
    }

    private static Transform FindRecursively(Transform current, string targetName)
    {
        if (current.name == targetName)
            return current;

        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindRecursively(current.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
