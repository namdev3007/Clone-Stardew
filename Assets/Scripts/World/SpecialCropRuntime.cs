using System;
using System.Collections.Generic;
using System.Linq;
using Interactions;
using Item;
using Item.Inventory;
using Plugins.Lowscope.ComponentSaveSystem.Components;
using Plugins.Lowscope.ComponentSaveSystem.Interfaces;
using UnityEngine;
using UnityEngine.SceneManagement;
using User_Interface;
using World.Objects;

namespace World
{
    // Runtime progression for the two repairable trellis farming areas.
    public enum SpecialCropAreaId
    {
        None,
        CucumberTrellis,
        DragonFruitTrellis
    }

    public sealed class SpecialCropProgressService : MonoBehaviour, ISaveable
    {
        [Serializable]
        private struct SaveData
        {
            public int version;
            public bool cucumberRepaired;
            public bool dragonFruitRepaired;
            public bool homeOrchardUnlocked;
            public List<string> clearedOrchardProps;
            public List<string> damagedOrchardPropIds;
            public List<int> damagedOrchardPropHits;
        }

        public static SpecialCropProgressService Instance { get; private set; }
        public event System.Action StateChanged;

        [SerializeField] private bool cucumberRepaired;
        [SerializeField] private bool dragonFruitRepaired;
        [SerializeField] private bool homeOrchardUnlocked;
        private readonly HashSet<string> clearedOrchardProps = new HashSet<string>();
        private readonly Dictionary<string, int> damagedOrchardProps = new Dictionary<string, int>();
        private bool registeredForSave;

        public bool CucumberRepaired => cucumberRepaired;
        public bool DragonFruitRepaired => dragonFruitRepaired;
        public bool HomeOrchardUnlocked => homeOrchardUnlocked;

        private void Awake()
        {
            Instance = this;
        }

        public void RegisterWithSaveSystem()
        {
            if (registeredForSave)
                return;
            Saveable saveable = GetComponent<Saveable>() ?? GetComponentInParent<Saveable>();
            if (saveable == null)
            {
                Debug.LogWarning("Special crop progress could not find a Saveable component.");
                return;
            }
            registeredForSave = true;
            saveable.AddSaveableComponent("SpecialCropProgress", this, true);
        }

        public bool IsRepaired(SpecialCropAreaId area)
        {
            return area == SpecialCropAreaId.CucumberTrellis
                ? cucumberRepaired
                : area == SpecialCropAreaId.DragonFruitTrellis && dragonFruitRepaired;
        }

        public bool CanRepair(SpecialCropAreaId area)
        {
            if (IsRepaired(area))
                return false;
            return area == SpecialCropAreaId.CucumberTrellis || cucumberRepaired;
        }

        public void MarkRepaired(SpecialCropAreaId area)
        {
            if (area == SpecialCropAreaId.CucumberTrellis)
                cucumberRepaired = true;
            else if (area == SpecialCropAreaId.DragonFruitTrellis && cucumberRepaired)
                dragonFruitRepaired = true;
            StateChanged?.Invoke();
        }

        public void UnlockHomeOrchard()
        {
            if (homeOrchardUnlocked)
                return;
            homeOrchardUnlocked = true;
            StateChanged?.Invoke();
        }

        public bool IsOrchardPropCleared(string propId) => !string.IsNullOrEmpty(propId) &&
                                                           clearedOrchardProps.Contains(propId);

        public int GetOrchardPropHits(string propId)
        {
            return !string.IsNullOrEmpty(propId) && damagedOrchardProps.TryGetValue(propId, out int hits)
                ? hits
                : 0;
        }

        public bool RecordOrchardPropHit(string propId, int requiredHits)
        {
            if (string.IsNullOrEmpty(propId) || IsOrchardPropCleared(propId))
                return false;
            int hits = GetOrchardPropHits(propId) + 1;
            if (hits >= Mathf.Max(1, requiredHits))
            {
                damagedOrchardProps.Remove(propId);
                clearedOrchardProps.Add(propId);
            }
            else
            {
                damagedOrchardProps[propId] = hits;
            }
            StateChanged?.Invoke();
            return true;
        }

