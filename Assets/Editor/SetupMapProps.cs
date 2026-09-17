#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using World;

/// <summary>
/// Reconstructs the vegetation/prop layer by locating the authored prop sprites
/// inside the flattened reference map. Props are normal GameObjects, not tiles.
/// </summary>
public static class SetupMapProps
{
    private const string LevelPath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string ReferencePath = "Assets/Sprites/tileset/full trang tri-1.png";
    private const string BaseMapPath = "Assets/Sprites/bố cục map.png";
    private const string GroupName = "Map Props (Generated)";
    private const string VersionKey = "Meadom.MapProps.LayoutVersion";
    private const int LayoutVersion = 8;
    private const float PixelsPerUnit = 100f;
    private const float MapLeftWorld = -8.96f;
    private const float MapTopWorld = 6.24f;
    private const int ReferenceTopCrop = 96;

    private static readonly string[] PropTexturePaths =
    {
        "Assets/Sprites/props-items/trang trí map-tách riêng/cay-nho-1.png",
        "Assets/Sprites/props-items/trang trí map-tách riêng/cay-vua-1.png",
        "Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-1.png",
        "Assets/Sprites/props-items/trang trí map-tách riêng/cay-lon-2.png",
        "Assets/Sprites/props-items/trang trí map-tách riêng/co-cao.png",
        "Assets/Sprites/props-items/trang trí map-tách riêng/co-nho-1.png",
        "Assets/Sprites/props-items/trang trí map-tách riêng/co-nho-2.png",
        "Assets/Sprites/props-items/trang trí map-tách riêng/bui-cay.png",
        "Assets/Sprites/props-items/cay hoa gạo-Sheet.png",
        "Assets/Sprites/props-items/cây-chết.png",
        "Assets/Sprites/props-items/lu nước.png",
        "Assets/Sprites/props-items/ụ lúa-tách riêng/u-lua-vang.png",
        "Assets/Sprites/props-items/ụ lúa-tách riêng/u-lua-xanh.png"
    };

    private readonly struct PixelPoint
    {
        public readonly int x;
        public readonly int y;
        public readonly Color32 color;

        public PixelPoint(int x, int y, Color32 color)
        {
            this.x = x;
            this.y = y;
            this.color = color;
        }
    }

    private readonly struct Match
    {
        public readonly Sprite sprite;
        public readonly int left;
        public readonly int bottom;
        public readonly float scale;

        public Match(Sprite sprite, int left, int bottom, float scale = 1f)
        {
            this.sprite = sprite;
            this.left = left;
            this.bottom = bottom;
            this.scale = scale;
        }

        public int Width => Mathf.RoundToInt(sprite.rect.width * scale);
        public int Height => Mathf.RoundToInt(sprite.rect.height * scale);
    }

