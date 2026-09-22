#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using World;

public static class BuildSpecialCropMapPreview
{
    private const string FarmScenePath = "Assets/Scenes/Levels/OutDoors/Level_Farm.unity";
    private const string ConfigPath = "Assets/Resources/Farming/Special Crop Runtime Config.asset";
    private const string PreviewRootName = "Special Crop Areas (Editor Preview)";
    private const string NamedContentRootName = "Named Region Content (Generated)";
    private const string BananaTreePath = "Assets/Sprites/c\u00e2y tr\u1ed3ng/c\u00e2y l\u00e2u n\u0103m/banana/bananatree_200.png";
    private const string LockSignPath = "Assets/Sprites/props-items/ban-khoa.png";
    private static readonly Vector3Int RemovedDecorativeBananaCell = new Vector3Int(-22, 21, 0);

    [InitializeOnLoadMethod]
    private static void QueueBuild()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                Build(false);
        };
    }

    [MenuItem("Tools/Farming/Build Special Crop Areas In Map")]
    public static void BuildFromMenu() => Build(true);

    [MenuItem("Tools/Map/Apply Named Regions To Farm")]
    public static void ApplyNamedRegionsFromMenu() => Build(true);

    private static void Build(bool forceRebuild)
    {
        SpecialCropRuntimeConfig config = AssetDatabase.LoadAssetAtPath<SpecialCropRuntimeConfig>(ConfigPath);
        if (config == null || config.regions == null)
            return;

        Scene scene = SceneManager.GetSceneByPath(FarmScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere)
            scene = EditorSceneManager.OpenScene(FarmScenePath, OpenSceneMode.Additive);

        try
        {
            RemoveLegacyRootObjects(scene);
            Transform existing = FindRoot(scene, PreviewRootName);
            GridManager gridManager = FindInScene<GridManager>(scene);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);

            if (gridManager == null)
            {
                Debug.LogWarning("Cannot build special crop preview: Level_Farm has no GridManager.");
                return;
            }

            MapRegionDefinition cucumber = FindRegion(config.regions, "v\u00f9ng tr\u1ed3ng c\u00e2y", "d\u01b0a chu\u1ed9t");
            MapRegionDefinition dragon = FindRegion(config.regions, "\u0111\u1ea5t tr\u1ed3ng thanh long");
            MapRegionDefinition cucumberLock = FindRegion(config.regions, "c\u1eafm bi\u1ec3n", "d\u01b0a chu\u1ed9t");
            MapRegionDefinition dragonLock = FindRegion(config.regions, "c\u1eafm bi\u1ec3n", "thanh long");
            Sprite lockSign = config.lockSign != null
                ? config.lockSign
                : AssetDatabase.LoadAllAssetsAtPath(LockSignPath).OfType<Sprite>().FirstOrDefault();
            if (cucumber == null || dragon == null)
            {
                Debug.LogWarning("Cannot build special crop preview: named cucumber/dragon regions are missing.");
                return;
            }

            GameObject root = new GameObject(PreviewRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<SpecialCropEditorPreview>();

            ConfigureAuthoredCucumberState(scene, false);

            BuildArea(root.transform, "Cucumber Trellis", cucumber, gridManager,
                cucumberLock, First(config.cucumberStages), Array.Empty<Sprite>(), false, lockSign);
            BuildArea(root.transform, "Dragon Fruit Field", dragon, gridManager,
                dragonLock, First(config.dragonFruitStages), config.dragonFruitBrokenSprites, true, lockSign);
            BuildNamedRegionColliders(scene, config.regions, gridManager);
            BuildNamedRegionContent(scene, config.regions, gridManager);

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Special crop areas are now visible in Level_Farm Edit Mode; post and lock colliders are attached.");
        }
        finally
        {
            if (openedHere && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void RemoveLegacyRootObjects(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            string objectName = root.name;
            if (objectName.StartsWith("Cucumber Planting Post ", StringComparison.Ordinal) ||
                objectName.StartsWith("Dragon Fruit Planting Post ", StringComparison.Ordinal) ||
                objectName == "Cucumber Repair Lock" ||
                objectName == "Dragon Fruit Repair Lock")
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }

    private static void BuildNamedRegionColliders(Scene scene, MapRegionCollection collection, GridManager grid)
    {
        if (collection == null || grid == null)
            return;

        const string rootName = "Named Region Box Colliders";
        // Remove the temporary root-level objects created by the old YAML based
        // builder. They duplicate these colliders and can leave broken SceneRoot
        // references when Unity repairs the scene during import.
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        {
            if (sceneRoot.name.StartsWith("đặt box collider [", StringComparison.OrdinalIgnoreCase) ||
                sceneRoot.name.StartsWith("đặt box colider [", StringComparison.OrdinalIgnoreCase) ||
                sceneRoot.name.StartsWith("đặt boxcolider [", StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.Object.DestroyImmediate(sceneRoot);
            }
        }

        Transform oldRoot = FindRoot(scene, rootName);
        if (oldRoot != null)
            UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);

        GameObject root = new GameObject(rootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        int count = 0;
        foreach (MapRegionDefinition region in collection.Regions)
        {
            if (region == null || !IsColliderRegion(region.RegionName))
                continue;

            Vector3 cellSize = grid.Grid.cellSize;
            Vector3 minWorld = grid.Grid.CellToWorld(region.MinCell);
            Vector3 maxWorld = grid.Grid.CellToWorld(region.MaxCell + new Vector3Int(1, 1, 0));
            Vector3 size = maxWorld - minWorld;

            GameObject colliderObject = new GameObject(
                $"{region.RegionName} [{region.MinCell.x},{region.MinCell.y} to {region.MaxCell.x},{region.MaxCell.y}]");
            colliderObject.transform.SetParent(root.transform, false);
            colliderObject.transform.position = (minWorld + maxWorld) * 0.5f;
            BoxCollider2D collider = colliderObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = false;
            collider.size = new Vector2(
                Mathf.Max(cellSize.x, Mathf.Abs(size.x)),
                Mathf.Max(cellSize.y, Mathf.Abs(size.y)));
            count++;
        }

        EditorUtility.SetDirty(root);
        Debug.Log($"Built {count} named BoxCollider2D regions in Level_Farm.");
    }

    private static bool IsColliderRegion(string regionName)
    {
        if (string.IsNullOrEmpty(regionName))
            return false;
        return regionName.IndexOf("collider", StringComparison.OrdinalIgnoreCase) >= 0 ||
               regionName.IndexOf("colider", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void BuildNamedRegionContent(Scene scene, MapRegionCollection collection, GridManager grid)
    {
        Transform oldRoot = FindRoot(scene, NamedContentRootName);
        if (oldRoot != null)
            UnityEngine.Object.DestroyImmediate(oldRoot.gameObject);

        GameObject root = new GameObject(NamedContentRootName);
        SceneManager.MoveGameObjectToScene(root, scene);

        // NPC transforms are authored directly in Level_Farm. Rebuilding map previews must
        // not move them back to old Map Region coordinates after the map has been edited.
        // Do not rebuild decorative banana trees. Banana crops planted by the player are
        // managed by the farming system and are not part of this Edit Mode preview root.
        // The dense forest region is intentionally left empty (trees removed).
        // Wells are no longer generated here; they are placed by hand in the scene.
        EditorUtility.SetDirty(root);
    }

    private static void ConfigureAuthoredCucumberState(Scene scene, bool repaired)
    {
        Transform authoredRoot = FindRoot(scene, "ruộng dưa chuột");
        if (authoredRoot == null)
            return;

        foreach (Transform child in authoredRoot)
        {
            bool isBroken = child.name.IndexOf("brokenfence_200", StringComparison.OrdinalIgnoreCase) >= 0;
            // The authored fence/posts are permanent map geometry. Only the
            // broken trellis overlay changes when the plot is repaired.
            child.gameObject.SetActive(isBroken ? !repaired : true);
            EditorUtility.SetDirty(child.gameObject);
        }
        EditorUtility.SetDirty(authoredRoot);
    }

    private static void BuildDecorativeBananas(Transform parent, MapRegionCollection collection, GridManager grid)
    {
        List<Sprite> bananaStages = AssetDatabase.LoadAllAssetsAtPath(BananaTreePath)
            .OfType<Sprite>()
            .OrderBy(sprite => ExtractTrailingNumber(sprite.name))
            .Where(sprite =>
            {
                int stage = ExtractTrailingNumber(sprite.name);
                // Decorative bananas must only use the two later static stages.
                return stage >= 5 && stage <= 6;
            })
            .ToList();
        if (bananaStages.Count == 0)
            return;

        GameObject group = new GameObject("Decorative Bananas - Static");
        group.transform.SetParent(parent, false);
        HashSet<string> processedBounds = new HashSet<string>(StringComparer.Ordinal);
        System.Random random = new System.Random(17092026);

        foreach (MapRegionDefinition region in collection.Regions)
        {
            if (region == null || region.RegionName.IndexOf("chu\u1ed1i", StringComparison.OrdinalIgnoreCase) < 0 ||
                region.RegionName.IndexOf("tr\u1ed3ng", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            string boundsKey = $"{region.MinCell.x},{region.MinCell.y}:{region.MaxCell.x},{region.MaxCell.y}";
            if (!processedBounds.Add(boundsKey))
                continue;

            Match countMatch = Regex.Match(region.RegionName, @"\d+");
            int count = countMatch.Success ? Mathf.Max(1, int.Parse(countMatch.Value)) : 1;
            List<Vector3Int> cells = EvenlyDistributedCells(region, count);
            for (int i = 0; i < cells.Count; i++)
            {
                // This authored position must remain clear; keep it excluded on
                // every automatic Edit Mode preview rebuild.
                if (cells[i] == RemovedDecorativeBananaCell)
                    continue;

                Sprite banana = bananaStages[random.Next(bananaStages.Count)];
                // bananatree_200 is authored at roughly twice the visual size of
                // the regular map-decoration trees, so display it at half scale.
                CreateStaticProp(group.transform, region, cells[i], banana, grid, 0.5f, true);
            }
        }
    }

    private static List<Vector3Int> EvenlyDistributedCells(MapRegionDefinition region, int count)
    {
        List<Vector3Int> all = new List<Vector3Int>();
        for (int y = region.MinCell.y; y <= region.MaxCell.y; y++)
        for (int x = region.MinCell.x; x <= region.MaxCell.x; x++)
            all.Add(new Vector3Int(x, y, 0));
        if (count >= all.Count)
            return all;

        List<Vector3Int> result = new List<Vector3Int>(count);
        for (int i = 0; i < count; i++)
        {
            int index = count == 1
                ? all.Count / 2
                : Mathf.RoundToInt(i * (all.Count - 1f) / (count - 1f));
            result.Add(all[index]);
        }
        return result;
    }

    private static void CreateStaticProp(Transform parent, MapRegionDefinition region, Vector3Int cell,
        Sprite sprite, GridManager grid, float scale, bool addCollider)
    {
        Vector3 anchor = grid.GetWorldLocation(cell);
        Bounds bounds = sprite.bounds;
        GameObject prop = new GameObject($"[{cell.x},{cell.y}] {sprite.name}");
        prop.transform.SetParent(parent, true);
        prop.transform.position = anchor - new Vector3(bounds.center.x, bounds.min.y, 0f) * scale;
        prop.transform.localScale = Vector3.one * scale;

        SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        renderer.sortingLayerName = "Dynamic";
        renderer.sortingOrder = Mathf.RoundToInt(-anchor.y * 100f);
        prop.AddComponent<MapRegionGeneratedProp>().Initialize(region.RegionName, cell);

        if (addCollider)
        {
            BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(
                Mathf.Max(0.06f, bounds.size.x * 0.28f),
                Mathf.Max(0.05f, bounds.size.y * 0.14f));
            collider.offset = new Vector2(bounds.center.x, bounds.min.y + collider.size.y * 0.5f);
        }
    }

    private static int ExtractTrailingNumber(string value)
    {
        int separator = value.LastIndexOf('_');
        return separator >= 0 && int.TryParse(value.Substring(separator + 1), out int number) ? number : 0;
    }

    private static void BuildArea(Transform parent, string name, MapRegionDefinition region,
        GridManager grid, MapRegionDefinition lockRegion, Sprite emptyPost,
        IReadOnlyList<Sprite> brokenSprites, bool fillEveryBrokenSlot, Sprite lockSprite)
    {
        GameObject area = new GameObject(name + " [" + region.RegionName + "]");
        area.transform.SetParent(parent, false);

        // Preview the locked state: plain soil plus the repair sign. The posts
        // are kept (inactive) only so their cells can be inspected in the editor.
        GameObject slotsRoot = new GameObject("Repaired State - 8 Planting Slots");
        slotsRoot.transform.SetParent(area.transform, false);
        slotsRoot.SetActive(false);
        List<Vector3Int> slots = BuildEightSlots(region);
        for (int i = 0; i < slots.Count; i++)
        {
            Vector3Int cell = slots[i];
            // Trellis planting cells are permanently tilled, including the
            // locked Edit Mode preview. Runtime enforces the same invariant.
            grid.EnsureDirtTile(cell);
            if (!grid.HasDirtHole(cell))
                grid.SetDirtHoleTile(cell);

            GameObject post = new GameObject($"Planting Post {i + 1} [{cell.x},{cell.y}]");
            post.transform.SetParent(slotsRoot.transform, false);
            post.transform.position = grid.GetWorldLocation(cell);
            AddSprite(post.transform, "State 0 Visual", emptyPost, 0.5f);

            BoxCollider2D collider = post.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.075f, 0.12f);
            collider.offset = new Vector2(0f, 0.06f);
            collider.isTrigger = false;
        }

        GameObject brokenRoot = new GameObject("Broken State Visuals");
        brokenRoot.transform.SetParent(area.transform, false);
        if (brokenSprites != null && brokenSprites.Count > 0)
        {
            int pieceCount = fillEveryBrokenSlot ? slots.Count : brokenSprites.Count;
            for (int i = 0; i < pieceCount; i++)
            {
                Sprite sprite = brokenSprites[i % brokenSprites.Count];
                if (sprite == null)
                    continue;
                Vector3Int cell = fillEveryBrokenSlot
                    ? slots[i]
                    : slots[Mathf.Min(i * 2, slots.Count - 1)];
                GameObject piece = new GameObject($"Broken Post {i + 1} [{cell.x},{cell.y}]");
                piece.transform.SetParent(brokenRoot.transform, false);
                piece.transform.position = grid.GetWorldLocation(cell);
                AddSprite(piece.transform, "Visual", sprite, 0.5f);
                BoxCollider2D collider = piece.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.14f, 0.10f);
                collider.offset = new Vector2(0f, 0.05f);
            }
        }

        Vector3Int lockCell = lockRegion != null
            ? lockRegion.MinCell
            : new Vector3Int(
                Mathf.RoundToInt((region.MinCell.x + region.MaxCell.x) * 0.5f), region.MinCell.y, 0);
        GameObject lockObject = new GameObject($"Repair Lock [{lockCell.x},{lockCell.y}]");
        lockObject.transform.SetParent(area.transform, false);
        lockObject.transform.position = grid.GetWorldLocation(lockCell);
        // The source is only 23x32 px. At 0.5 scale it was effectively a tiny
        // dot in the map and looked as though the repair sign had disappeared.
        AddSprite(lockObject.transform, "Visual", lockSprite, 1f);
        BoxCollider2D lockCollider = lockObject.AddComponent<BoxCollider2D>();
        lockCollider.size = new Vector2(0.14f, 0.14f);
        lockCollider.isTrigger = true;

        GameObject blocker = new GameObject("Sign Collider");
        blocker.transform.SetParent(lockObject.transform, false);
        BoxCollider2D solid = blocker.AddComponent<BoxCollider2D>();
        solid.size = SpecialCropAreaController.LockSignColliderSize;
        solid.offset = SpecialCropAreaController.LockSignColliderOffset;
    }

    private static List<Vector3Int> BuildEightSlots(MapRegionDefinition region)
    {
        List<Vector3Int> slots = new List<Vector3Int>(8);
        int minX = region.MinCell.x + (region.Width > 2 ? 1 : 0);
        int maxX = region.MaxCell.x - (region.Width > 2 ? 1 : 0);
        int minY = region.MinCell.y + (region.Height > 2 ? 1 : 0);
        int maxY = region.MaxCell.y - (region.Height > 2 ? 1 : 0);
        for (int row = 0; row < 2; row++)
        {
            int y = Mathf.RoundToInt(Mathf.Lerp(minY, maxY, row));
            for (int column = 0; column < 4; column++)
            {
                int x = Mathf.RoundToInt(Mathf.Lerp(minX, maxX, column / 3f));
                slots.Add(new Vector3Int(x, y, 0));
            }
        }
        return slots;
    }

    private static void AddSprite(Transform parent, string name, Sprite sprite, float scale)
    {
        GameObject visual = new GameObject(name);
        visual.transform.SetParent(parent, false);
        visual.transform.localScale = Vector3.one * scale;
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingLayerName = "Dynamic";
        renderer.sortingOrder = Mathf.RoundToInt(-parent.position.y * 100f);
        if (sprite == null)
            return;
        float x = (sprite.pivot.x - sprite.rect.width * 0.5f) / sprite.pixelsPerUnit * scale;
        float y = sprite.pivot.y / sprite.pixelsPerUnit * scale;
        visual.transform.localPosition = new Vector3(x, y, 0f);
    }

    private static Sprite First(Sprite[] sprites) => sprites != null && sprites.Length > 0 ? sprites[0] : null;

    private static MapRegionDefinition FindRegion(MapRegionCollection collection, params string[] tokens)
    {
        return collection.Regions.FirstOrDefault(region => region != null &&
            tokens.All(token => region.RegionName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0));
    }

    private static Transform FindRoot(Scene scene, string name)
    {
        return scene.GetRootGameObjects().Select(root => root.transform)
            .FirstOrDefault(root => root.name == name);
    }

    private static Transform FindTransform(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindTransform(root.transform, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static Transform FindTransform(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindTransform(parent.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T found = root.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }
        return null;
    }

}
#endif