        public string OnSave()
        {
            return JsonUtility.ToJson(new SaveData
            {
                version = 1,
                cucumberRepaired = cucumberRepaired,
                dragonFruitRepaired = dragonFruitRepaired,
                homeOrchardUnlocked = homeOrchardUnlocked,
                clearedOrchardProps = new List<string>(clearedOrchardProps),
                damagedOrchardPropIds = new List<string>(damagedOrchardProps.Keys),
                damagedOrchardPropHits = new List<int>(damagedOrchardProps.Values)
            });
        }

        public void OnLoad(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                cucumberRepaired = false;
                dragonFruitRepaired = false;
                homeOrchardUnlocked = false;
                clearedOrchardProps.Clear();
                damagedOrchardProps.Clear();
                StateChanged?.Invoke();
                return;
            }
            SaveData loaded = JsonUtility.FromJson<SaveData>(data);
            cucumberRepaired = loaded.cucumberRepaired;
            dragonFruitRepaired = loaded.dragonFruitRepaired && cucumberRepaired;
            homeOrchardUnlocked = loaded.homeOrchardUnlocked && dragonFruitRepaired;
            clearedOrchardProps.Clear();
            damagedOrchardProps.Clear();
            if (loaded.clearedOrchardProps != null)
                for (int i = 0; i < loaded.clearedOrchardProps.Count; i++)
                    if (!string.IsNullOrEmpty(loaded.clearedOrchardProps[i]))
                        clearedOrchardProps.Add(loaded.clearedOrchardProps[i]);
            if (loaded.damagedOrchardPropIds != null && loaded.damagedOrchardPropHits != null)
                for (int i = 0; i < Mathf.Min(loaded.damagedOrchardPropIds.Count, loaded.damagedOrchardPropHits.Count); i++)
                    if (!string.IsNullOrEmpty(loaded.damagedOrchardPropIds[i]))
                        damagedOrchardProps[loaded.damagedOrchardPropIds[i]] = loaded.damagedOrchardPropHits[i];
            StateChanged?.Invoke();
        }

