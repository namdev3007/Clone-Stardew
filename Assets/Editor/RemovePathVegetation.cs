#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RemovePathVegetation
{
    private const string LevelPath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string BaseMapPath = "Assets/Sprites/bố cục map.png";
    private const string GeneratedPropsName = "Map Props (Generated)";
    private const string MigrationKey = "Meadom.RemovePathVegetation.Version";
    private const int MigrationVersion = 2;
    private const float PixelsPerUnit = 100f;
    private const float MapLeftWorld = -8.96f;
    private const float MapTopWorld = 6.24f;

    [InitializeOnLoadMethod]
    private static void ApplyAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorPrefs.GetInt(MigrationKey, 0) < MigrationVersion)
                Apply();
        };
    }

    [MenuItem("Tools/Map/Remove Vegetation From Paths")]
    private static void ApplyFromMenu()
    {
        Apply();
    }

    private static void Apply()
    {
        TextureImporter importer = AssetImporter.GetAtPath(BaseMapPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"Base map texture was not found: {BaseMapPath}");
            return;
        }

        bool restoreReadable = !importer.isReadable;
        Scene level = default;
        bool openedByTool = false;
        try
        {
            if (restoreReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath);
            Color32[] pixels = baseMap.GetPixels32();
            level = SceneManager.GetSceneByPath(LevelPath);
            openedByTool = !level.IsValid() || !level.isLoaded;
            if (openedByTool)
                level = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Additive);

            Transform generatedProps = FindGeneratedProps(level);
            if (generatedProps == null)
                throw new InvalidOperationException($"{GeneratedPropsName} was not found in Level_Farm.");

            List<GameObject> remove = new List<GameObject>();
            foreach (SpriteRenderer renderer in generatedProps.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null || renderer.sprite == null || !IsVegetation(renderer.sprite))
                    continue;

                Bounds bounds = renderer.bounds;
                if (IsPathAtWorldPoint(baseMap, pixels, bounds.center.x, bounds.min.y))
                    remove.Add(renderer.gameObject);
            }

            foreach (GameObject vegetation in remove)
                UnityEngine.Object.DestroyImmediate(vegetation);

            if (remove.Count > 0)
            {
                EditorSceneManager.MarkSceneDirty(level);
                EditorSceneManager.SaveScene(level);
            }

            EditorPrefs.SetInt(MigrationKey, MigrationVersion);
            Debug.Log($"Removed {remove.Count} vegetation props whose roots were on map paths.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (restoreReadable)
            {
                importer = AssetImporter.GetAtPath(BaseMapPath) as TextureImporter;
                if (importer != null)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }

            if (openedByTool && level.IsValid() && level.isLoaded)
                EditorSceneManager.CloseScene(level, true);
        }
    }

    private static Transform FindGeneratedProps(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindRecursively(root.transform);
            if (found != null)
                return found;
        }
        return null;
    }

    private static Transform FindRecursively(Transform current)
    {
        if (current.name == GeneratedPropsName)
            return current;
        for (int i = 0; i < current.childCount; i++)
        {
            Transform found = FindRecursively(current.GetChild(i));
            if (found != null)
                return found;
        }
        return null;
    }

    private static bool IsVegetation(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite);
        return path.Contains("/trang trí map-tách riêng/") ||
               path.EndsWith("/trang trí map.png", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/cay hoa gạo-Sheet.png", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/cây-chết.png", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPathAtWorldPoint(Texture2D texture, Color32[] pixels, float worldX, float worldY)
    {
        int centerX = Mathf.RoundToInt((worldX - MapLeftWorld) * PixelsPerUnit);
        int fromTop = Mathf.RoundToInt((MapTopWorld - worldY) * PixelsPerUnit);
        int centerY = texture.height - 1 - fromTop;
        int pathPixels = 0;
        int checkedPixels = 0;

        for (int oy = -3; oy <= 3; oy++)
        for (int ox = -5; ox <= 5; ox++)
        {
            int x = centerX + ox;
            int y = centerY + oy;
            if (x < 0 || x >= texture.width || y < 0 || y >= texture.height)
                continue;

            checkedPixels++;
            Color32 color = pixels[y * texture.width + x];
            if (color.a > 200 && color.r >= 175 && color.r <= 205 &&
                color.g >= 155 && color.g <= 190 && color.b >= 115 && color.b <= 160)
                pathPixels++;
        }

        return checkedPixels > 0 && pathPixels >= Mathf.CeilToInt(checkedPixels * 0.55f);
    }
}
#endif
