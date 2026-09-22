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

        /// <summary>
        /// Post cells of either trellis, repaired or not. These stay hoed for the
        /// whole game and must never be reset back to ordinary ground.
        /// </summary>
        public static bool IsPermanentTilledCell(Vector3Int cell)
        {
            SpecialCropAreaController area = FindArea(cell);
            return area != null && area.IsPlantingSlot(cell);
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
        private GameObject brokenRoot;
        private GameObject lockRoot;
        private GameObject authoredBrokenVisual;
        private readonly List<GameObject> authoredRepairedVisuals = new List<GameObject>(8);
        private bool useAuthoredRepairedVisuals;
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
            EnsurePermanentTilledGround();
            FindAuthoredStateVisuals();
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
            if (UnityEngine.Time.unscaledTime < nextVisualRefresh)
                return;
            nextVisualRefresh = UnityEngine.Time.unscaledTime + 0.2f;
            EnsurePermanentTilledGround();
            if (!IsRepaired)
                return;

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
            // Rows are listed from the top down. Both rows must remain inside the
            // marked trellis region; the old cucumber offset placed its second row
            // one cell too low.
            int lowerRowY = minY;
            for (int row = 0; row < 2; row++)
            {
                int y = row == 0 ? maxY : lowerRowY;
                for (int column = 0; column < 4; column++)
                {
                    int x = Mathf.RoundToInt(Mathf.Lerp(minX, maxX, column / 3f));
                    slots.Add(new Vector3Int(x, y, 0));
                }
            }
        }

        private void BuildVisuals()
        {
            // The authored cucumber layout has two visual states: one broken
            // trellis before repair, and the eight aligned posts after repair.
            // Runtime posts remain as planting/collision anchors, but do not
            // duplicate the authored post art.
            repairedRoot = new GameObject("Repaired - 8 planting posts");
            repairedRoot.transform.SetParent(transform, false);
            repairedRoot.SetActive(false);

            Sprite[] stages = areaId == SpecialCropAreaId.CucumberTrellis
                ? config.cucumberStages
                : config.dragonFruitStages;
            Sprite emptyPost = !useAuthoredRepairedVisuals && stages != null && stages.Length > 0
                ? stages[0]
                : null;
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

            BuildRepairedFenceRows();
            BuildFallbackBrokenVisuals();

            BuildLockInteraction();
        }

        /// <summary>
        /// One intact trellis sprite per row of posts, shown only after repair.
        /// It is scenery behind the crops, so it uses the trellis background order.
        /// Only the cucumber area has a trellis; dragon fruit grows on bare posts.
        /// </summary>
        private void BuildRepairedFenceRows()
        {
            if (areaId != SpecialCropAreaId.CucumberTrellis)
                return;

            Sprite fence = config.cucumberRepairedFence;
            if (fence == null || slots.Count == 0)
                return;

            float cellSize = gridManager.Grid.cellSize.x;
            const int columnsPerRow = 4;
            for (int row = 0; row * columnsPerRow < slots.Count; row++)
            {
                int first = row * columnsPerRow;
                int last = Mathf.Min(first + columnsPerRow, slots.Count) - 1;
                Vector3 firstPost = gridManager.GetWorldLocation(slots[first]);
                Vector3 lastPost = gridManager.GetWorldLocation(slots[last]);

                // The two authored row positions are stored in the config so the
                // repaired trellis matches the editor reference in both Play Mode
                // and future runs. Keep the post-derived position as a fallback.
                float targetWidth = Mathf.Abs(lastPost.x - firstPost.x) + cellSize;
                float scale = fence.bounds.size.x > 0f ? targetWidth / fence.bounds.size.x : 1f;
                Vector2 authoredPosition = row == 0
                    ? config.cucumberUpperFencePosition
                    : config.cucumberLowerFencePosition;
                bool hasAuthoredPosition = authoredPosition != Vector2.zero;
                Vector3 fencePosition = hasAuthoredPosition
                    ? new Vector3(authoredPosition.x, authoredPosition.y, 0f)
                    : new Vector3((firstPost.x + lastPost.x) * 0.5f, firstPost.y, 0f);
                Vector2 authoredScale = config.cucumberFenceScale;
                Vector3 fenceScale = authoredScale.x > 0f && authoredScale.y > 0f
                    ? new Vector3(authoredScale.x, authoredScale.y, 1f)
                    : new Vector3(scale, scale, 1f);

                GameObject fenceObject = new GameObject($"Repaired Trellis Row {row + 1}");
                fenceObject.transform.SetParent(repairedRoot.transform, false);
                fenceObject.transform.position = fencePosition;
                fenceObject.transform.localScale = fenceScale;

                SpriteRenderer renderer = fenceObject.AddComponent<SpriteRenderer>();
                renderer.sprite = fence;
                renderer.color = Color.white;
                renderer.sortingLayerName = MapPropSorting.SortingLayer;
                renderer.sortingOrder = MapPropSorting.TrellisBackgroundOrder;
            }
        }

        private void FindAuthoredStateVisuals()
        {
            authoredBrokenVisual = null;
            authoredRepairedVisuals.Clear();
            useAuthoredRepairedVisuals = false;

            if (gridManager == null)
                return;

            string rootName = areaId == SpecialCropAreaId.CucumberTrellis
                ? "ruộng dưa chuột"
                : "ruộng thanh long";
            GameObject authoredRoot = FindSceneObject(gridManager.gameObject.scene, rootName);
            if (authoredRoot == null)
                return;

            PushTrellisToBackground(authoredRoot);

            if (areaId != SpecialCropAreaId.CucumberTrellis)
                return;

            foreach (Transform child in authoredRoot.transform)
            {
                string childName = child.name;
                if (childName.IndexOf("brokenfence_200", StringComparison.OrdinalIgnoreCase) >= 0)
                    authoredBrokenVisual = child.gameObject;
                else
                    authoredRepairedVisuals.Add(child.gameObject);
            }

            useAuthoredRepairedVisuals = authoredRepairedVisuals.Count > 0;
        }

        /// <summary>
        /// The authored trellis frame is a backdrop: crops planted on its posts
        /// and the player walking past must always be drawn in front of it.
        /// </summary>
        private static void PushTrellisToBackground(GameObject authoredRoot)
        {
            SpriteRenderer[] renderers = authoredRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sortingLayerName = MapPropSorting.SortingLayer;
                renderers[i].sortingOrder = MapPropSorting.TrellisBackgroundOrder;
            }
        }

        private void BuildFallbackBrokenVisuals()
        {
            if (authoredBrokenVisual != null || config == null)
                return;

            Sprite[] brokenSprites = areaId == SpecialCropAreaId.CucumberTrellis
                ? config.cucumberBrokenSprites
                : config.dragonFruitBrokenSprites;
            if (brokenSprites == null || brokenSprites.Length == 0)
                return;

            bool fillEverySlot = areaId == SpecialCropAreaId.DragonFruitTrellis;
            int pieceCount = fillEverySlot ? slots.Count : brokenSprites.Length;
            brokenRoot = new GameObject(areaId == SpecialCropAreaId.CucumberTrellis
                ? "Broken cucumber trellis"
                : "Broken dragon fruit posts - 8 columns");
            brokenRoot.transform.SetParent(transform, false);
            for (int i = 0; i < pieceCount; i++)
            {
                Sprite sprite = brokenSprites[i % brokenSprites.Length];
                if (sprite == null)
                    continue;

                Vector3Int cell = fillEverySlot
                    ? slots[i]
                    : slots[Mathf.Min(i * 2, slots.Count - 1)];
                GameObject piece = new GameObject($"Broken Post {i + 1} [{cell.x},{cell.y}]");
                piece.transform.SetParent(brokenRoot.transform, false);
                piece.transform.position = gridManager.GetWorldLocation(cell);
                CreateAnchoredSprite(piece.transform, "Visual", sprite, 0.5f);
                BoxCollider2D collider = piece.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(0.14f, 0.10f);
                collider.offset = new Vector2(0f, 0.05f);
            }
        }

        private static GameObject FindSceneObject(Scene scene, string objectName)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (string.Equals(root.name, objectName, StringComparison.OrdinalIgnoreCase))
                    return root;
            }

            return null;
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
        /// The repair prompt uses the pause menu's quit-confirmation art, built
        /// on demand. The old start-menu window is only a fallback for configs
        /// that have no dialog art assigned yet.
        /// </summary>
        private static ConfirmationWindow GetConfirmationWindow(SpecialCropRuntimeConfig runtimeConfig)
        {
            if (spawnedWindow != null)
                return spawnedWindow;
            if (runtimeConfig == null)
                return null;

            spawnedWindow = YesNoDialogFactory.Create("Repair Confirmation UI", runtimeConfig.dialogPanel,
                runtimeConfig.dialogYesButton, runtimeConfig.dialogNoButton, runtimeConfig.dialogFont);
            if (spawnedWindow != null)
                return spawnedWindow;

            Debug.LogWarning("Repair dialog art is missing; falling back to the old confirmation window.");
            if (runtimeConfig.confirmationWindowPrefab == null)
                return null;

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
            if (authoredBrokenVisual != null)
                authoredBrokenVisual.SetActive(!repaired);
            if (brokenRoot != null)
                brokenRoot.SetActive(!repaired);
            for (int i = 0; i < authoredRepairedVisuals.Count; i++)
            {
                if (authoredRepairedVisuals[i] != null)
                    authoredRepairedVisuals[i].SetActive(true);
            }
            if (repairedRoot != null)
                repairedRoot.SetActive(repaired);
            EnsurePermanentTilledGround();
            if (!repaired)
                return;

            // The repair sign is removed for good once the area is unlocked.
            if (lockRoot != null)
            {
                Destroy(lockRoot);
                lockRoot = null;
            }
        }

        private void EnsurePermanentTilledGround()
        {
            if (gridManager == null)
                return;

            // The eight trellis cells are authored farm plots. They are hoed
            // from the beginning and must never fall back to ordinary ground,
            // regardless of repair, watering, harvesting, reset, or save load.
            for (int i = 0; i < slots.Count; i++)
            {
                Vector3Int cell = slots[i];
                gridManager.EnsureDirtTile(cell);
                if (!gridManager.HasDirtHole(cell))
                    gridManager.SetDirtHoleTile(cell);
            }
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
            // Decorations inside the starting farm are valid axe targets from the
            // beginning. The expanded home orchard keeps its progression lock.
            // Excluding the starting farm from the second pass prevents the
            // overlapping home-orchard rectangle from re-locking those props.
            ClearableOrchardProp.AttachNamedRegionProps(
                initialFarm, progress, false, null, "InitialFarm");
            ClearableOrchardProp.AttachNamedRegionProps(
                homeOrchard, progress, true, initialFarm, "HomeOrchard");
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
