#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using World;

/// <summary>
/// Places the two requested decorative banana trees above Map Prop 814.
/// The names make the operation idempotent when scripts recompile.
/// </summary>
public static class AddTreesAboveMapProp814
{
    private const string ScenePath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string SourceName = "Map Prop 814 - bananatree_200_5";
    private const string Stage6Path = "Assets/Sprites/cây trồng/cây lâu năm/banana/bananatree_200-tách riêng/bananatree_200_6.png";
    private const string Stage7Path = "Assets/Sprites/cây trồng/cây lâu năm/banana/bananatree_200-tách riêng/bananatree_200_7.png";

    [InitializeOnLoadMethod]
    private static void QueueApply()
    {
        EditorApplication.delayCall += Apply;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall += Apply;
    }

    [MenuItem("Tools/Map/Add two trees above Map Prop 814")]
    private static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedByTool = !scene.IsValid() || !scene.isLoaded;
        if (openedByTool)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject source = Find(scene, SourceName);
            if (source == null)
            {
                Debug.LogWarning($"Could not find {SourceName} in {ScenePath}.");
                return;
            }

            Sprite stage6 = AssetDatabase.LoadAssetAtPath<Sprite>(Stage6Path);
            Sprite stage7 = AssetDatabase.LoadAssetAtPath<Sprite>(Stage7Path);
            if (stage6 == null || stage7 == null)
            {
                Debug.LogWarning("Could not load the separated banana stage 6/7 sprites.");
                return;
            }

            bool changed = false;
            changed |= EnsureClone(scene, source, "Map Prop 825 - bananatree_200_7",
                new Vector3(0f, 0.64f, 0f), new Vector3Int(86, 40, 0), new Vector3Int(30, 4, 0),
                "prop_0847_bananatree_200_7", stage7, "Map Prop 825 - bananatree_200_5");
            changed |= EnsureClone(scene, source, "Map Prop 826 - bananatree_200_6",
                new Vector3(0f, 1.28f, 0f), new Vector3Int(86, 36, 0), new Vector3Int(30, 8, 0),
                "prop_0848_bananatree_200_6", stage6, "Map Prop 826 - bananatree_200_5");

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Added two decorative banana trees above Map Prop 814.");
            }
        }
        finally
        {
            if (openedByTool && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static bool EnsureClone(Scene scene, GameObject source, string cloneName, Vector3 offset,
        Vector3Int sourceCell, Vector3Int targetCell, string propId, Sprite sprite, string legacyName)
    {
        GameObject clone = Find(scene, cloneName) ?? Find(scene, legacyName);
        bool changed = clone == null;
        if (clone == null)
            clone = Object.Instantiate(source, source.transform.parent);

        Vector3 desiredPosition = source.transform.localPosition + offset;
        if (clone.name != cloneName || clone.transform.localPosition != desiredPosition)
            changed = true;
        clone.name = cloneName;
        clone.transform.localPosition = desiredPosition;

        SpriteRenderer renderer = clone.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.sprite != sprite)
        {
            renderer.sprite = sprite;
            changed = true;
        }

        MapRegionGeneratedProp tracking = clone.GetComponent<MapRegionGeneratedProp>();
        if (tracking != null)
        {
            if (tracking.SourceCell != sourceCell || tracking.TargetCell != targetCell || tracking.PropId != propId)
                changed = true;
            tracking.Initialize(tracking.RegionName, sourceCell, targetCell, propId);
        }

        if (changed)
            EditorUtility.SetDirty(clone);
        return changed;
    }

    private static GameObject Find(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(transform => transform.name == objectName)?.gameObject;
    }
}
#endif
