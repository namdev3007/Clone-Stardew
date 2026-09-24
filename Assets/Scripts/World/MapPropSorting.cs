using System;
using Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace World
{
    /// <summary>
    /// Sorting rules for map decoration props on the "Dynamic" layer.
    /// Trees and bushes sort by the Y of their base like the player; grass is
    /// flat ground cover and always stays underneath the player and crops.
    /// </summary>
    public static class MapPropSorting
    {
        public const string SortingLayer = "Dynamic";

        /// <summary>Lowest order on the Dynamic layer, below every Y-sorted object.</summary>
        public const int GrassSortingOrder = short.MinValue;

        /// <summary>
        /// Trellis frames are scenery the crops grow in front of: above grass,
        /// but below every crop, prop and character that sorts by its Y.
        /// </summary>
        public const int TrellisBackgroundOrder = short.MinValue + 1000;

        /// <summary>Grass decoration sprites are exported as co-cao, co-nho-1, co-nho-2.</summary>
        public static bool IsGrass(Sprite sprite)
        {
            return sprite != null && sprite.name.StartsWith("co-", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Sorting order for a prop whose base touches the ground at <paramref name="groundY"/>.</summary>
        public static int GetSortingOrder(Sprite sprite, float groundY)
        {
            return IsGrass(sprite) ? GrassSortingOrder : Mathf.RoundToInt(-groundY * 100f);
        }
    }

    /// <summary>
    /// Installs depth sorting on authored fences (including the upright fence posts
    /// and the cucumber trellis boundary fences like hàng rào_3 (1)) without changing their
    /// authored SpriteRenderer Order in Layer. This also covers play mode when the farm
    /// scene is loaded after Core 1.
    /// </summary>
    public static class AuthoredFenceDepthInstaller
    {
        public static bool IsTargetFence(GameObject go)
        {
            if (go == null) return false;
            string name = go.name;
            if (string.Equals(name, "hàng rào_0 (1)", StringComparison.Ordinal) ||
                string.Equals(name, "hàng rào_3 (1)", StringComparison.Ordinal))
            {
                return true;
            }

            if (go.transform.parent != null &&
                string.Equals(go.transform.parent.name, "ruộng dưa chuột", StringComparison.OrdinalIgnoreCase) &&
                name.IndexOf("hàng rào", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            for (int i = 0; i < SceneManager.sceneCount; i++)
                ConfigureScene(SceneManager.GetSceneAt(i));
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConfigureScene(scene);
        }

        public static void ConfigureScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (SpriteRenderer renderer in renderers)
                {
                    if (renderer == null || !IsTargetFence(renderer.gameObject))
                        continue;

                    renderer.sortingLayerName = MapPropSorting.SortingLayer;

                    SortingGroup group = renderer.GetComponent<SortingGroup>();
                    if (group == null)
                        group = renderer.gameObject.AddComponent<SortingGroup>();
                    group.sortingLayerName = MapPropSorting.SortingLayer;

                    HeightBasedSorting sorting = renderer.GetComponent<HeightBasedSorting>();
                    if (sorting == null)
                        sorting = renderer.gameObject.AddComponent<HeightBasedSorting>();
                    sorting.ConfigureGroundAnchor(renderer);
                }
            }
        }
    }

    /// <summary>
    /// Automatically ensures all Map Props on the Dynamic layer have their
    /// sorting order aligned with their ground contact point at runtime.
    /// This guarantees that duplicated or repositioned props always sort
    /// properly with the player even if not saved in editor.
    /// </summary>
    public static class MapPropDepthInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            for (int i = 0; i < SceneManager.sceneCount; i++)
                ConfigureScene(SceneManager.GetSceneAt(i));
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConfigureScene(scene);
        }

        public static void ConfigureScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root == null) continue;

                SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (SpriteRenderer renderer in renderers)
                {
                    if (renderer == null || renderer.sprite == null) continue;

                    string name = renderer.gameObject.name;
                    bool isMapProp = name.StartsWith("Map Prop", StringComparison.OrdinalIgnoreCase) ||
                                     (renderer.transform.parent != null &&
                                      string.Equals(renderer.transform.parent.name, "Map Props (Generated)", StringComparison.OrdinalIgnoreCase));

                    if (!isMapProp) continue;

                    renderer.sortingLayerName = MapPropSorting.SortingLayer;
                    renderer.sortingOrder = MapPropSorting.GetSortingOrder(renderer.sprite, renderer.bounds.min.y);
                }
            }
        }
    }
}