        public bool OnSaveCondition() => true;
    }

    public static class SpecialCropRuntime
    {
        private static readonly List<SpecialCropAreaController> Areas = new List<SpecialCropAreaController>();

        public static void ResetAreas() => Areas.Clear();

        public static void Register(SpecialCropAreaController area)
        {
            if (area != null && !Areas.Contains(area))
                Areas.Add(area);
        }

        public static bool CanHoe(Vector3Int cell)
        {
            // Trellis areas are never hoed: seeds go straight onto the posts.
            SpecialCropAreaController area = FindArea(cell);
            return area == null && FarmExpansionRuntime.CanHoe(cell);
        }

        /// <summary>True for one of the eight post cells of a repaired trellis area.</summary>
        public static bool IsRepairedPlantingSlot(Vector3Int cell)
        {
            SpecialCropAreaController area = FindArea(cell);
            return area != null && area.IsRepaired && area.IsPlantingSlot(cell);
        }

        public static bool CanPlant(Vector3Int cell, CropDefinition definition)
        {
            if (definition == null)
                return true;

            SpecialCropAreaController area = FindArea(cell);
            if (area == null)
                return definition.PlantingZone == PlantingZone.Normal &&
                       FarmExpansionRuntime.CanPlant(cell, definition);

            return area.IsRepaired && area.IsPlantingSlot(cell) && area.PlantingZone == definition.PlantingZone;
        }

        private static SpecialCropAreaController FindArea(Vector3Int cell)
        {
            for (int i = 0; i < Areas.Count; i++)
            {
                if (Areas[i] != null && Areas[i].Contains(cell))
                    return Areas[i];
            }
            return null;
        }
    }

    public sealed class SpecialCropAreaController : MonoBehaviour
    {
        private const int RepairCost = 30;
        private readonly List<Vector3Int> slots = new List<Vector3Int>(8);
        private readonly List<SpriteRenderer> emptyPostRenderers = new List<SpriteRenderer>(8);

        private SpecialCropAreaId areaId;
        private PlantingZone plantingZone;
        private MapRegionDefinition region;
        private MapRegionDefinition lockRegion;
        private GridManager gridManager;
        private SpecialCropProgressService progress;
        private SpecialCropRuntimeConfig config;
        private GameObject repairedRoot;
        private GameObject lockRoot;
        private static ConfirmationWindow spawnedWindow;
        private float nextVisualRefresh;

        public bool IsRepaired => progress != null && progress.IsRepaired(areaId);
        public PlantingZone PlantingZone => plantingZone;

        public void Configure(
            SpecialCropAreaId id,
            PlantingZone zone,
            MapRegionDefinition regionDefinition,
            MapRegionDefinition lockRegionDefinition,
            GridManager grid,
            SpecialCropProgressService progressService,
            SpecialCropRuntimeConfig runtimeConfig)
        {
            areaId = id;
            plantingZone = zone;
            region = regionDefinition;
            lockRegion = lockRegionDefinition;
            gridManager = grid;
            progress = progressService;
            config = runtimeConfig;
            BuildEightSlots();
            BuildVisuals();
            progress.StateChanged += RefreshState;
            SpecialCropRuntime.Register(this);
            RefreshState();
        }

        private void OnDestroy()
        {
            if (progress != null)
                progress.StateChanged -= RefreshState;
        }

        private void Update()
        {
            if (!IsRepaired || UnityEngine.Time.unscaledTime < nextVisualRefresh)
                return;
            nextVisualRefresh = UnityEngine.Time.unscaledTime + 0.2f;
            EnsureRepairedGround();
            for (int i = 0; i < slots.Count; i++)
            {
                if (emptyPostRenderers[i] != null)
                    emptyPostRenderers[i].enabled = gridManager.GetCrop(slots[i]) == null;
            }
        }

        public bool Contains(Vector3Int cell)
        {
            return region != null && region.Contains(cell);
        }

        public bool IsPlantingSlot(Vector3Int cell) => slots.Contains(cell);

        private void BuildEightSlots()
        {
            slots.Clear();
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
        }

        private void BuildVisuals()
        {
            // Locked areas are plain soil with only the repair sign. The posts
            // (stage 0 sprite) exist only after the area has been unlocked.
            repairedRoot = new GameObject("Repaired - 8 planting posts");
            repairedRoot.transform.SetParent(transform, false);
            repairedRoot.SetActive(false);

            Sprite[] stages = areaId == SpecialCropAreaId.CucumberTrellis
                ? config.cucumberStages
                : config.dragonFruitStages;
            Sprite emptyPost = stages != null && stages.Length > 0 ? stages[0] : null;
            for (int i = 0; i < slots.Count; i++)
            {
                GameObject post = new GameObject($"Planting Post {i + 1} [{slots[i].x},{slots[i].y}]");
                post.transform.SetParent(repairedRoot.transform, false);
                post.transform.position = gridManager.GetWorldLocation(slots[i]);
                SpriteRenderer renderer = CreateAnchoredSprite(post.transform, "Empty Post Visual", emptyPost, 0.5f);
                emptyPostRenderers.Add(renderer);

                BoxCollider2D collider = post.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.055f, 0.07f);
                collider.offset = new Vector2(0f, 0.035f);
            }

            BuildLockInteraction();
        }

        private void BuildLockInteraction()
        {
            Vector3Int bottomCenter = lockRegion != null
                ? lockRegion.MinCell
                : new Vector3Int(
                    Mathf.RoundToInt((region.MinCell.x + region.MaxCell.x) * 0.5f),
                    region.MinCell.y,
                    0);
            lockRoot = new GameObject("Repair Lock Sign");
            lockRoot.transform.SetParent(transform, false);
            lockRoot.transform.position = gridManager.GetWorldLocation(bottomCenter);
            // Keep the runtime sign at the same readable size as the Edit Mode preview.
            SpriteRenderer lockRenderer = CreateAnchoredSprite(lockRoot.transform, "Visual", config.lockSign, 1f);
            if (config.lockSign == null)
                Debug.LogError("Special crop lock sign is missing. Assign Assets/Sprites/props-items/ban-khoa.png to Special Crop Runtime Config.");
            else
            {
                lockRenderer.enabled = true;
                lockRenderer.color = Color.white;
            }
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer >= 0)
                lockRoot.layer = interactableLayer;
            // The player's aim point is 0.1 units ahead of their feet, so the
            // trigger covers the sign cell plus the drawn sign above it.
            BoxCollider2D trigger = lockRoot.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.24f, 0.42f);
            trigger.offset = new Vector2(0f, 0.12f);
            InteractionField field = lockRoot.AddComponent<InteractionField>();
            field.Configure(0.7f, config.mouseInteractionEvent, TryRepair);

            // Solid collider on the sign post so the player cannot walk through it.
            // It lives on a Default-layer child so it stays separate from the trigger.
            GameObject blocker = new GameObject("Sign Collider");
            blocker.transform.SetParent(lockRoot.transform, false);
            BoxCollider2D solid = blocker.AddComponent<BoxCollider2D>();
            solid.size = LockSignColliderSize;
            solid.offset = LockSignColliderOffset;
        }

        /// <summary>Footprint of the 23x32 px repair sign post, shared with the Edit Mode preview.</summary>
        public static readonly Vector2 LockSignColliderSize = new Vector2(0.14f, 0.06f);
        public static readonly Vector2 LockSignColliderOffset = new Vector2(0f, 0.03f);

        private static SpriteRenderer CreateAnchoredSprite(Transform parent, string name, Sprite sprite, float scale)
        {
            GameObject visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingLayerName = "Dynamic";
            renderer.sortingOrder = Mathf.RoundToInt(-parent.position.y * 100f);
            if (sprite != null)
            {
                float x = ((sprite.pivot.x - sprite.rect.width * 0.5f) / sprite.pixelsPerUnit) * scale;
                float y = (sprite.pivot.y / sprite.pixelsPerUnit) * scale;
                visual.transform.localPosition = new Vector3(x, y, 0f);
            }
            visual.transform.localScale = Vector3.one * scale;
            return renderer;
        }

        private void TryRepair()
        {
            if (IsRepaired)
                return;
            if (!progress.CanRepair(areaId))
            {
                ShowMessage("Repair the cucumber trellis first.");
                return;
            }

            Inventory inventory = FindPlayerInventory();
            if (inventory == null || config.currencyItem == null || inventory.GetItemAmount(config.currencyItem) < RepairCost)
            {
                ShowMessage("You need 30Đ to repair this area.");
                return;
            }

            ConfirmationWindow window = GetConfirmationWindow(config);
            if (window == null)
                return;
            string question = areaId == SpecialCropAreaId.CucumberTrellis
                ? "Repair the cucumber trellis for 30Đ?"
                : "Repair the dragon fruit field for 30Đ?";
            window.Configure(new ConfirmationWindow.Configuration
            {
                acceptOnly = false,
                question = question,
                answerYes = "YES",
                answerNo = "NO",
                actionYes = CompleteRepair
            });
        }

        private void CompleteRepair()
        {
            Inventory inventory = FindPlayerInventory();
            if (inventory == null || config.currencyItem == null ||
                !progress.CanRepair(areaId) || !inventory.TryRemoveItemAmount(config.currencyItem, RepairCost))
                return;
            progress.MarkRepaired(areaId);
        }

        private static Inventory FindPlayerInventory()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.GetComponent<Inventory>() : null;
        }

        /// <summary>
        /// Gameplay scenes have no confirmation window of their own (it only
        /// exists in the start menu), so spawn one from the config on demand.
        /// </summary>
        private static ConfirmationWindow GetConfirmationWindow(SpecialCropRuntimeConfig runtimeConfig)
        {
            if (spawnedWindow != null)
                return spawnedWindow;
            spawnedWindow = FindFirstObjectByType<ConfirmationWindow>(FindObjectsInactive.Include);
            if (spawnedWindow != null || runtimeConfig == null || runtimeConfig.confirmationWindowPrefab == null)
                return spawnedWindow;

            GameObject instance = Instantiate(runtimeConfig.confirmationWindowPrefab);
            instance.SetActive(false);
            spawnedWindow = instance.GetComponentInChildren<ConfirmationWindow>(true);
            if (spawnedWindow == null)
                Debug.LogError("Confirmation Window prefab has no ConfirmationWindow component.");
            return spawnedWindow;
        }

        private void ShowMessage(string message)
        {
            ConfirmationWindow window = GetConfirmationWindow(config);
            if (window == null)
            {
                Debug.Log(message);
                return;
            }
            window.Configure(new ConfirmationWindow.Configuration
            {
                acceptOnly = true,
                question = message,
                answerYes = "OK"
            });
        }

        private void RefreshState()
        {
            bool repaired = IsRepaired;
            if (repairedRoot != null)
                repairedRoot.SetActive(repaired);
            if (!repaired)
                return;

            // The repair sign is removed for good once the area is unlocked.
            if (lockRoot != null)
            {
                Destroy(lockRoot);
                lockRoot = null;
            }
            EnsureRepairedGround();
        }

        private void EnsureRepairedGround()
        {
            if (gridManager == null || region == null)
                return;

            // Unlocking replaces the whole marked rectangle with ordinary
            // soil. The area is never hoed; seeds are planted straight onto the
            // eight post cells (see CanPlant and GridManager.TryBeginPlanting).
            for (int y = region.MinCell.y; y <= region.MaxCell.y; y++)
            for (int x = region.MinCell.x; x <= region.MaxCell.x; x++)
                gridManager.EnsureDirtTile(new Vector3Int(x, y, 0));
        }
    }

    public sealed class WaterRefillSource : MonoBehaviour
    {
        [SerializeField] private Vector3Int[] refillCells = Array.Empty<Vector3Int>();
        private GridManager gridManager;

        public void Configure(GridManager grid, IEnumerable<Vector3Int> cells)
        {
            gridManager = grid;
            refillCells = cells != null ? new List<Vector3Int>(cells).ToArray() : Array.Empty<Vector3Int>();
            Register();
        }

        private void OnEnable() => Register();
        private void OnDisable() => Unregister();

        private void Register()
        {
            if (gridManager == null)
                gridManager = FindFirstObjectByType<GridManager>();
            if (gridManager == null)
                return;
            for (int i = 0; i < refillCells.Length; i++)
                gridManager.RegisterWaterRefillCell(refillCells[i]);
        }

        private void Unregister()
        {
            if (gridManager == null)
                return;
            for (int i = 0; i < refillCells.Length; i++)
                gridManager.UnregisterWaterRefillCell(refillCells[i]);
        }
    }

    public static class SpecialCropRuntimeBootstrap
    {
        private const string ConfigResourcePath = "Farming/Special Crop Runtime Config";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadListener()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Core and Level_Farm are loaded separately. The first callback can
            // happen before GridManager exists, so retry for every additive scene.
            Setup();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Setup()
        {
            GridManager grid = UnityEngine.Object.FindFirstObjectByType<GridManager>();
            if (grid == null || GameObject.Find("Special Crop Areas Runtime") != null)
                return;

            SpecialCropRuntimeConfig config = Resources.Load<SpecialCropRuntimeConfig>(ConfigResourcePath);
            if (config == null || config.regions == null)
            {
                Debug.LogError("Missing Special Crop Runtime Config in Resources/Farming.");
                return;
            }

            SpecialCropProgressService progress = grid.GetComponent<SpecialCropProgressService>();
            if (progress == null)
                progress = grid.gameObject.AddComponent<SpecialCropProgressService>();
            progress.RegisterWithSaveSystem();

            MapRegionDefinition cucumber = FindRegion(config.regions, "v\u00f9ng tr\u1ed3ng c\u00e2y", "d\u01b0a chu\u1ed9t");
            MapRegionDefinition dragon = FindRegion(config.regions, "\u0111\u1ea5t tr\u1ed3ng thanh long");
            MapRegionDefinition cucumberLock = FindRegion(config.regions, "c\u1eafm bi\u1ec3n", "d\u01b0a chu\u1ed9t");
            MapRegionDefinition dragonLock = FindRegion(config.regions, "c\u1eafm bi\u1ec3n", "thanh long");
            MapRegionDefinition initialFarm = FindRegion(config.regions, "b\u00ecnh th\u01b0\u1eddng");
            MapRegionDefinition homeOrchard = FindRegion(config.regions, "sau khi m\u1edf ru\u1ed9ng thanh long");
            if (cucumber == null || dragon == null)
            {
                Debug.LogError("Named cucumber/dragon fruit regions were not found in Map Region Collection.");
                return;
            }

            DisableLegacyRootObjects(grid.gameObject.scene);

            // Only hide the authored Edit Mode copy after the runtime areas are
            // known to be buildable. Otherwise a failed bootstrap would remove
            // the only visible repair signs.
            GameObject editorPreview = GameObject.Find("Special Crop Areas (Editor Preview)");
            if (editorPreview != null)
                editorPreview.SetActive(false);

            SpecialCropRuntime.ResetAreas();
            FarmExpansionRuntime.Configure(grid, progress, initialFarm, homeOrchard);
            ClearableOrchardProp.AttachNamedRegionProps(homeOrchard, progress);
            GameObject root = new GameObject("Special Crop Areas Runtime");
            SceneManager.MoveGameObjectToScene(root, grid.gameObject.scene);
            CreateArea(root.transform, "Cucumber Trellis", SpecialCropAreaId.CucumberTrellis,
                PlantingZone.CucumberTrellis, cucumber, cucumberLock, grid, progress, config);
            CreateArea(root.transform, "Dragon Fruit Field", SpecialCropAreaId.DragonFruitTrellis,
                PlantingZone.DragonFruitTrellis, dragon, dragonLock, grid, progress, config);
            CreateNamedWellSources(root.transform, config.regions, grid);
        }

        private static void DisableLegacyRootObjects(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                string objectName = root.name;
                if (objectName.StartsWith("Cucumber Planting Post ", StringComparison.Ordinal) ||
                    objectName.StartsWith("Dragon Fruit Planting Post ", StringComparison.Ordinal) ||
                    objectName == "Cucumber Repair Lock" ||
                    objectName == "Dragon Fruit Repair Lock")
                {
                    root.SetActive(false);
                }
            }
        }

        private static MapRegionDefinition FindRegion(MapRegionCollection collection, params string[] tokens)
        {
            foreach (MapRegionDefinition region in collection.Regions)
            {
                if (region != null && tokens.All(token =>
                        region.RegionName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0))
                    return region;
            }
            return null;
        }

        private static void CreateArea(
            Transform parent,
            string name,
            SpecialCropAreaId id,
            PlantingZone zone,
            MapRegionDefinition region,
            MapRegionDefinition lockRegion,
            GridManager grid,
            SpecialCropProgressService progress,
            SpecialCropRuntimeConfig config)
        {
            GameObject areaObject = new GameObject(name);
            areaObject.transform.SetParent(parent, false);
            SpecialCropAreaController controller = areaObject.AddComponent<SpecialCropAreaController>();
            controller.Configure(id, zone, region, lockRegion, grid, progress, config);
        }

        private static void CreateNamedWellSources(Transform parent, MapRegionCollection collection, GridManager grid)
        {
            foreach (MapRegionDefinition region in collection.Regions)
            {
                if (region == null)
                    continue;
                string name = region.RegionName.ToLowerInvariant();
                if (!name.Contains("well") && !name.Contains("gi\u1ebfng"))
                    continue;

                List<Vector3Int> cells = new List<Vector3Int>();
                for (int y = region.MinCell.y; y <= region.MaxCell.y; y++)
                for (int x = region.MinCell.x; x <= region.MaxCell.x; x++)
                    cells.Add(new Vector3Int(x, y, 0));

                GameObject sourceObject = new GameObject("Water Refill - " + region.RegionName);
                sourceObject.transform.SetParent(parent, false);
                sourceObject.AddComponent<WaterRefillSource>().Configure(grid, cells);
            }
        }
    }
}
