using Combat;
using Combat.Interfaces;
using Item;
using Plugins.Lowscope.ComponentSaveSystem.Interfaces;
using Referencing.Scriptable_Assets;
using Referencing.Scriptable_Reference;
using System.Collections;
using UnityEngine;
using World.Interfaces;

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
        }

        private const float MissingFertilizerPenaltySeconds = 15f;
        private const float FirstGrowthMissingWaterPenaltySeconds = 6f;
        private const float RegrowthMissingWaterPenaltySeconds = 21f;
        private const float RegrowthRestSeconds = 10f;
        private const float CropVisualScale = 0.5f;
        private const float CropVisualPositionY = 0f;

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
        private int harvestCount;
        private bool registered;
        private bool showcaseMode;
        private int showcaseStageIndex;

        public bool NeedsWater => !wateredThisCycle && (phase == GrowthPhase.Growing || phase == GrowthPhase.Regrowing);
        public bool IsReadyToHarvest => phase == GrowthPhase.ReadyToHarvest;
        public bool UsesPerennialFootprint => definition != null && definition.IsPerennialTree;

        private void Awake()
        {
            gridManager = gridManagerReference.Reference?.GetComponent<GridManager>();
            health?.AddListener((IKillable)this);
            health?.SetInvulnerable(true);
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

            switch (phase)
            {
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
                        StartRegrowth();
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

        public void Configure(CropDefinition cropDefinition, bool wasFertilized)
        {
            showcaseMode = false;
            definition = cropDefinition;
            fertilized = wasFertilized;
            harvestCount = 0;
            StartFirstGrowth();
            health?.Revive();
            health?.SetInvulnerable(true);
            ConfigureDrop();
            RegisterWithGrid();
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
            if (definition == null || !NeedsWater)
                return false;

            float removedPenalty = phase == GrowthPhase.Regrowing
                ? RegrowthMissingWaterPenaltySeconds
                : FirstGrowthMissingWaterPenaltySeconds;
            wateredThisCycle = true;
            activeDuration = Mathf.Max(0.1f, activeDuration - removedPenalty);
            remainingSeconds = Mathf.Max(0f, remainingSeconds - removedPenalty);
            RefreshVisuals();
            if (remainingSeconds <= 0f)
                BecomeHarvestable();
            return true;
        }

        private void StartFirstGrowth()
        {
            if (definition == null)
                return;

            phase = GrowthPhase.Growing;
            wateredThisCycle = false;
            activeDuration = definition.FirstGrowthSeconds
                + (fertilized ? 0f : MissingFertilizerPenaltySeconds)
                + FirstGrowthMissingWaterPenaltySeconds;
            remainingSeconds = activeDuration;
            health?.SetInvulnerable(true);
            RefreshVisuals();
        }

        private void StartRegrowth()
        {
            if (definition == null)
                return;

            phase = GrowthPhase.Regrowing;
            wateredThisCycle = false;
            activeDuration = definition.RegrowthSeconds + RegrowthMissingWaterPenaltySeconds;
            remainingSeconds = activeDuration;
            health?.SetInvulnerable(true);
            RefreshVisuals();
        }

        private void RegisterWithGrid()
        {
            if (registered || gridManager == null)
                return;

            gridManager.SubscribeToGridChanges(this, transform.position);
            gridManager.RegisterCrop(this, transform.position);
            registered = true;
        }

        private void ConfigureDrop()
        {
            if (definition != null && itemDropper != null)
                itemDropper.ConfigureSingleDrop(definition.HarvestedItem, 1);
        }

        private void BecomeHarvestable()
        {
            phase = GrowthPhase.ReadyToHarvest;
            remainingSeconds = 0f;
            SetSprite(GetLastSprite());
            health?.SetInvulnerable(false);
            gridManager?.SetCropStatus(transform.position, FarmPlotStatus.Harvest);
        }

        private void RefreshVisuals()
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
                    gridManager?.SetCropStatus(transform.position, wateredThisCycle ? FarmPlotStatus.None : FarmPlotStatus.Water);
                    break;
                case GrowthPhase.ReadyToHarvest:
                    SetSprite(GetLastSprite());
                    health?.SetInvulnerable(false);
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
            int usableCount = sprites.Length - firstIndex;
            float progress = 1f - Mathf.Clamp01(remainingSeconds / activeDuration);
            int offset = Mathf.Min(usableCount - 1, Mathf.FloorToInt(progress * usableCount));
            SetSprite(sprites[firstIndex + Mathf.Max(0, offset)]);
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

            spriteRenderer.sprite = sprite;

            // Imported crop frames are tightly trimmed and have different sizes.
            // Offset each frame so its bottom-center remains fixed to the soil cell.
            float pixelsPerUnit = sprite.pixelsPerUnit;
            Vector2 pivot = sprite.pivot;
            float scale = CropVisualScale;

            spriteRenderer.transform.localScale = Vector3.one * scale;
            float x = ((pivot.x - sprite.rect.width * 0.5f) / pixelsPerUnit) * scale;
            float y = (pivot.y / pixelsPerUnit) * scale;
            Vector2 stageOffset = definition != null ? definition.GetStagePositionOffset(sprite) : Vector2.zero;
            spriteRenderer.transform.localPosition = new Vector3(
                x + stageOffset.x,
                CropVisualPositionY + y + stageOffset.y,
                0f);
        }

        public void OnDeath(Health killedHealth)
        {
            Harvest();
        }

        public bool Harvest()
        {
            if (phase != GrowthPhase.ReadyToHarvest || definition == null)
                return false;

            itemDropper?.Drop(transform.position);
            harvestCount++;

            if (harvestCount >= definition.MaximumHarvests)
            {
                gridManager?.ReleaseFarmPlot(transform.position);
                if (gridManager != null)
                    gridManager.UnSubscribeToGridChanges(this, transform.position);
                registered = false;
                gameObject.SetActive(false);
                return true;
            }

            phase = GrowthPhase.RestingAfterHarvest;
            remainingSeconds = RegrowthRestSeconds;
            health?.SetInvulnerable(true);
            RefreshVisuals();
            StartCoroutine(ReviveAfterDamageCompletes());
            return true;
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
            while (timeLeftToSkip > 0f && transitionGuard++ < 4)
            {
                switch (phase)
                {
                    case GrowthPhase.WaitingForFirstWater:
                        StartFirstGrowth();
                        break;

                    case GrowthPhase.WaitingForRegrowthWater:
                        StartRegrowth();
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
                            StartRegrowth();
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
                version = 4,
                definitionGuid = definition != null ? definition.GetGuid() : string.Empty,
                phase = (int)phase,
                remainingSeconds = remainingSeconds,
                fertilized = fertilized,
                wateredThisCycle = wateredThisCycle,
                harvestCount = harvestCount
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

            if (definition != null)
            {
                if (saveData.version < 2)
                {
                    if (phase == GrowthPhase.WaitingForFirstWater)
                    {
                        StartFirstGrowth();
                    }
                    else if (phase == GrowthPhase.WaitingForRegrowthWater)
                    {
                        StartRegrowth();
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

                activeDuration = phase == GrowthPhase.Regrowing
                    ? definition.RegrowthSeconds + (wateredThisCycle ? 0f : RegrowthMissingWaterPenaltySeconds)
                    : definition.FirstGrowthSeconds
                        + (fertilized ? 0f : MissingFertilizerPenaltySeconds)
                        + (wateredThisCycle ? 0f : FirstGrowthMissingWaterPenaltySeconds);
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
