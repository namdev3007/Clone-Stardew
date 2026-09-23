using Plugins.Lowscope.ComponentSaveSystem.Interfaces;
using Referencing.Scriptable_Assets;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using World.Interfaces;
using World.Objects;

namespace World
{
    public enum FarmPlotStatus
    {
        None,
        Fertilize,
        SowSeed,
        Water,
        Harvest
    }

    /// <summary>
    /// Responsible for managing the grids in a scene.
    /// Is used to access grids on specific locations.
    /// Saveable component, all tile actions are saved.
    /// </summary>
    [RequireComponent(typeof(Grid))]
    public class GridManager : MonoBehaviour, ISaveable
    {
        // Data container for all manipulated tiles
        [System.Serializable]
        public struct TileManipulationAction
        {
            // All tiles inherit from a custom class that provides an GUID
            // This class is named ScriptableTileBase.
            public string Guid;
            public Vector3Int Location;
            public string Tag;
        }

        private Grid grid;
        public Grid Grid
        {
            get
            {
                if (grid == null)
                {
                    grid = GetComponent<Grid>();
                    return grid;
                }

                return grid;
            }
        }

        private Dictionary<string, Tilemap> tileMaps = new Dictionary<string, Tilemap>();

        [SerializeField]
        private ScriptableTileBase wateredDirtTile;

        [SerializeField]
        private ScriptableTileBase dirtTile;

        [SerializeField]
        private ScriptableTileBase dirtHoleTile;

        [SerializeField]
        private Tilemap wateredDirtTileMap;

        public Tilemap WateredDirtTileMap { get { return wateredDirtTileMap; } }

        [SerializeField]
        private Tilemap dirtTileMap;

        public Tilemap DirtTileMap { get { return dirtTileMap; } }

        [SerializeField]
        private Tilemap dirtHoleTileMap;

        public Tilemap DirtHoleTileMap { get { return dirtHoleTileMap; } }

        [SerializeField]
        private Tilemap waterTileMap;

        public Tilemap WaterTileMap { get { return waterTileMap; } }

        private Dictionary<Vector3Int, List<ITilemapListener>> listeners = new Dictionary<Vector3Int, List<ITilemapListener>>();

        [System.Serializable]
        public class FarmPlotData
        {
            public Vector3Int location;
            public bool fertilized;
            public bool occupied;
            [System.NonSerialized] public FarmPlotStatus status;
        }

        private readonly Dictionary<Vector3Int, FarmPlotData> farmPlots = new Dictionary<Vector3Int, FarmPlotData>();
        private readonly Dictionary<Vector3Int, SpriteRenderer> farmPlotIndicators = new Dictionary<Vector3Int, SpriteRenderer>();
        private readonly Dictionary<Vector3Int, Vector3> farmPlotIndicatorBasePositions = new Dictionary<Vector3Int, Vector3>();
        private readonly HashSet<Vector3Int> bobbingCropIndicators = new HashSet<Vector3Int>();
        private readonly List<Vector3Int> staleFarmPlotIndicators = new List<Vector3Int>();
        private readonly Dictionary<Vector3Int, Crop> crops = new Dictionary<Vector3Int, Crop>();
        private readonly Dictionary<Vector3Int, Vector3Int> reservedPlantingCells = new Dictionary<Vector3Int, Vector3Int>();
        private readonly HashSet<Vector3Int> waterRefillCells = new HashSet<Vector3Int>();
        private FarmStatusSpriteSet farmStatusSprites;
        private bool farmStatusIndicatorsVisible = true;

        public bool FarmStatusIndicatorsVisible => farmStatusIndicatorsVisible;

        private bool initialized = false;

        private const float CropStatusScale = 0.5f;
        private const float CropStatusGap = 0.012f;
        private const float CropStatusBobAmplitude = 0.006f;
        private const float CropStatusBobCyclesPerSecond = 1.15f;
        private const float GroundStatusCellFill = 0.72f;
        private const float WateredDirtOverlayAlpha = 0.58f;
        private const float FarmStatusValidationInterval = 0.25f;
        private float nextFarmStatusValidationTime;

        private void Awake()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            if (UnityEngine.Time.unscaledTime >= nextFarmStatusValidationTime)
            {
                nextFarmStatusValidationTime = UnityEngine.Time.unscaledTime + FarmStatusValidationInterval;
                PruneStaleFarmPlotIndicators();
            }

            if (!farmStatusIndicatorsVisible || bobbingCropIndicators.Count == 0)
                return;

