using Audio;
using Combat;
using Combat.Interfaces;
using Item;
using Plugins.Lowscope.ComponentSaveSystem.Interfaces;
using Referencing.Scriptable_Assets;
using Referencing.Scriptable_Reference;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;
using World.Interfaces;
using World.NPC;

namespace World.Objects
{
    public class Crop : MonoBehaviour, IKillable, ISaveable, ITilemapListener
    {
        public enum GrowthPhase
        {
            WaitingForFirstWater,
            Growing,
            ReadyToHarvest,
            RestingAfterHarvest,
            WaitingForRegrowthWater,
            Regrowing
        }

        [System.Serializable]
        public struct SaveData
        {
            public int version;
            public string definitionGuid;
            public int phase;
            public float remainingSeconds;
            public bool fertilized;
            public bool wateredThisCycle;
            public int harvestCount;
            public int axeHitCount;
            public float waterWaitTimer;
        }

        private const float MissingFertilizerPenaltySeconds = 15f;
        private const float MissingWaterPenaltySeconds = 6f;
        private const float WaterWaitTimeoutSeconds = 30f;
        private const float RegrowthRestSeconds = 10f;
        private const string DepthSortingLayer = "Dynamic";
        private const float DepthSortingScale = -100f;

        [Header("References")]
        [SerializeField] private ScriptableReference gridManagerReference;
        [SerializeField] private Health health;
        [SerializeField] private ItemDropper itemDropper;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Ground")]
        [SerializeField] private string groundWetTilemapName = "Dirt Hole Wet";

        private GridManager gridManager;
        private CropDefinition definition;
        private GrowthPhase phase = GrowthPhase.WaitingForFirstWater;
        private float remainingSeconds;
        private float activeDuration;
        private bool fertilized;
        private bool wateredThisCycle;
        private float waterWaitTimer;
        private int harvestCount;
        private int axeHitCount;
        private bool registered;
        private bool showcaseMode;
        private int showcaseStageIndex;
        private SortingGroup depthSortingGroup;
        private float lastDepthSortY = float.NaN;

        public bool NeedsWater => !wateredThisCycle && (phase == GrowthPhase.WaitingForFirstWater || phase == GrowthPhase.WaitingForRegrowthWater);
        public bool IsReadyToHarvest => phase == GrowthPhase.ReadyToHarvest;
        public bool UsesPerennialFootprint => definition != null && definition.IsPerennialTree;

        public bool TryGetStatusAnchor(out Vector3 anchor)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null || !spriteRenderer.enabled)
            {
                anchor = transform.position;
                return false;
            }