    [InitializeOnLoadMethod]
    private static void InstallAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorPrefs.GetInt(VersionKey, 0) < LayoutVersion)
                Install(false);
        };
    }

    [MenuItem("Tools/Map/Place Props From Reference Layout")]
    private static void InstallFromMenu()
    {
        Install(true);
    }

    public static void InstallFromCommandLine()
    {
        Install(true);
    }

    private static void Install(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Stop Play Mode before placing reference map props.");
            return;
        }

        List<TextureImporter> importersToRestore = new List<TextureImporter>();
        Scene scene = default;
        bool openedByTool = false;
        try
        {
            MakeReadable(ReferencePath, importersToRestore);
            MakeReadable(BaseMapPath, importersToRestore);
            foreach (string path in PropTexturePaths)
                MakeReadable(path, importersToRestore);

            Texture2D reference = AssetDatabase.LoadAssetAtPath<Texture2D>(ReferencePath);
            Texture2D baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseMapPath);
            if (reference == null || baseMap == null)
                throw new InvalidOperationException("Reference map or base map texture is missing.");

            Color32[] referencePixels = reference.GetPixels32();
            Color32[] basePixels = baseMap.GetPixels32();
            List<Sprite> sprites = LoadPropSprites();
            List<Match> matches = new List<Match>();
            foreach (Sprite sprite in sprites)
            {
                List<Match> spriteMatches = FindMatches(reference, referencePixels, sprite).ToList();
                matches.AddRange(spriteMatches);
            }

            // Different prop sprites occasionally share a small common image. Keep
            // only one object for the same bottom-centre anchor, preferring the
            // larger authored sprite.
            matches = matches
                .OrderByDescending(match => match.sprite.rect.width * match.sprite.rect.height)
                .GroupBy(match => GetAnchorKey(match, reference.height))
                .Select(group => group.First())
                .OrderBy(match => reference.height - match.bottom)
                .ThenBy(match => match.left)
                .ToList();

            scene = SceneManager.GetSceneByPath(LevelPath);
            openedByTool = !scene.IsValid() || !scene.isLoaded;
            if (openedByTool)
                scene = EditorSceneManager.OpenScene(LevelPath, OpenSceneMode.Additive);

            GridManager manager = FindInScene<GridManager>(scene);
            if (manager == null)
                throw new InvalidOperationException("Grid Manager was not found in Level_Farm.");

            Transform oldGroup = manager.transform.Find(GroupName);
            if (oldGroup != null)
                UnityEngine.Object.DestroyImmediate(oldGroup.gameObject);

            GameObject group = new GameObject(GroupName);
            group.transform.SetParent(manager.transform, false);

            int created = 0;
            int alreadyBaked = 0;
            int outsideCroppedMap = 0;
            int skippedOnPaths = 0;
            foreach (Match match in matches)
            {
                int referenceTop = reference.height - (match.bottom + match.Height);
                int newTop = referenceTop - ReferenceTopCrop;
                int newBottom = newTop + match.Height;
                if (newBottom <= 0 || newTop >= baseMap.height)
                {
                    outsideCroppedMap++;
                    continue;
                }

                int baseBottom = baseMap.height - newBottom;
                if (IsVegetation(match.sprite) && !IsFlowerTree(match.sprite) &&
                    IsPathAtGround(baseMap, basePixels, match, reference.height))
                {
                    skippedOnPaths++;
                    continue;
                }

                // The flattened base image still contains a few authored props
                // (notably the flower tree and water jars). Keep those as real
                // GameObjects too so Y sorting/colliders work as requested.
                if (!IsAlwaysPhysicalProp(match.sprite) &&
                    IsAlreadyBaked(baseMap, basePixels, match.sprite, match.left, baseBottom))
                {
                    alreadyBaked++;
                    continue;
                }

                CreateProp(group.transform, match, reference.height, created++);
            }

            EditorUtility.SetDirty(group);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorPrefs.SetInt(VersionKey, LayoutVersion);
            Debug.Log($"Reference map props installed: {created} GameObjects, " +
                      $"{alreadyBaked} already baked into the base map, {outsideCroppedMap} above/below the cropped map, " +
                      $"{skippedOnPaths} vegetation props skipped on paths.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            foreach (TextureImporter importer in importersToRestore)
            {
                if (importer == null)
                    continue;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }

            if (openedByTool && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static List<Sprite> LoadPropSprites()
    {
        List<Sprite> result = new List<Sprite>();
        foreach (string path in PropTexturePaths)
        {
            result.AddRange(AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<Sprite>()
                .Where(sprite => !sprite.name.Contains("ban-khoa") && sprite.rect.width > 8f && sprite.rect.height > 8f));
        }
        return result;
    }

    private static IEnumerable<Match> FindMatches(Texture2D reference, Color32[] referencePixels, Sprite sprite)
    {
        // This unique tree is present once in the flattened authoring reference.
        // Its large source texture is compressed differently from the full-map
        // texture by Unity, so preserve the verified authored pixel anchor.
        if (IsFlowerTree(sprite) && reference.width == 1808 && reference.height == 1328)
        {
            yield return new Match(sprite, 487, 639);
            yield break;
        }

        Rect rect = sprite.rect;
        float renderScale = IsWaterJar(sprite) ? 0.5f : 1f;
        int sourceWidth = Mathf.RoundToInt(rect.width);
        int sourceHeight = Mathf.RoundToInt(rect.height);
        int width = Mathf.RoundToInt(sourceWidth * renderScale);
        int height = Mathf.RoundToInt(sourceHeight * renderScale);
        int spriteStartX = Mathf.RoundToInt(rect.x);
        int spriteStartY = Mathf.RoundToInt(rect.y);
        Color32[] sourcePixels = sprite.texture.GetPixels32();
        List<PixelPoint> opaque = new List<PixelPoint>();

        bool matchByTrunk = IsTreeLike(sprite);
        bool isWaterJar = IsWaterJar(sprite);
        bool isFlowerTree = IsFlowerTree(sprite);
        bool isRicePile = IsRicePile(sprite);
        bool allowPartialMatch = isWaterJar || isFlowerTree || isRicePile;
        int colorTolerance = isFlowerTree ? 24 : isWaterJar ? 12 : 8;
        float requiredOpaqueRatio = isFlowerTree ? 0.82f : isWaterJar || isRicePile ? 0.88f : 1f;
        int matchHeight = matchByTrunk ? Mathf.Min(12, height) : height;
        int sourceStep = renderScale < 1f ? 2 : 1;

        // Canopies overlap heavily in the authored map. For trees, matching the
        // opaque lower trunk/root pixels recovers every planted tree even when
        // another crown covers its upper half.
        for (int y = 0; y < matchHeight; y++)
        {
            int sourceRow = (spriteStartY + y * sourceStep) * sprite.texture.width + spriteStartX;
            for (int x = 0; x < width; x++)
            {
                Color32 color = sourcePixels[sourceRow + x * sourceStep];
                if (color.a >= 200)
                    opaque.Add(new PixelPoint(x, y, color));
            }
        }

        if (opaque.Count < 8)
            yield break;

        PixelPoint[] probes = SelectProbes(opaque);
        List<Vector2Int> accepted = new List<Vector2Int>();
        for (int bottom = 0; bottom <= reference.height - height; bottom++)
        {
            for (int left = 0; left <= reference.width - width; left++)
            {
                int matchingProbes = 0;
                for (int probeIndex = 0; probeIndex < probes.Length; probeIndex++)
                {
                    PixelPoint probe = probes[probeIndex];
                    if (SimilarRgb(referencePixels[(bottom + probe.y) * reference.width + left + probe.x],
                            probe.color, colorTolerance))
                        matchingProbes++;
                }
                int requiredProbes = allowPartialMatch ? Mathf.CeilToInt(probes.Length * 0.67f) : probes.Length;
                if (matchingProbes < requiredProbes ||
                    !AllOpaquePixelsMatch(referencePixels, reference.width, left, bottom, opaque,
                        colorTolerance, requiredOpaqueRatio))
                    continue;

                Vector2Int candidate = new Vector2Int(left, bottom);
                if (accepted.Any(existing => Mathf.Abs(existing.x - candidate.x) <= 2 &&
                                             Mathf.Abs(existing.y - candidate.y) <= 2))
                    continue;
                accepted.Add(candidate);
                yield return new Match(sprite, left, bottom, renderScale);
            }
        }
    }

    private static PixelPoint[] SelectProbes(IReadOnlyList<PixelPoint> opaque)
    {
        // Spread probes across the opaque pixel list; six exact RGB checks reject
        // almost every map position before the full sprite comparison is needed.
        int[] indices = { 0, opaque.Count / 5, opaque.Count * 2 / 5, opaque.Count * 3 / 5, opaque.Count * 4 / 5, opaque.Count - 1 };
        return indices.Distinct().Select(index => opaque[index]).ToArray();
    }

    private static bool AllOpaquePixelsMatch(Color32[] referencePixels, int referenceWidth,
        int left, int bottom, IReadOnlyList<PixelPoint> opaque, int tolerance, float requiredRatio)
    {
        int matching = 0;
        for (int i = 0; i < opaque.Count; i++)
        {
            PixelPoint point = opaque[i];
            if (SimilarRgb(referencePixels[(bottom + point.y) * referenceWidth + left + point.x],
                    point.color, tolerance))
                matching++;
            else if (requiredRatio >= 1f)
                return false;
        }
        return matching >= Mathf.CeilToInt(opaque.Count * requiredRatio);
    }

    private static bool IsAlreadyBaked(Texture2D baseMap, Color32[] basePixels, Sprite sprite, int left, int bottom)
    {
        int width = Mathf.RoundToInt(sprite.rect.width);
        int height = Mathf.RoundToInt(sprite.rect.height);
        if (left < 0 || bottom < 0 || left + width > baseMap.width || bottom + height > baseMap.height)
            return false;

        int spriteStartX = Mathf.RoundToInt(sprite.rect.x);
        int spriteStartY = Mathf.RoundToInt(sprite.rect.y);
        Color32[] spritePixels = sprite.texture.GetPixels32();
        int checkedPixels = 0;
        int matchingPixels = 0;
        for (int y = 0; y < height; y += 2)
        {
            for (int x = 0; x < width; x += 2)
            {
                Color32 source = spritePixels[(spriteStartY + y) * sprite.texture.width + spriteStartX + x];
                if (source.a < 200)
                    continue;
                checkedPixels++;
                if (SameRgb(basePixels[(bottom + y) * baseMap.width + left + x], source))
                    matchingPixels++;
            }
        }
        return checkedPixels > 0 && matchingPixels >= checkedPixels * 0.9f;
    }

    private static bool IsVegetation(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite);
        return path.Contains("/trang trí map-tách riêng/") ||
               path.EndsWith("/trang trí map.png", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/cay hoa gạo-Sheet.png", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith("/cây-chết.png", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTreeLike(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite);
        return (path.Contains("/trang trí map-tách riêng/") && sprite.rect.height >= 50f) ||
               path.EndsWith("/cay hoa gạo-Sheet.png", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWaterJar(Sprite sprite)
    {
        return AssetDatabase.GetAssetPath(sprite)
            .EndsWith("/lu nước.png", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFlowerTree(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite);
        // Keep this check Unicode-normalisation safe; Windows/Unity can return
        // the Vietnamese filename in composed or decomposed form.
        return path.IndexOf("/cay hoa ", StringComparison.OrdinalIgnoreCase) >= 0 &&
               path.EndsWith("-Sheet.png", StringComparison.OrdinalIgnoreCase) &&
               sprite.rect.width > 100f;
    }

    private static bool IsRicePile(Sprite sprite)
    {
        return AssetDatabase.GetAssetPath(sprite)
            .IndexOf("/ụ lúa-tách riêng/", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsAlwaysPhysicalProp(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite);
        return IsWaterJar(sprite) || IsFlowerTree(sprite);
    }

    private static bool IsPathAtGround(Texture2D baseMap, Color32[] pixels, Match match, int referenceHeight)
    {
        int x = Mathf.RoundToInt(match.left + match.Width * 0.5f);
        int groundFromTop = Mathf.RoundToInt(referenceHeight - match.bottom - ReferenceTopCrop);
        int y = baseMap.height - 1 - groundFromTop;
        int pathPixels = 0;
        int checkedPixels = 0;

        for (int oy = -3; oy <= 3; oy++)
        for (int ox = -5; ox <= 5; ox++)
        {
            int sampleX = x + ox;
            int sampleY = y + oy;
            if (sampleX < 0 || sampleX >= baseMap.width || sampleY < 0 || sampleY >= baseMap.height)
                continue;

            checkedPixels++;
            if (IsPathColor(pixels[sampleY * baseMap.width + sampleX]))
                pathPixels++;
        }

        return checkedPixels > 0 && pathPixels >= Mathf.CeilToInt(checkedPixels * 0.55f);
    }

    private static bool IsPathColor(Color32 color)
    {
        return color.a > 200 && color.r >= 175 && color.r <= 205 &&
               color.g >= 155 && color.g <= 190 && color.b >= 115 && color.b <= 160;
    }

    private static void CreateProp(Transform parent, Match match, int referenceHeight, int index)
    {
        Sprite sprite = match.sprite;
        float anchorPixelX = match.left + match.Width * 0.5f;
        float referenceBottomFromTop = referenceHeight - match.bottom;
        float newBottomFromTop = referenceBottomFromTop - ReferenceTopCrop;
        Vector3 groundAnchor = new Vector3(
            MapLeftWorld + anchorPixelX / PixelsPerUnit,
            MapTopWorld - newBottomFromTop / PixelsPerUnit,
            0f);

        GameObject prop = new GameObject($"Map Prop {index:000} - {sprite.name}");
        prop.transform.SetParent(parent, false);
        Bounds bounds = sprite.bounds;
        prop.transform.localScale = new Vector3(match.scale, match.scale, 1f);
        prop.transform.position = groundAnchor -
                                  new Vector3(bounds.center.x * match.scale, bounds.min.y * match.scale, 0f);

        SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        renderer.sortingLayerName = "Dynamic";
        renderer.sortingOrder = Mathf.RoundToInt(-groundAnchor.y * 100f);

        bool isGroundDetail = sprite.name.EndsWith("_4") || sprite.name.EndsWith("_5") || sprite.name.EndsWith("_6");
        if (!isGroundDetail && bounds.size.x >= 0.25f && bounds.size.y >= 0.25f)
        {
            BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(Mathf.Max(0.12f / match.scale, bounds.size.x * 0.42f),
                Mathf.Clamp(bounds.size.y * 0.16f, 0.08f / match.scale, 0.24f / match.scale));
            collider.offset = new Vector2(bounds.center.x, bounds.min.y + collider.size.y * 0.5f);
        }
    }

    private static string GetAnchorKey(Match match, int referenceHeight)
    {
        int centreX = Mathf.RoundToInt(match.left + match.Width * 0.5f);
        int bottomFromTop = referenceHeight - match.bottom;
        return centreX + ":" + bottomFromTop;
    }

    private static bool SameRgb(Color32 left, Color32 right)
    {
        return SimilarRgb(left, right, 8);
    }

    private static bool SimilarRgb(Color32 left, Color32 right, int tolerance)
    {
        return Mathf.Abs(left.r - right.r) <= tolerance &&
               Mathf.Abs(left.g - right.g) <= tolerance &&
               Mathf.Abs(left.b - right.b) <= tolerance;
    }

    private static void MakeReadable(string path, ICollection<TextureImporter> restore)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            restore.Add(importer);
        }
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T result = root.GetComponentInChildren<T>(true);
            if (result != null)
                return result;
        }
        return null;
    }
}
#endif
