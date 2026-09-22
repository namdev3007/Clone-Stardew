#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using World;

/// <summary>Adds dense 16x16 grass clusters around three user-selected reference props.</summary>
internal static class DensifySpecificGrassClusters
{
    private const string ScenePath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string GrassPath = "Assets/Sprites/props-items/trang trí map-tách riêng/co-cao.png";
    private const string RootName = "Dense Grass Around Props 400 607 618 (Generated)";
    private const string AppliedVersionKey = "Meadom.DenseGrass.Props400607618.Version";
    private const int AppliedVersion = 1;
    private const float CellSize = 0.16f;

    private static readonly string[] AnchorNames =
    {
        "Map Prop 607 - co-cao",
        "Map Prop 400 - co-cao",
        "Map Prop 618 - co-cao"
    };

    // Close inner ring first, then fill the outer ring until each area has 12 new tufts.
    private static readonly Vector2Int[] Offsets =
    {
        new(-1, 0), new(1, 0), new(0, -1), new(0, 1),
        new(-1, -1), new(1, -1), new(-1, 1), new(1, 1),
        new(-2, 0), new(2, 0), new(0, -2), new(0, 2),
        new(-2, -1), new(-2, 1), new(2, -1), new(2, 1),
        new(-1, -2), new(1, -2), new(-1, 2), new(1, 2)
    };

    [InitializeOnLoadMethod]
    private static void ApplyOnceAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetInt(AppliedVersionKey, 0) >= AppliedVersion ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            Apply();
            EditorPrefs.SetInt(AppliedVersionKey, AppliedVersion);
        };
    }

    [MenuItem("Tools/Map/Densify Grass Around Props 400 607 618")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before changing Level_Farm.");

        Sprite grass = AssetDatabase.LoadAssetAtPath<Sprite>(GrassPath);
        if (grass == null)
            throw new FileNotFoundException("Tall-grass sprite was not found.", GrassPath);

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            Transform[] transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform[] anchors = AnchorNames.Select(name =>
                transforms.FirstOrDefault(candidate => candidate.name == name)).ToArray();
            if (anchors.Any(anchor => anchor == null))
                throw new MissingReferenceException("One or more requested grass anchors were not found.");

            Transform parent = anchors[0].parent;
            Transform oldRoot = transforms.FirstOrDefault(candidate => candidate.name == RootName);
            if (oldRoot != null)
                UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);

            GameObject rootObject = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(rootObject, scene);
            rootObject.transform.SetParent(parent, false);

            List<Vector2> occupied = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SpriteRenderer>(true))
                .Where(renderer => renderer.transform != rootObject.transform)
                .Select(renderer => (Vector2)renderer.transform.position)
                .ToList();

            int total = 0;
            for (int anchorIndex = 0; anchorIndex < anchors.Length; anchorIndex++)
            {
                Vector3 anchor = anchors[anchorIndex].position;
                int added = 0;
                foreach (Vector2Int offset in Offsets)
                {
                    if (added >= 12)
                        break;

                    Vector3 position = anchor + new Vector3(offset.x * CellSize, offset.y * CellSize, 0f);
                    if (occupied.Any(existing => Vector2.Distance(existing, position) < CellSize * 0.48f))
                        continue;

                    GameObject tuft = new GameObject($"Extra Grass {AnchorNames[anchorIndex]} #{added + 1}");
                    tuft.transform.SetParent(rootObject.transform, true);
                    tuft.transform.position = position;
                    SpriteRenderer renderer = tuft.AddComponent<SpriteRenderer>();
                    renderer.sprite = grass;
                    renderer.sortingLayerName = "Dynamic";
                    renderer.sortingOrder = MapPropSorting.GetSortingOrder(grass, position.y);

                    MapRegionGeneratedProp marker = tuft.AddComponent<MapRegionGeneratedProp>();
                    Vector3Int cell = new Vector3Int(
                        Mathf.RoundToInt(position.x / CellSize),
                        Mathf.RoundToInt(position.y / CellSize), 0);
                    marker.Initialize("Dense Grass Around Selected Props", cell, cell,
                        $"dense_grass_{anchorIndex}_{added}");

                    occupied.Add(position);
                    added++;
                    total++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"DENSE_GRASS_RESULT: Added {total} tall-grass props around Map Props 400, 607 and 618.");
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }
}
#endif