            Bounds visualBounds = spriteRenderer.bounds;
            anchor = new Vector3(visualBounds.center.x, visualBounds.max.y, transform.position.z);
            return true;
        }

        private void Awake()
        {
            gridManager = gridManagerReference.Reference?.GetComponent<GridManager>();
            health?.AddListener((IKillable)this);
            health?.SetInvulnerable(true);
            SetupDepthSorting();
            EnsureTreeSolidCollider();
            UpdateTreeCollider(spriteRenderer != null ? spriteRenderer.sprite : null);
        }

        private void OnEnable()
        {
            FarmingCheats.OnGodModeChanged += HandleGodModeChanged;
        }

        private void OnDisable()
        {
            FarmingCheats.OnGodModeChanged -= HandleGodModeChanged;
            if (treeSolidCollider != null)
                treeSolidCollider.enabled = false;
        }

        /// <summary>
        /// Crops sort by the Y of their base on the same layer as the player and
        /// decoration trees: the player is drawn over a crop when standing in
        /// front of it and behind it when standing further up.
        /// </summary>
        private void SetupDepthSorting()
        {
            if (spriteRenderer == null)
                return;

            if (depthSortingGroup == null)
            {
                depthSortingGroup = spriteRenderer.GetComponent<SortingGroup>();
                if (depthSortingGroup == null)
                    depthSortingGroup = GetComponent<SortingGroup>();
                if (depthSortingGroup == null)
                    depthSortingGroup = GetComponentInChildren<SortingGroup>(true);
            }
            // The prefab's HeightBasedSorting sits on the sprite child, which is
            // raised by each frame's pivot offset; sort from the crop root instead.
            HeightBasedSorting childSorting = spriteRenderer.GetComponent<HeightBasedSorting>();
            if (childSorting != null)
                childSorting.enabled = false;

            if (depthSortingGroup != null)
                depthSortingGroup.sortingLayerName = DepthSortingLayer;
            spriteRenderer.sortingLayerName = DepthSortingLayer;
            UpdateDepthSorting();
        }

        private void LateUpdate()
        {
            if (!Mathf.Approximately(lastDepthSortY, transform.position.y))
                UpdateDepthSorting();
        }

        private void UpdateDepthSorting()
        {
            lastDepthSortY = transform.position.y;
            int order = Mathf.RoundToInt(lastDepthSortY * DepthSortingScale);
            if (definition != null && definition.PlantingZone == PlantingZone.DragonFruitTrellis)
            {
                order = SpecialCropAreaController.GetDragonFruitSortingOrder(lastDepthSortY);
            }

            if (depthSortingGroup != null)
                depthSortingGroup.sortingOrder = order;
            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = order;
        }

        private void Start()
        {
            RegisterWithGrid();
            if (showcaseMode)
            {
                if (phase == GrowthPhase.ReadyToHarvest)
                    RefreshVisuals();
                else
                    RefreshShowcaseVisual();
            }
            else
                RefreshVisuals();
        }

        private void Update()
        {
            if (definition == null || showcaseMode)
                return;

            if (FarmingCheats.GodMode)
            {
                if (phase == GrowthPhase.WaitingForFirstWater)
                {
                    StartFirstGrowth(watered: true);
                    return;
                }
                if (phase == GrowthPhase.WaitingForRegrowthWater)
                {
                    StartRegrowth(watered: true);
                    return;
                }
                if (phase == GrowthPhase.RestingAfterHarvest)
                {
                    remainingSeconds = 0f;
                    BeginRegrowthCycle();
                    return;
                }

                if (phase == GrowthPhase.Growing || phase == GrowthPhase.Regrowing)
                {
                    int totalSprites = definition.GrowthSprites != null ? definition.GrowthSprites.Length : 1;
                    int stageCount = phase == GrowthPhase.Regrowing
                        ? Mathf.Max(1, (totalSprites - definition.RegrowthStageStart) - 1)
                        : Mathf.Max(1, totalSprites - 1);
                    float targetDuration = stageCount * FarmingCheats.GodModeStageSeconds;
                    if (activeDuration > targetDuration)
                    {
                        float progress = activeDuration > 0f ? (1f - Mathf.Clamp01(remainingSeconds / activeDuration)) : 0f;
                        activeDuration = targetDuration;
                        remainingSeconds = (1f - progress) * targetDuration;
                    }
                }
            }

            switch (phase)
            {
                case GrowthPhase.WaitingForFirstWater:
                    if (definition.IsPerennialTree)
                    {
                        waterWaitTimer = Mathf.Max(0f, waterWaitTimer - UnityEngine.Time.deltaTime);
                        if (waterWaitTimer <= 0f)
                        {
                            StartFirstGrowth(watered: false);
                        }
                    }
                    break;

                case GrowthPhase.WaitingForRegrowthWater:
                    if (definition.IsPerennialTree)
                    {
                        waterWaitTimer = Mathf.Max(0f, waterWaitTimer - UnityEngine.Time.deltaTime);
                        if (waterWaitTimer <= 0f)
                        {
                            StartRegrowth(watered: false);
                        }
                    }
                    break;

                case GrowthPhase.Growing:
                case GrowthPhase.Regrowing:
                    remainingSeconds = Mathf.Max(0f, remainingSeconds - UnityEngine.Time.deltaTime);
                    UpdateGrowthSprite();
                    if (remainingSeconds <= 0f)
                        BecomeHarvestable();
                    break;

                case GrowthPhase.RestingAfterHarvest:
                    remainingSeconds = Mathf.Max(0f, remainingSeconds - UnityEngine.Time.deltaTime);
                    if (remainingSeconds <= 0f)
                    {
                        BeginRegrowthCycle();
                    }
                    break;
            }
        }

        private void OnDestroy()
        {
            if (gridManager != null)
            {
                gridManager.UnSubscribeToGridChanges(this, transform.position);
                gridManager.UnregisterCrop(this, transform.position);
            }
        }

        private bool IsGroundWet()
        {
            if (gridManager == null && gridManagerReference != null)
                gridManager = gridManagerReference.Reference?.GetComponent<GridManager>();

            if (gridManager == null)
                return false;

            Vector3Int gridLoc = gridManager.GetGridLocation(transform.position);
            return gridManager.HasWateredDirt(gridLoc) || gridManager.ContainsMapAtLocation(groundWetTilemapName, transform.position);
        }

        public void Configure(CropDefinition cropDefinition, bool wasFertilized)
        {
            showcaseMode = false;
            definition = cropDefinition;
            fertilized = wasFertilized;
            harvestCount = 0;
            axeHitCount = 0;
            health?.Revive();
            health?.SetInvulnerable(true);
            ConfigureDrop();
            RegisterWithGrid();
            UpdateDepthSorting();

            if (IsGroundWet() || FarmingCheats.GodMode)
            {
                wateredThisCycle = true;
                if (fertilized || FarmingCheats.GodMode)
                    StartFirstGrowth(watered: true);
                else
                    WaitForFirstGrowthRequirements();
            }
            else
            {
                WaitForFirstGrowthRequirements();
            }
        }

        private void WaitForFirstGrowthRequirements()
        {
            phase = GrowthPhase.WaitingForFirstWater;
            waterWaitTimer = definition != null && definition.IsPerennialTree ? WaterWaitTimeoutSeconds : 0f;
            activeDuration = definition != null ? definition.FirstGrowthSeconds : 0f;
            remainingSeconds = activeDuration;
            RefreshVisuals();
        }

        public void ConfigureShowcase(CropDefinition cropDefinition, int stageIndex, bool harvestableDemo)
        {
            definition = cropDefinition;
            showcaseMode = true;
            showcaseStageIndex = Mathf.Clamp(stageIndex, 0, Mathf.Max(0, cropDefinition.GrowthSprites.Length - 1));
            fertilized = true;
            wateredThisCycle = true;
            health?.Revive();
            health?.SetInvulnerable(true);
            ConfigureDrop();
            RegisterWithGrid();
            UpdateDepthSorting();

            if (harvestableDemo)
            {
                phase = GrowthPhase.ReadyToHarvest;
                BecomeHarvestable();
            }
            else
            {
                phase = GrowthPhase.Growing;
                RefreshShowcaseVisual();
            }
        }

        private void RefreshShowcaseVisual()
        {
            if (definition == null || definition.GrowthSprites == null || definition.GrowthSprites.Length == 0)
                return;

            SetSprite(definition.GrowthSprites[showcaseStageIndex]);
            gridManager?.SetCropStatus(transform.position, FarmPlotStatus.None);
        }

        public bool TryWater()
        {
            if (definition == null || phase != GrowthPhase.WaitingForFirstWater && phase != GrowthPhase.WaitingForRegrowthWater)
                return false;

            wateredThisCycle = true;
            if (phase == GrowthPhase.WaitingForFirstWater)
            {
                // The first cycle needs both fertilizer and water. Remember the
                // water if it is applied first, then fertilizer can start growth.
                if (fertilized)
                    StartFirstGrowth(watered: true);
                else
                    RefreshVisuals();
                return true;
            }

            StartRegrowth(watered: true);
            return true;
        }

        public bool TryFertilize()
        {
            if (definition == null || fertilized || harvestCount > 0 ||
                phase != GrowthPhase.WaitingForFirstWater)
                return false;

            fertilized = true;
            if (wateredThisCycle || IsGroundWet())
                StartFirstGrowth(watered: true);
            else
                RefreshVisuals();
            return true;
        }

        private void StartFirstGrowth(bool watered)
        {
            if (definition == null)
                return;

            if (!fertilized && !FarmingCheats.GodMode)
            {
                WaitForFirstGrowthRequirements();
                return;
            }

            phase = GrowthPhase.Growing;
            wateredThisCycle = watered;

            if (FarmingCheats.GodMode)
            {
                int stageCount = Mathf.Max(1, (definition.GrowthSprites != null ? definition.GrowthSprites.Length : 1) - 1);
                activeDuration = stageCount * FarmingCheats.GodModeStageSeconds;
                remainingSeconds = activeDuration;
            }
            else
            {
                float penalty = 0f;
                if (definition.IsPerennialTree && !watered)
                    penalty = MissingWaterPenaltySeconds;

                activeDuration = definition.FirstGrowthSeconds + penalty;
                remainingSeconds = activeDuration;
            }
            health?.SetInvulnerable(true);
            RefreshVisuals();
        }

        private void BeginRegrowthCycle()
        {
            if (IsGroundWet() || FarmingCheats.GodMode)
            {
                StartRegrowth(watered: true);
            }
            else
            {
                phase = GrowthPhase.WaitingForRegrowthWater;
                wateredThisCycle = false;
                waterWaitTimer = definition != null && definition.IsPerennialTree ? WaterWaitTimeoutSeconds : 0f;
                activeDuration = definition.RegrowthSeconds;
                remainingSeconds = activeDuration;
                RefreshVisuals();
            }
        }

        private void StartRegrowth(bool watered)
        {
            if (definition == null)
                return;

            phase = GrowthPhase.Regrowing;
            wateredThisCycle = watered;

            if (FarmingCheats.GodMode)
            {
                int totalSprites = definition.GrowthSprites != null ? definition.GrowthSprites.Length : 1;
                int stageCount = Mathf.Max(1, (totalSprites - definition.RegrowthStageStart) - 1);
                activeDuration = stageCount * FarmingCheats.GodModeStageSeconds;
                remainingSeconds = activeDuration;
            }
            else
            {
                float penalty = 0f;
                if (definition.IsPerennialTree && !watered)
                    penalty = MissingWaterPenaltySeconds;

                activeDuration = definition.RegrowthSeconds + penalty;
                remainingSeconds = activeDuration;
            }
            health?.SetInvulnerable(true);
            RefreshVisuals();
        }

        private void HandleGodModeChanged(bool enabled)
        {
            if (!enabled || definition == null || showcaseMode)
                return;

            if (phase == GrowthPhase.WaitingForFirstWater)
                StartFirstGrowth(watered: true);
            else if (phase == GrowthPhase.WaitingForRegrowthWater)
                StartRegrowth(watered: true);
            else if (phase == GrowthPhase.RestingAfterHarvest)
            {
                remainingSeconds = 0f;
                BeginRegrowthCycle();
            }
            else if (phase == GrowthPhase.Growing || phase == GrowthPhase.Regrowing)
            {
                int totalSprites = definition.GrowthSprites != null ? definition.GrowthSprites.Length : 1;
                int stageCount = phase == GrowthPhase.Regrowing
                    ? Mathf.Max(1, (totalSprites - definition.RegrowthStageStart) - 1)
                    : Mathf.Max(1, totalSprites - 1);
                float targetDuration = stageCount * FarmingCheats.GodModeStageSeconds;
                if (activeDuration > targetDuration)
                {
                    float progress = activeDuration > 0f ? (1f - Mathf.Clamp01(remainingSeconds / activeDuration)) : 0f;
                    activeDuration = targetDuration;
                    remainingSeconds = (1f - progress) * targetDuration;
                }
            }
        }

        private void RegisterWithGrid()
        {
            if (registered || gridManager == null)
                return;

            gridManager.SubscribeToGridChanges(this, transform.position);
            gridManager.RegisterCrop(this, transform.position);
            registered = true;
            gridManager.RefreshCropStatusPosition(transform.position);
        }

        private void ConfigureDrop()
        {
            if (definition != null && itemDropper != null)
                itemDropper.ConfigureSingleDrop(definition.HarvestedItem, definition.HarvestYield);
        }

        private void BecomeHarvestable()
        {
            phase = GrowthPhase.ReadyToHarvest;
            remainingSeconds = 0f;
            axeHitCount = 0;
            SetSprite(GetLastSprite());
            // Banana/mango trees are handled by the dedicated axe interaction;
            // generic crop attacks must never harvest or remove them.
            health?.SetInvulnerable(definition != null && definition.IsPerennialTree);
            gridManager?.SetCropStatus(transform.position, FarmPlotStatus.Harvest);
        }

        public void RefreshVisuals()
        {
            if (definition == null)
                return;

            switch (phase)
            {
                case GrowthPhase.WaitingForFirstWater:
                    SetSprite(GetGrowthSprite(0));
                    gridManager?.SetCropStatus(transform.position, FarmPlotStatus.Water);
                    break;
                case GrowthPhase.Growing:
                case GrowthPhase.Regrowing:
                    UpdateGrowthSprite();
                    gridManager?.SetCropStatus(transform.position, FarmPlotStatus.None);
                    break;
                case GrowthPhase.ReadyToHarvest:
                    SetSprite(GetLastSprite());
                    health?.SetInvulnerable(definition.IsPerennialTree);
                    gridManager?.SetCropStatus(transform.position, FarmPlotStatus.Harvest);
                    break;
                case GrowthPhase.RestingAfterHarvest:
                    SetSprite(GetRegrowthStartSprite());
                    gridManager?.SetCropStatus(transform.position, FarmPlotStatus.None);
                    break;
                case GrowthPhase.WaitingForRegrowthWater:
                    SetSprite(GetRegrowthStartSprite());
                    gridManager?.SetCropStatus(transform.position, FarmPlotStatus.Water);
                    break;
            }
        }

        private void UpdateGrowthSprite()
        {
            if (definition == null || activeDuration <= 0f)
                return;

            Sprite[] sprites = definition.GrowthSprites;
            if (sprites == null || sprites.Length == 0)
                return;

            int firstIndex = phase == GrowthPhase.Regrowing ? definition.RegrowthStageStart : 0;
            int totalCount = sprites.Length - firstIndex;
            if (totalCount <= 0)
                return;

            // The final sprite (sprites.Length - 1) is reserved for ReadyToHarvest.
            // While actively growing, distribute progress across intermediate growing sprites.
            int growingStages = totalCount - 1;
            if (growingStages <= 0)
            {
                SetSprite(sprites[firstIndex]);
                return;
            }

            float progress = 1f - Mathf.Clamp01(remainingSeconds / activeDuration);
            int offset = Mathf.Clamp(Mathf.FloorToInt(progress * growingStages), 0, growingStages - 1);
            SetSprite(sprites[firstIndex + offset]);
        }

        private Sprite GetGrowthSprite(int index)
        {
            Sprite[] sprites = definition?.GrowthSprites;
            return sprites != null && sprites.Length > 0 ? sprites[Mathf.Clamp(index, 0, sprites.Length - 1)] : null;
        }

        private Sprite GetLastSprite()
        {
            Sprite[] sprites = definition?.GrowthSprites;
            return sprites != null && sprites.Length > 0 ? sprites[sprites.Length - 1] : null;
        }

        private Sprite GetRegrowthStartSprite()
        {
            return GetGrowthSprite(definition != null ? definition.RegrowthStageStart : 0);
        }

        private void SetSprite(Sprite sprite)
        {
            if (spriteRenderer == null || sprite == null)
                return;

            UpdateDepthSorting();

            bool spriteChanged = spriteRenderer.sprite != sprite;
            spriteRenderer.sprite = sprite;
            // Crops are not part of the removed day/night lighting system. Keep
            // their source colours intact when the wet-ground tile is applied.
            spriteRenderer.color = Color.white;

            float scale = CropSpriteAlignment.CropVisualScale;
            spriteRenderer.transform.localScale = Vector3.one * scale;

            // Each growth stage carries its own anchor (see CropDefinition), so
            // stages with different pivots or padding all land in this soil cell.
            Vector2 localPosition = definition != null
                ? definition.GetStageLocalPosition(sprite, scale, GetCellHeight())
                : CropSpriteAlignment.GetCellCenteredOffset(sprite, scale, GetCellHeight());
            spriteRenderer.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);

            if (spriteChanged)
                gridManager?.RefreshCropStatusPosition(transform.position);

            UpdateTreeCollider(sprite);
        }

        private float GetCellHeight()
        {
            if (gridManager != null && gridManager.Grid != null && gridManager.Grid.cellSize.y > 0f)
                return gridManager.Grid.cellSize.y;
            return CropSpriteAlignment.DefaultCellHeight;
        }

        private BoxCollider2D treeSolidCollider;

        private void EnsureTreeSolidCollider()
        {
            if (treeSolidCollider != null)
                return;

            Transform oldChild = transform.Find("TreeSolidCollider");
            if (oldChild != null)
                Destroy(oldChild.gameObject);

            BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].isTrigger)
                {
                    treeSolidCollider = colliders[i];
                    break;
                }
            }

            if (treeSolidCollider == null)
            {
                treeSolidCollider = gameObject.AddComponent<BoxCollider2D>();
                treeSolidCollider.isTrigger = false;
                treeSolidCollider.size = new Vector2(0.18f, 0.10f);
                treeSolidCollider.offset = new Vector2(0f, 0.05f);
                treeSolidCollider.enabled = false;
            }
        }

        private void UpdateTreeCollider(Sprite currentSprite)
        {
            EnsureTreeSolidCollider();

            bool isPerennial = definition != null && definition.IsPerennialTree;
            if (!isPerennial)
            {
                if (treeSolidCollider != null)
                    treeSolidCollider.enabled = false;
                return;
            }

            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null && rb.bodyType != RigidbodyType2D.Static)
                rb.bodyType = RigidbodyType2D.Static;

            treeSolidCollider.isTrigger = false;
            treeSolidCollider.enabled = true;

            bool isSprout = currentSprite != null && currentSprite == GetGrowthSprite(0);
            if (isSprout)
            {
                treeSolidCollider.size = new Vector2(0.14f, 0.08f);
                treeSolidCollider.offset = new Vector2(0f, -0.01f);
            }
            else
            {
                // The trunk foot sits near the bottom of the root cell (see
                // GetCellCenteredOffset), so the solid base stays inside that cell.
                treeSolidCollider.size = new Vector2(0.18f, 0.10f);
                treeSolidCollider.offset = new Vector2(0f, -0.01f);
            }
        }

        public void OnDeath(Health killedHealth)
        {
            Harvest();
        }

        public bool Harvest()
        {
            if (phase != GrowthPhase.ReadyToHarvest || definition == null)
                return false;

            GameAudioService.PlayHarvest();
            itemDropper?.Drop(transform.position);
            TutorialProgressService.Instance.RecordCropHarvested(definition.DisplayName);
            harvestCount++;

            if (harvestCount >= definition.MaximumHarvests)
            {
                gridManager?.ReleaseFarmPlot(transform.position);
                if (gridManager != null)
                    gridManager.UnSubscribeToGridChanges(this, transform.position);
                registered = false;
                Destroy(gameObject);
                return true;
            }

            phase = GrowthPhase.RestingAfterHarvest;
            remainingSeconds = RegrowthRestSeconds;
            health?.SetInvulnerable(true);
            RefreshVisuals();
            StartCoroutine(ReviveAfterDamageCompletes());
            return true;
        }

        /// <summary>
        /// Handles axe interaction for planted banana/mango trees only. Ripe
        /// fruit is harvested without counting as tree damage; every other
        /// growth phase counts toward chopping the whole tree down.
        /// </summary>
        public bool TryUseAxe()
        {
            if (definition == null || !definition.IsPerennialTree || showcaseMode)
                return false;

            bool isBanana = definition.DisplayName.IndexOf("Banana", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (phase == GrowthPhase.ReadyToHarvest)
            {
                if (isBanana)
                {
                    axeHitCount++;
                    if (axeHitCount < 3)
                    {
                        StartCoroutine(AxeHitFeedback());
                        return true;
                    }
                    axeHitCount = 0;
                    return Harvest();
                }
                return Harvest();
            }

            axeHitCount++;
            int requiredHits = definition.DisplayName.IndexOf("Mango", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? 6
                : 3;
            if (axeHitCount < requiredHits)
            {
                StartCoroutine(AxeHitFeedback());
                return true;
            }

            gridManager?.ReleaseFarmPlot(transform.position);
            if (gridManager != null)
                gridManager.UnSubscribeToGridChanges(this, transform.position);
            registered = false;
            Destroy(gameObject);
            return true;
        }

        private IEnumerator AxeHitFeedback()
        {
            if (spriteRenderer == null) yield break;
            Vector3 originalLocalPos = spriteRenderer.transform.localPosition;
            spriteRenderer.transform.localPosition = originalLocalPos + new Vector3(0.02f, 0f, 0f);
            yield return new WaitForSeconds(0.05f);
            if (spriteRenderer != null)
                spriteRenderer.transform.localPosition = originalLocalPos - new Vector3(0.02f, 0f, 0f);
            yield return new WaitForSeconds(0.05f);
            if (spriteRenderer != null)
                spriteRenderer.transform.localPosition = originalLocalPos;
        }

        public void CheatGrowNow()
        {
            if (definition == null || phase == GrowthPhase.ReadyToHarvest)
                return;

            BecomeHarvestable();
        }

        public bool CheatAdvanceGrowth(float seconds)
        {
            if (definition == null || seconds <= 0f || phase == GrowthPhase.ReadyToHarvest)
                return false;

            float timeLeftToSkip = seconds;
            int transitionGuard = 0;
            while (timeLeftToSkip > 0f && transitionGuard++ < 6)
            {
                switch (phase)
                {
                    case GrowthPhase.WaitingForFirstWater:
                        if (definition.IsPerennialTree)
                        {
                            if (timeLeftToSkip < waterWaitTimer)
                            {
                                waterWaitTimer -= timeLeftToSkip;
                                timeLeftToSkip = 0f;
                            }
                            else
                            {
                                timeLeftToSkip -= waterWaitTimer;
                                waterWaitTimer = 0f;
                                StartFirstGrowth(watered: false);
                            }
                        }
                        else
                        {
                            StartFirstGrowth(watered: true);
                        }
                        break;

                    case GrowthPhase.WaitingForRegrowthWater:
                        if (definition.IsPerennialTree)
                        {
                            if (timeLeftToSkip < waterWaitTimer)
                            {
                                waterWaitTimer -= timeLeftToSkip;
                                timeLeftToSkip = 0f;
                            }
                            else
                            {
                                timeLeftToSkip -= waterWaitTimer;
                                waterWaitTimer = 0f;
                                StartRegrowth(watered: false);
                            }
                        }
                        else
                        {
                            StartRegrowth(watered: true);
                        }
                        break;

                    case GrowthPhase.Growing:
                    case GrowthPhase.Regrowing:
                        if (timeLeftToSkip >= remainingSeconds)
                        {
                            timeLeftToSkip -= remainingSeconds;
                            BecomeHarvestable();
                            return true;
                        }

                        remainingSeconds -= timeLeftToSkip;
                        timeLeftToSkip = 0f;
                        UpdateGrowthSprite();
                        break;

                    case GrowthPhase.RestingAfterHarvest:
                        if (timeLeftToSkip >= remainingSeconds)
                        {
                            timeLeftToSkip -= remainingSeconds;
                            BeginRegrowthCycle();
                        }
                        else
                        {
                            remainingSeconds -= timeLeftToSkip;
                            timeLeftToSkip = 0f;
                        }
                        break;

                    case GrowthPhase.ReadyToHarvest:
                        return true;
                }
            }

            return true;
        }

        public void CheatWater()
        {
            TryWater();
        }

        public void CheatFertilize()
        {
            fertilized = true;
        }

        private IEnumerator ReviveAfterDamageCompletes()
        {
            yield return null;
            health?.Revive();
            health?.SetInvulnerable(true);
        }

        public string OnSave()
        {
            return JsonUtility.ToJson(new SaveData
            {
                version = 6,
                definitionGuid = definition != null ? definition.GetGuid() : string.Empty,
                phase = (int)phase,
                remainingSeconds = remainingSeconds,
                fertilized = fertilized,
                wateredThisCycle = wateredThisCycle,
                harvestCount = harvestCount,
                axeHitCount = axeHitCount,
                waterWaitTimer = waterWaitTimer
            });
        }

        public void OnLoad(string data)
        {
            SaveData saveData = JsonUtility.FromJson<SaveData>(data);
            definition = ScriptableAssetDatabase.GetAsset(saveData.definitionGuid) as CropDefinition;
            phase = (GrowthPhase)saveData.phase;
            remainingSeconds = Mathf.Max(0f, saveData.remainingSeconds);
            fertilized = saveData.fertilized;
            wateredThisCycle = saveData.wateredThisCycle;
            harvestCount = Mathf.Max(0, saveData.harvestCount);
            axeHitCount = Mathf.Max(0, saveData.axeHitCount);
            waterWaitTimer = Mathf.Max(0f, saveData.waterWaitTimer);
            UpdateDepthSorting();

            if (definition != null)
            {
                if (saveData.version < 2)
                {
                    if (phase == GrowthPhase.WaitingForFirstWater)
                    {
                        StartFirstGrowth(watered: true);
                    }
                    else if (phase == GrowthPhase.WaitingForRegrowthWater)
                    {
                        StartRegrowth(watered: true);
                    }
                    else
                    {
                        // Crops that were already growing in old saves had necessarily been watered.
                        wateredThisCycle = phase == GrowthPhase.Growing || phase == GrowthPhase.Regrowing || phase == GrowthPhase.ReadyToHarvest;
                    }
                }

                if (saveData.version < 4 && phase == GrowthPhase.Growing && fertilized && !wateredThisCycle)
                {
                    // Version 3 treated every crop as fertilized and assigned the
                    // whole 21-second penalty to water. Keep its elapsed progress
                    // when splitting that penalty back into 15s fertilizer + 6s water.
                    remainingSeconds = Mathf.Max(0f, remainingSeconds - MissingFertilizerPenaltySeconds);
                }

                if (saveData.version < 6)
                {
                    if ((phase == GrowthPhase.WaitingForFirstWater || phase == GrowthPhase.WaitingForRegrowthWater) && waterWaitTimer <= 0f)
                    {
                        waterWaitTimer = WaterWaitTimeoutSeconds;
                    }
                }

                float penalty = 0f;
                if (definition.IsPerennialTree && !wateredThisCycle)
                    penalty = MissingWaterPenaltySeconds;

                activeDuration = phase == GrowthPhase.Regrowing
                    ? definition.RegrowthSeconds + penalty
                    : definition.FirstGrowthSeconds + penalty;
            }

            ConfigureDrop();
            RegisterWithGrid();
            RefreshVisuals();
        }

        public bool OnSaveCondition() => definition != null && !showcaseMode;

        public void OnAddedTile(string tilemapName)
        {
            if (tilemapName == groundWetTilemapName)
                TryWater();
        }

        public void OnRemovedTile(string tilemapName) { }
    }
}