            float bobOffset = Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f * CropStatusBobCyclesPerSecond)
                              * CropStatusBobAmplitude;
            foreach (Vector3Int location in bobbingCropIndicators)
            {
                if (farmPlotIndicators.TryGetValue(location, out SpriteRenderer renderer) && renderer != null &&
                    renderer.enabled && farmPlotIndicatorBasePositions.TryGetValue(location, out Vector3 basePosition))
                {
                    renderer.transform.position = basePosition + Vector3.up * bobOffset;
                }
            }
        }

        private void EmitGridTilemapAdd(Vector3Int position, string tilemapName)
        {
            List<ITilemapListener> list;

            if (listeners.TryGetValue(position, out list))
            {
                int listenerCount = list.Count;
                for (int i = 0; i < listenerCount; i++)
                {
                    list[i]?.OnAddedTile(tilemapName);
                }
            }
        }

        private void EmitGridTilemapRemove(Vector3Int position, string tilemapName)
        {
            List<ITilemapListener> list;
            if (listeners.TryGetValue(position, out list))
            {
                int listenerCount = list.Count;
                for (int i = 0; i < listenerCount; i++)
                {
                    list[i]?.OnRemovedTile(tilemapName);
                }
            }
        }

        public void SubscribeToGridChanges(ITilemapListener listener, Vector3 pos)
        {
            pos.z = 0;
            Vector3Int gridPos = GetGridLocation(pos);
            List<ITilemapListener> list;

            if (listeners.TryGetValue(gridPos, out list))
            {
                if (!list.Contains(listener))
                {
                    list.Add(listener);
                }
            }
            else
            {
                listeners.Add(gridPos, new List<ITilemapListener>() { listener });
            }
        }

        public void UnSubscribeToGridChanges(ITilemapListener listener, Vector3 pos)
        {
            Vector3Int gridPos = GetGridLocation(pos);
            List<ITilemapListener> list;
            if (listeners.TryGetValue(gridPos, out list))
            {
                list.Remove(listener);
            }
        }

        private void Initialize()
        {
            if (!initialized)
            {
                grid = GetComponent<Grid>();

                Tilemap[] getTileMaps = GetComponentsInChildren<Tilemap>();

                for (int i = 0; i < getTileMaps.Length; i++)
                {
                    tileMaps.Add(getTileMaps[i].name, getTileMaps[i]);
                }

                initialized = true;
            }

            if (farmStatusSprites == null)
                farmStatusSprites = Resources.Load<FarmStatusSpriteSet>("Farming/Farm Status Sprites");

            // Wet ground is an overlay above the normal hoed tile. Blending it
            // with the dry tile keeps the wet cue readable without turning the
            // whole cell into an opaque dark block.
            if (wateredDirtTileMap != null)
                wateredDirtTileMap.color = new Color(1f, 1f, 1f, WateredDirtOverlayAlpha);
        }

        public void RegisterFarmPlot(Vector3Int location)
        {
            Initialize();
            if (!farmPlots.TryGetValue(location, out FarmPlotData plot))
            {
                plot = new FarmPlotData
                {
                    location = location,
                    fertilized = false,
                    occupied = false,
                    status = FarmPlotStatus.Fertilize
                };
                farmPlots.Add(location, plot);
            }

            if (!plot.occupied)
            {
                plot.status = plot.fertilized ? FarmPlotStatus.SowSeed : FarmPlotStatus.Fertilize;
                RefreshFarmPlotIndicator(plot);
            }
        }

        /// <summary>Consumes no item; the item action handles its own charge after this succeeds.</summary>
        public bool TryFertilizePlot(Vector3Int location)
        {
            Initialize();

            if (!SpecialCropRuntime.CanHoe(location) ||
                reservedPlantingCells.ContainsKey(location) ||
                !farmPlots.TryGetValue(location, out FarmPlotData plot) ||
                plot.occupied || plot.fertilized || !HasDirtHole(location))
            {
                return false;
            }

            plot.fertilized = true;
            plot.status = FarmPlotStatus.SowSeed;
            RefreshFarmPlotIndicator(plot);
            return true;
        }

        public bool TryBeginPlanting(Vector3Int location, out bool fertilized)
        {
            return TryBeginPlanting(location, null, out fertilized);
        }

        public bool TryBeginPlanting(Vector3Int location, CropDefinition definition, out bool fertilized)
        {
            fertilized = false;

            if (!SpecialCropRuntime.CanPlant(location, definition))
                return false;

            if (definition != null && definition.IsPerennialTree)
            {
                if (!CanPlantPerennialTree(location))
                    return false;

                RegisterFarmPlot(location);
                FarmPlotData perennialPlot = farmPlots[location];
                perennialPlot.occupied = true;
                perennialPlot.fertilized = true;
                perennialPlot.status = FarmPlotStatus.None;
                ReservePerennialFootprint(location);
                RefreshFarmPlotIndicator(perennialPlot);
                fertilized = true;
                return true;
            }

            if (reservedPlantingCells.ContainsKey(location))
                return false;

            if (!farmPlots.TryGetValue(location, out FarmPlotData plot) || plot.occupied || !HasDirtHole(location) || !plot.fertilized)
                return false;

            fertilized = plot.fertilized;
            plot.occupied = true;
            plot.status = FarmPlotStatus.None;
            RefreshFarmPlotIndicator(plot);
            return true;
        }

        public bool CheatBeginShowcasePlanting(Vector3Int location, CropDefinition definition, out bool fertilized)
        {
            fertilized = true;
            if (definition == null || !HasDirt(location) || HasWater(location))
                return false;

            RegisterFarmPlot(location);
            FarmPlotData plot = farmPlots[location];
            plot.occupied = true;
            plot.fertilized = true;
            plot.status = FarmPlotStatus.None;
            if (definition.IsPerennialTree)
                ReservePerennialFootprint(location);
            RefreshFarmPlotIndicator(plot);
            return true;
        }

        public void CancelPlanting(Vector3Int location)
        {
            bool wasPerennialPlanting = reservedPlantingCells.TryGetValue(location, out Vector3Int center) && center == location;
            ReleasePerennialFootprint(location);
            if (!farmPlots.TryGetValue(location, out FarmPlotData plot))
                return;

            if (wasPerennialPlanting)
            {
                farmPlots.Remove(location);
                RemoveFarmPlotIndicator(location);
                return;
            }

            plot.occupied = false;
            plot.status = plot.fertilized ? FarmPlotStatus.SowSeed : FarmPlotStatus.Fertilize;
            RefreshFarmPlotIndicator(plot);
        }

        public void RegisterCrop(Crop crop, Vector3 worldPosition)
        {
            Vector3Int location = GetGridLocation(worldPosition);
            RegisterFarmPlot(location);
            farmPlots[location].occupied = true;
            crops[location] = crop;
            if (crop != null && crop.UsesPerennialFootprint)
                ReservePerennialFootprint(location);
        }

        public void UnregisterCrop(Crop crop, Vector3 worldPosition)
        {
            Vector3Int location = GetGridLocation(worldPosition);
            if (crops.TryGetValue(location, out Crop current) && current == crop)
            {
                crops.Remove(location);
                if (crop != null && crop.UsesPerennialFootprint)
                    ReleasePerennialFootprint(location);
            }
        }

        public Crop GetCrop(Vector3Int location)
        {
            crops.TryGetValue(location, out Crop crop);
            return crop;
        }

        public void SetCropStatus(Vector3 worldPosition, FarmPlotStatus status)
        {
            Vector3Int location = GetGridLocation(worldPosition);
            RegisterFarmPlot(location);
            FarmPlotData plot = farmPlots[location];
            plot.occupied = true;
            plot.status = status;
            RefreshFarmPlotIndicator(plot);
        }

        public void RefreshCropStatusPosition(Vector3 worldPosition)
        {
            Vector3Int location = GetGridLocation(worldPosition);
            if (farmPlots.TryGetValue(location, out FarmPlotData plot) && plot.occupied &&
                plot.status != FarmPlotStatus.None)
            {
                RefreshFarmPlotIndicator(plot);
            }
        }

        public void ReleaseFarmPlot(Vector3 worldPosition)
        {
            Vector3Int location = GetGridLocation(worldPosition);
            bool wasPerennialPlot = reservedPlantingCells.TryGetValue(location, out Vector3Int center) &&
                                    center == location;
            ReleasePerennialFootprint(location);
            crops.Remove(location);

            // Ordinary soil must be hoed again after the crop's final harvest.
            // Trellis post cells are the sole exception: they remain tilled.
            if (!SpecialCropRuntime.IsPermanentTilledCell(location))
            {
                ClearOrdinaryFarmPlot(location);
                return;
            }

            if (wasPerennialPlot)
                return;

            RegisterFarmPlot(location);
            FarmPlotData plot = farmPlots[location];
            plot.occupied = false;
            plot.fertilized = false;
            plot.status = FarmPlotStatus.Fertilize;
            RefreshFarmPlotIndicator(plot);
        }

        /// <summary>Right-click with the hoe removes an ordinary hoed cell and any crop on it.</summary>
        public bool TryCancelHoedCell(Vector3Int location)
        {
            Initialize();
            if (SpecialCropRuntime.IsPermanentTilledCell(location) || !HasDirtHole(location))
                return false;

            if (crops.TryGetValue(location, out Crop crop) && crop != null)
            {
                crops.Remove(location);
                ReleasePerennialFootprint(location);
                Destroy(crop.gameObject);
            }

            ClearOrdinaryFarmPlot(location);
            return true;
        }

        private void ClearOrdinaryFarmPlot(Vector3Int location)
        {
            RemoveCheatTile(wateredDirtTileMap, location);
            RemoveCheatTile(dirtHoleTileMap, location);
            farmPlots.Remove(location);
            RemoveFarmPlotIndicator(location);
        }

        private bool CanPlantPerennialTree(Vector3Int center)
        {
            bool canPlantOnGrass = FarmExpansionRuntime.CanPlantPerennialFootprint(center);
            if (!canPlantOnGrass)
                return false;

            bool isPlantingOnDirt = HasDirt(center);
            if (isPlantingOnDirt)
            {
                // Banana and mango trees planted on soil cannot be on the outer edge of the dirt plot;
                // they must be indented at least 1 tile deep into the dirt (1-tile padding around the 3x3 footprint).
                for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    Vector3Int checkCell = center + new Vector3Int(dx, dy, 0);
                    if (!HasDirt(checkCell))
                        return false;
                }
            }

            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    Vector3Int location = center + new Vector3Int(x, y, 0);
                    if ((!canPlantOnGrass && !HasDirt(location)) || HasDirtHole(location) || HasWater(location) ||
                        reservedPlantingCells.ContainsKey(location) || crops.ContainsKey(location))
                        return false;

                    if (farmPlots.TryGetValue(location, out FarmPlotData plot) && plot.occupied)
                        return false;
                }
            }

            return true;
        }

        private void ReservePerennialFootprint(Vector3Int center)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int x = -1; x <= 1; x++)
                    reservedPlantingCells[center + new Vector3Int(x, y, 0)] = center;
            }
        }

        private void ReleasePerennialFootprint(Vector3Int center)
        {
            List<Vector3Int> cellsToRelease = new List<Vector3Int>();
            foreach (KeyValuePair<Vector3Int, Vector3Int> entry in reservedPlantingCells)
            {
                if (entry.Value == center)
                    cellsToRelease.Add(entry.Key);
            }

            for (int i = 0; i < cellsToRelease.Count; i++)
                reservedPlantingCells.Remove(cellsToRelease[i]);
        }

        private void RefreshFarmPlotIndicator(FarmPlotData plot)
        {
            if (plot.status != FarmPlotStatus.None && !IsFarmStatusApplicable(plot))
            {
                RemoveFarmPlotIndicator(plot.location);
                return;
            }

            if (!farmStatusIndicatorsVisible)
            {
                if (farmPlotIndicators.TryGetValue(plot.location, out SpriteRenderer disabledRenderer))
                    disabledRenderer.enabled = false;
                return;
            }

            Sprite sprite = GetStatusSprite(plot.status);
            if (sprite == null)
            {
                if (farmPlotIndicators.TryGetValue(plot.location, out SpriteRenderer hiddenRenderer))
                    hiddenRenderer.enabled = false;
                bobbingCropIndicators.Remove(plot.location);
                farmPlotIndicatorBasePositions.Remove(plot.location);
                return;
            }

            if (!farmPlotIndicators.TryGetValue(plot.location, out SpriteRenderer renderer) || renderer == null)
            {
                GameObject indicator = new GameObject($"Farm Status {plot.location.x},{plot.location.y}");
                indicator.transform.SetParent(transform, true);
                renderer = indicator.AddComponent<SpriteRenderer>();
                renderer.sortingLayerName = "Foreground Trees";
                renderer.sortingOrder = short.MaxValue;
                farmPlotIndicators[plot.location] = renderer;
            }

            Vector3 cropAnchor = Vector3.zero;
            bool followsCrop = plot.occupied && crops.TryGetValue(plot.location, out Crop crop) && crop != null &&
                               crop.gameObject.activeInHierarchy && crop.TryGetStatusAnchor(out cropAnchor);
            float statusScale = followsCrop ? CropStatusScale : GetGroundStatusScale(sprite);

            renderer.sprite = sprite;
            renderer.transform.localScale = Vector3.one * statusScale;
            renderer.color = Color.white;

            Vector3 basePosition;
            if (followsCrop)
            {
                // The imported status sprites use a bottom-left pivot. Anchor the
                // visible bottom-centre just above the current crop frame.
                basePosition = cropAnchor + new Vector3(
                    -sprite.bounds.center.x * statusScale,
                    CropStatusGap - sprite.bounds.min.y * statusScale,
                    0f);
                bobbingCropIndicators.Add(plot.location);
            }
            else
            {
                // Empty-plot instructions belong inside the soil cell. Centre the
                // visible bounds instead of centring the sprite's custom pivot.
                basePosition = GetWorldLocation(plot.location) - sprite.bounds.center * statusScale;
                bobbingCropIndicators.Remove(plot.location);
            }

            farmPlotIndicatorBasePositions[plot.location] = basePosition;
            renderer.transform.position = basePosition;
            renderer.enabled = true;
        }

        private bool IsFarmStatusApplicable(FarmPlotData plot)
        {
            if (plot.status == FarmPlotStatus.None)
                return false;

            if (!plot.occupied)
                return HasDirtHole(plot.location);

            return crops.TryGetValue(plot.location, out Crop crop) && crop != null &&
                   crop.gameObject.activeInHierarchy;
        }

        private void PruneStaleFarmPlotIndicators()
        {
            if (farmPlotIndicators.Count == 0)
                return;

            staleFarmPlotIndicators.Clear();
            foreach (KeyValuePair<Vector3Int, SpriteRenderer> entry in farmPlotIndicators)
            {
                if (entry.Value == null || !farmPlots.TryGetValue(entry.Key, out FarmPlotData plot) ||
                    !IsFarmStatusApplicable(plot))
                {
                    staleFarmPlotIndicators.Add(entry.Key);
                }
            }

            for (int i = 0; i < staleFarmPlotIndicators.Count; i++)
            {
                Vector3Int location = staleFarmPlotIndicators[i];
                bool removePlot = farmPlots.TryGetValue(location, out FarmPlotData plot) &&
                                  !plot.occupied && !HasDirtHole(location);

                RemoveFarmPlotIndicator(location);
                if (removePlot)
                {
                    farmPlots.Remove(location);
                    RemoveRecordedFarmTileActions(location);
                }
            }
        }

        private void RemoveRecordedFarmTileActions(Vector3Int location)
        {
            string dirtHoleMapName = dirtHoleTileMap != null ? dirtHoleTileMap.name : null;
            string wateredMapName = wateredDirtTileMap != null ? wateredDirtTileMap.name : null;

            for (int i = saveData.actions.Count - 1; i >= 0; i--)
            {
                TileManipulationAction action = saveData.actions[i];
                if (action.Location == location &&
                    (action.Tag == dirtHoleMapName || action.Tag == wateredMapName))
                {
                    saveData.actions.RemoveAt(i);
                }
            }
        }

        private void RemoveFarmPlotIndicator(Vector3Int location)
        {
            if (farmPlotIndicators.TryGetValue(location, out SpriteRenderer renderer) && renderer != null)
                Destroy(renderer.gameObject);

            farmPlotIndicators.Remove(location);
            farmPlotIndicatorBasePositions.Remove(location);
            bobbingCropIndicators.Remove(location);
        }

        private float GetGroundStatusScale(Sprite sprite)
        {
            float spriteSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            float cellSize = Mathf.Min(Mathf.Abs(Grid.cellSize.x), Mathf.Abs(Grid.cellSize.y));
            if (spriteSize <= Mathf.Epsilon || cellSize <= Mathf.Epsilon)
                return CropStatusScale;

            return Mathf.Min(CropStatusScale, cellSize * GroundStatusCellFill / spriteSize);
        }

        public void CheatSetFarmStatusVisible(bool visible)
        {
            farmStatusIndicatorsVisible = visible;

            if (!visible)
            {
                foreach (SpriteRenderer renderer in farmPlotIndicators.Values)
                {
                    if (renderer != null)
                        renderer.enabled = false;
                }
                return;
            }

            foreach (FarmPlotData plot in farmPlots.Values)
                RefreshFarmPlotIndicator(plot);
        }

        private Sprite GetStatusSprite(FarmPlotStatus status)
        {
            if (farmStatusSprites == null)
                farmStatusSprites = Resources.Load<FarmStatusSpriteSet>("Farming/Farm Status Sprites");

            if (farmStatusSprites == null)
                return null;

            switch (status)
            {
                case FarmPlotStatus.Fertilize: return farmStatusSprites.Fertilize;
                case FarmPlotStatus.SowSeed: return farmStatusSprites.SowSeed;
                case FarmPlotStatus.Water: return farmStatusSprites.Water;
                case FarmPlotStatus.Harvest: return farmStatusSprites.Harvest;
                default: return null;
            }
        }

        public int CheatHoeAllDirt()
        {
            Initialize();
            if (dirtTileMap == null)
                return 0;

            List<Vector3Int> locations = new List<Vector3Int>();
            foreach (Vector3Int location in dirtTileMap.cellBounds.allPositionsWithin)
            {
                if (HasDirt(location) && !HasDirtHole(location) && !HasWater(location))
                    locations.Add(location);
            }

            for (int i = 0; i < locations.Count; i++)
            {
                SetDirtHoleTile(locations[i]);
                RegisterFarmPlot(locations[i]);
            }

            return locations.Count;
        }

        public int CheatHoeAlternatingRows()
        {
            Initialize();
            if (dirtTileMap == null)
                return 0;

            int firstRow = dirtTileMap.cellBounds.yMin;
            List<Vector3Int> locations = new List<Vector3Int>();
            foreach (Vector3Int location in dirtTileMap.cellBounds.allPositionsWithin)
            {
                bool isHoeRow = Mathf.Abs(location.y - firstRow) % 2 == 0;
                if (isHoeRow && HasDirt(location) && !HasDirtHole(location) && !HasWater(location))
                    locations.Add(location);
            }

            for (int i = 0; i < locations.Count; i++)
            {
                SetDirtHoleTile(locations[i]);
                RegisterFarmPlot(locations[i]);
            }

            return locations.Count;
        }

        public List<List<Vector3Int>> CheatPrepareCropShowcaseRows(int[] stageCounts, bool[] perennialRows, Vector3 worldCenter)
        {
            CheatResetAllFarmLand();
            Initialize();

            List<List<Vector3Int>> rows = new List<List<Vector3Int>>();
            if (dirtTileMap == null || stageCounts == null || perennialRows == null)
                return rows;

            int rowCount = Mathf.Min(stageCounts.Length, perennialRows.Length);
            Vector3Int center = GetGridLocation(worldCenter);
            BoundsInt bounds = dirtTileMap.cellBounds;
            List<int> candidateRows = new List<int>();
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                candidateRows.Add(y);
            candidateRows.Sort((left, right) => Mathf.Abs(left - center.y).CompareTo(Mathf.Abs(right - center.y)));

            List<int> usedRowCenters = new List<int>();
            HashSet<Vector3Int> usedFootprint = new HashSet<Vector3Int>();
            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                int stageCount = Mathf.Max(0, stageCounts[rowIndex]);
                bool perennial = perennialRows[rowIndex];
                int radius = perennial ? 1 : 0;
                // A 3x3 footprint blocks the eight immediate neighbours. Two cells
                // between tree centres is sufficient: neither centre enters the
                // other's blocked neighbourhood, while long showcase rows still fit.
                int spacing = perennial ? 2 : 1;
                List<Vector3Int> selectedRow = new List<Vector3Int>();

                for (int yIndex = 0; yIndex < candidateRows.Count && selectedRow.Count == 0; yIndex++)
                {
                    int y = candidateRows[yIndex];
                    if (y - radius < bounds.yMin || y + radius >= bounds.yMax)
                        continue;

                    bool rowTooClose = false;
                    for (int usedIndex = 0; usedIndex < usedRowCenters.Count; usedIndex++)
                    {
                        if (Mathf.Abs(y - usedRowCenters[usedIndex]) < 3)
                        {
                            rowTooClose = true;
                            break;
                        }
                    }
                    if (rowTooClose)
                        continue;

                    int lastCenterOffset = Mathf.Max(0, stageCount - 1) * spacing;
                    List<int> candidateStarts = new List<int>();
                    for (int startX = bounds.xMin + radius; startX + lastCenterOffset + radius < bounds.xMax; startX++)
                        candidateStarts.Add(startX);
                    candidateStarts.Sort((left, right) =>
                    {
                        float leftMiddle = left + lastCenterOffset * 0.5f;
                        float rightMiddle = right + lastCenterOffset * 0.5f;
                        return Mathf.Abs(leftMiddle - center.x).CompareTo(Mathf.Abs(rightMiddle - center.x));
                    });

                    for (int startIndex = 0; startIndex < candidateStarts.Count; startIndex++)
                    {
                        int startX = candidateStarts[startIndex];
                        List<Vector3Int> candidate = new List<Vector3Int>();
                        bool valid = true;
                        for (int stageIndex = 0; stageIndex < stageCount && valid; stageIndex++)
                        {
                            Vector3Int stageCenter = new Vector3Int(startX + stageIndex * spacing, y, 0);
                            candidate.Add(stageCenter);
                            for (int offsetY = -radius; offsetY <= radius && valid; offsetY++)
                            {
                                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                                {
                                    Vector3Int cell = stageCenter + new Vector3Int(offsetX, offsetY, 0);
                                    if (!HasDirt(cell) || HasWater(cell) || usedFootprint.Contains(cell))
                                    {
                                        valid = false;
                                        break;
                                    }
                                }
                            }
                        }

                        if (valid)
                        {
                            selectedRow = candidate;
                            break;
                        }
                    }
                }

                if (selectedRow.Count > 0)
                {
                    usedRowCenters.Add(selectedRow[0].y);
                    for (int i = 0; i < selectedRow.Count; i++)
                    {
                        int footprintRadius = perennial ? 1 : 0;
                        for (int y = -footprintRadius; y <= footprintRadius; y++)
                        {
                            for (int x = -footprintRadius; x <= footprintRadius; x++)
                                usedFootprint.Add(selectedRow[i] + new Vector3Int(x, y, 0));
                        }

                        if (!perennial)
                        {
                            SetDirtHoleTile(selectedRow[i]);
                            RegisterFarmPlot(selectedRow[i]);
                        }
                    }
                }

                rows.Add(selectedRow);
            }

            return rows;
        }

        public int CheatResetAllFarmLand()
        {
            Initialize();
            HashSet<Vector3Int> locations = new HashSet<Vector3Int>(farmPlots.Keys);

            CollectTileLocations(dirtHoleTileMap, locations);
            CollectTileLocations(wateredDirtTileMap, locations);

            List<Crop> activeCrops = new List<Crop>(crops.Values);
            crops.Clear();
            for (int i = 0; i < activeCrops.Count; i++)
            {
                if (activeCrops[i] != null)
                    Destroy(activeCrops[i].gameObject);
            }

            foreach (Vector3Int location in locations)
            {
                // Trellis post cells are permanently tilled, so the reset leaves
                // their hoed ground alone and only clears the watering.
                if (!SpecialCropRuntime.IsPermanentTilledCell(location))
                    RemoveCheatTile(dirtHoleTileMap, location);
                RemoveCheatTile(wateredDirtTileMap, location);

                if (farmPlotIndicators.TryGetValue(location, out SpriteRenderer indicator) && indicator != null)
                    Destroy(indicator.gameObject);
            }

            farmPlotIndicators.Clear();
            farmPlots.Clear();
            reservedPlantingCells.Clear();
            return locations.Count;
        }

        private static void CollectTileLocations(Tilemap tilemap, HashSet<Vector3Int> locations)
        {
            if (tilemap == null)
                return;

            foreach (Vector3Int location in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.HasTile(location))
                    locations.Add(location);
            }
        }

        private void RemoveCheatTile(Tilemap tilemap, Vector3Int location)
        {
            if (tilemap == null || !tilemap.HasTile(location))
                return;

            tilemap.SetTile(location, null);
            EmitGridTilemapRemove(location, tilemap.name);

            for (int i = saveData.actions.Count - 1; i >= 0; i--)
            {
                TileManipulationAction action = saveData.actions[i];
                if (action.Location == location && action.Tag == tilemap.name)
                    saveData.actions.RemoveAt(i);
            }
        }

        public int CheatWaterAllPlots()
        {
            List<Vector3Int> locations = new List<Vector3Int>(farmPlots.Keys);
            int count = 0;
            for (int i = 0; i < locations.Count; i++)
            {
                Vector3Int location = locations[i];
                if (!HasDirtHole(location))
                    continue;

                if (!HasWateredDirt(location))
                    SetWateredDirtTile(location);
                GetCrop(location)?.CheatWater();
                count++;
            }
            return count;
        }

        public int CheatFertilizeAllPlots()
        {
            int count = 0;
            foreach (KeyValuePair<Vector3Int, FarmPlotData> entry in farmPlots)
            {
                entry.Value.fertilized = true;
                GetCrop(entry.Key)?.CheatFertilize();
                count++;
            }
            return count;
        }

        public int CheatGrowAllCrops()
        {
            List<Crop> activeCrops = new List<Crop>(crops.Values);
            for (int i = 0; i < activeCrops.Count; i++)
                activeCrops[i]?.CheatGrowNow();
            return activeCrops.Count;
        }

        public int CheatAdvanceAllCropGrowth(float seconds)
        {
            List<Crop> activeCrops = new List<Crop>(crops.Values);
            int count = 0;
            for (int i = 0; i < activeCrops.Count; i++)
            {
                if (activeCrops[i] != null && activeCrops[i].CheatAdvanceGrowth(seconds))
                    count++;
            }
            return count;
        }

        public int CheatHarvestAllCrops()
        {
            List<Crop> activeCrops = new List<Crop>(crops.Values);
            int count = 0;
            for (int i = 0; i < activeCrops.Count; i++)
            {
                if (activeCrops[i] != null && activeCrops[i].Harvest())
                    count++;
            }
            return count;
        }

        public void ClearTileMapData(Tilemap tileMap)
        {
            string tileMapName = tileMap.name;

            for (int i = saveData.actions.Count - 1; i >= 0; i--)
            {
                // TODO: Make sure this becomes optimized by providing an more efficient way of comparing.
                if (saveData.actions[i].Tag == tileMapName)
                {
                    saveData.actions.RemoveAt(i);
                }
            }

            tileMap.ClearAllTiles();
        }

        public int GetGridLocationTileCount(Vector3Int location)
        {
            int tiles = 0;

            foreach (Tilemap item in tileMaps.Values)
            {
                if (item.HasTile(location))
                {
                    tiles++;
                }
            }

            return tiles;
        }

        public Tilemap GetTilemap(string name)
        {
            Tilemap tileMap;

            tileMaps.TryGetValue(name, out tileMap);

            if (tileMap != null)
            {
                return tileMap;
            }
            else
            {
                Debug.Log($"Failed attempt to get tilemap with name: {name}");
            }

            return null;
        }

        public Vector3Int GetGridLocation(Vector3 position)
        {
            return Grid.WorldToCell(position);
        }

        public Vector3 GetWorldLocation(Vector3Int position)
        {
            return Grid.CellToWorld(position) + grid.cellSize * 0.5f;
        }

        public bool ContainsMapAtLocation(string tileMapName, Vector3Int location)
        {
            Tilemap tileMap;

            if (tileMaps.TryGetValue(tileMapName, out tileMap))
            {
                if (tileMap.HasTile(location))
                {
                    return true;
                }
            }

            return false;
        }

        public bool ContainsMapsAtLocation(string[] tileMapNames, Vector3Int location)
        {
            for (int i = 0; i < tileMapNames.Length; i++)
            {
                Tilemap tileMap;

                if (tileMaps.TryGetValue(tileMapNames[i], out tileMap))
                {
                    if (tileMap.HasTile(location))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool ContainsMapAtLocation(string tileMapName, Vector3 worldPosition)
        {
            return ContainsMapAtLocation(tileMapName, GetGridLocation(worldPosition));
        }

        public bool ContainsMapsAtLocation(string[] tileMapNames, Vector3 worldPosition)
        {
            return ContainsMapsAtLocation(tileMapNames, GetGridLocation(worldPosition));
        }

        public ScriptableTileBase GetTile(Vector3Int location, string tilemapName)
        {
            Tilemap tileMap;

            tileMaps.TryGetValue(tilemapName, out tileMap);

            if (tileMap != null)
            {
                return tileMap.GetTile(location) as ScriptableTileBase;
            }
            else
            {
                Debug.Log($"Failed attempt to get tilemap with name: {tilemapName}");
            }

            return null;
        }

        public void SetTile(Vector3Int location, string tilemapName, ScriptableTileBase tileBase)
        {
            ApplyTile(location, tilemapName, tileBase, true);
        }

        private void ApplyTile(Vector3Int location, string tilemapName, ScriptableTileBase tileBase, bool recordAction)
        {
            if (tileBase == null)
            {
                Debug.LogWarning($"Cannot apply a null tile to '{tilemapName}' at {location}.");
                return;
            }

            Tilemap tileMap;

            tileMaps.TryGetValue(tilemapName, out tileMap);

            if (tileMap != null)
            {
                tileMap.SetTile(location, tileBase);

                EmitGridTilemapAdd(location, tilemapName);

                if (recordAction)
                {
                    // Only the latest state for one cell on one tilemap matters.
                    // Repeated unlock/setup calls used to grow the save file with
                    // hundreds of identical actions and replay all of them on load.
                    RemoveRecordedTileAction(location, tilemapName);
                    saveData.actions.Add(new TileManipulationAction()
                    {
                        Guid = tileBase.GetGuid(),
                        Location = location,
                        Tag = tilemapName
                    });
                }
            }
            else
            {
                Debug.Log($"Failed attempt to set tilemap with name: {tilemapName}");
            }
        }

        public bool HasDirt(Vector3Int position)
        {
            return dirtTileMap != null && dirtTileMap.GetTile(position) != null;
        }

        public bool HasDirtHole(Vector3Int position)
        {
            return dirtHoleTileMap != null && dirtHoleTileMap.GetTile(position) != null;
        }

        public bool HasWateredDirt(Vector3Int position)
        {
            return wateredDirtTileMap != null && wateredDirtTileMap.GetTile(position) != null;
        }

        public bool HasWater(Vector3Int position)
        {
            return waterTileMap != null && waterTileMap.GetTile(position) != null;
        }

        public bool CanHoeCell(Vector3Int position)
        {
            return SpecialCropRuntime.CanHoe(position);
        }

        public bool HasFarmPlot(Vector3Int position)
        {
            return farmPlots.ContainsKey(position);
        }

        public bool HasCrop(Vector3Int position)
        {
            return crops.ContainsKey(position) && crops[position] != null;
        }

        /// <summary>
        /// Returns true if the cell is any valid soil plot (hoed dirt hole, registered
        /// farm plot, planted crop, repaired trellis planting slot, or tillable farm dirt).
        /// </summary>
        public bool IsSoilCell(Vector3Int position)
        {
            if (HasDirtHole(position))
                return true;

            if (farmPlots.ContainsKey(position))
                return true;

            if (crops.ContainsKey(position) && crops[position] != null)
                return true;

            if (SpecialCropRuntime.IsRepairedPlantingSlot(position))
                return true;

            if (CanHoeCell(position) && HasDirt(position) && !HasWater(position))
                return true;

            return false;
        }

        public void RegisterWaterRefillCell(Vector3Int position)
        {
            position.z = 0;
            waterRefillCells.Add(position);
        }

        public void UnregisterWaterRefillCell(Vector3Int position)
        {
            position.z = 0;
            waterRefillCells.Remove(position);
        }

        public bool CanRefillWaterAt(Vector3Int position)
        {
            position.z = 0;
            return HasWater(position) || waterRefillCells.Contains(position);
        }

        public void SetDirtTile(Vector3Int location)
        {
            if (dirtTileMap != null)
            {
                SetTile(location, dirtTileMap.name, dirtTile);
            }
        }

        /// <summary>
        /// Adds ordinary farmable ground only when the cell does not already
        /// contain it. This keeps unlock-time terrain preparation idempotent
        /// and avoids recording duplicate tile actions in the save data.
        /// </summary>
        public bool EnsureDirtTile(Vector3Int location)
        {
            if (dirtTileMap == null || dirtTile == null || HasDirt(location) || HasWater(location))
                return false;

            SetTile(location, dirtTileMap.name, dirtTile);
            return true;
        }

        public void SetWateredDirtTile(Vector3Int location)
        {
            if (wateredDirtTileMap != null)
            {
                SetTile(location, wateredDirtTileMap.name, wateredDirtTile);
            }
        }

        public void SetDirtHoleTile(Vector3Int location)
        {
            if (dirtHoleTileMap != null)
            {
                SetTile(location, dirtHoleTileMap.name, dirtHoleTile);
            }
        }

        #region Saving

        [System.Serializable]
        public class SaveData
        {
            public int version;
            public List<TileManipulationAction> actions = new List<TileManipulationAction>();
            public List<FarmPlotData> farmPlots = new List<FarmPlotData>();
        }

        private SaveData saveData = new SaveData();

        public string OnSave()
        {
            saveData.version = 2;
            CompactTileActions();
            saveData.farmPlots.Clear();
            foreach (FarmPlotData plot in farmPlots.Values)
            {
                saveData.farmPlots.Add(new FarmPlotData
                {
                    location = plot.location,
                    fertilized = plot.fertilized,
                    occupied = plot.occupied
                });
            }
            return JsonUtility.ToJson(saveData);
        }

        public void OnLoad(string data)
        {
            Initialize();

            SaveData loadedData = JsonUtility.FromJson<SaveData>(data);

            if (loadedData != null)
            {
                saveData = loadedData;
                if (saveData.actions == null)
                    saveData.actions = new List<TileManipulationAction>();
                if (saveData.farmPlots == null)
                    saveData.farmPlots = new List<FarmPlotData>();

                // Version 2 installs a completely different farm grid. Tile
                // coordinates recorded against the previous map must not be
                // replayed over the new layout.
                if (saveData.version < 2)
                {
                    saveData.actions.Clear();
                    saveData.farmPlots.Clear();
                    saveData.version = 2;
                }

                CompactTileActions();

                for (int i = 0; i < saveData.actions.Count; i++)
                {
                    TileManipulationAction action = saveData.actions[i];
                    ScriptableTileBase tileBase = ScriptableAssetDatabase.GetAsset(action.Guid) as ScriptableTileBase;

                    // Farming tiles used by the rebuilt map live under Assets/Tiles.
                    // Older builds did not register that folder in the scriptable
                    // asset database, so keep the tile-map identity as a durable
                    // fallback for existing save files.
                    if (tileBase == null)
                        tileBase = ResolveSavedTile(action.Tag);

                    if (tileBase != null)
                    {
                        ApplyTile(action.Location, action.Tag, tileBase, false);
                    }
                    else
                    {
                        Debug.LogWarning($"Could not restore tile '{action.Guid}' on tilemap '{action.Tag}' at {action.Location}.");
                    }
                }

                farmPlots.Clear();
                for (int i = 0; i < saveData.farmPlots.Count; i++)
                {
                    FarmPlotData plot = saveData.farmPlots[i];
                    // Older saves came from the temporary fertilizer bypass and
                    // marked every empty plot as fertilized. Reset only empty plots.
                    if (saveData.version < 1 && !plot.occupied)
                        plot.fertilized = false;
                    plot.status = plot.occupied
                        ? FarmPlotStatus.None
                        : (plot.fertilized ? FarmPlotStatus.SowSeed : FarmPlotStatus.Fertilize);
                    farmPlots[plot.location] = plot;
                    RefreshFarmPlotIndicator(plot);
                }

                // Saves created before the fertilizer system contain dirt holes but no plot data.
                if (saveData.farmPlots.Count == 0 && dirtHoleTileMap != null)
                {
                    foreach (Vector3Int location in dirtHoleTileMap.cellBounds.allPositionsWithin)
                    {
                        if (dirtHoleTileMap.HasTile(location))
                            RegisterFarmPlot(location);
                    }
                }

                foreach (KeyValuePair<Vector3Int, Crop> entry in crops)
                {
                    if (farmPlots.TryGetValue(entry.Key, out FarmPlotData plot))
                    {
                        plot.occupied = true;
                        if (entry.Value != null)
                            entry.Value.RefreshVisuals();
                        else
                            RefreshFarmPlotIndicator(plot);
                    }
                }
            }
        }

        public bool OnSaveCondition() => true;

        private ScriptableTileBase ResolveSavedTile(string tilemapName)
        {
            if (dirtHoleTileMap != null && tilemapName == dirtHoleTileMap.name)
                return dirtHoleTile;
            if (wateredDirtTileMap != null && tilemapName == wateredDirtTileMap.name)
                return wateredDirtTile;
            if (dirtTileMap != null && tilemapName == dirtTileMap.name)
                return dirtTile;

            return null;
        }

        private void RemoveRecordedTileAction(Vector3Int location, string tilemapName)
        {
            for (int i = saveData.actions.Count - 1; i >= 0; i--)
            {
                TileManipulationAction action = saveData.actions[i];
                if (action.Location == location && action.Tag == tilemapName)
                    saveData.actions.RemoveAt(i);
            }
        }

        private void CompactTileActions()
        {
            if (saveData.actions == null || saveData.actions.Count < 2)
                return;

            HashSet<string> occupiedCells = new HashSet<string>();
            for (int i = saveData.actions.Count - 1; i >= 0; i--)
            {
                TileManipulationAction action = saveData.actions[i];
                string key = action.Tag + "\u001f" + action.Location.x + "," +
                             action.Location.y + "," + action.Location.z;
                if (!occupiedCells.Add(key))
                    saveData.actions.RemoveAt(i);
            }
        }

        #endregion
    }
}
