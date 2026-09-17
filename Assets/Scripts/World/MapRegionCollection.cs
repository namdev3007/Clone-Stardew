using System;
using System.Collections.Generic;
using UnityEngine;

namespace World
{
    public enum MapRegionScatterSource
    {
        BananaGrowthStages,
        DecorationProps,
        BananaAndDecoration
    }

    [Serializable]
    public sealed class MapRegionScatterSettings
    {
        [SerializeField] private MapRegionScatterSource source = MapRegionScatterSource.BananaGrowthStages;
        [SerializeField, Range(0f, 1f)] private float density = 0.15f;
        [SerializeField, Min(0)] private int randomSeed = 12345;
        [SerializeField, Min(0)] private int minimumCellSpacing = 1;
        [SerializeField, Min(0)] private int minimumBananaStage;
        [SerializeField, Min(0)] private int maximumBananaStage = 7;
        [SerializeField] private Vector2 randomScaleRange = Vector2.one;
        [SerializeField] private bool addCollider = true;
        [SerializeField] private bool requireVisibleMapTile = true;
        [SerializeField] private bool skipWaterCells = true;
        [SerializeField] private bool autoSaveScene = true;

        public MapRegionScatterSource Source => source;
        public float Density => density;
        public int RandomSeed => randomSeed;
        public int MinimumCellSpacing => minimumCellSpacing;
        public int MinimumBananaStage => minimumBananaStage;
        public int MaximumBananaStage => maximumBananaStage;
        public Vector2 RandomScaleRange => randomScaleRange;
        public bool AddCollider => addCollider;
        public bool RequireVisibleMapTile => requireVisibleMapTile;
        public bool SkipWaterCells => skipWaterCells;
        public bool AutoSaveScene => autoSaveScene;

        public void Update(
            MapRegionScatterSource newSource,
            float newDensity,
            int newRandomSeed,
            int newMinimumCellSpacing,
            int newMinimumBananaStage,
            int newMaximumBananaStage,
            Vector2 newRandomScaleRange,
            bool newAddCollider,
            bool newRequireVisibleMapTile,
            bool newSkipWaterCells,
            bool newAutoSaveScene)
        {
            source = newSource;
            density = Mathf.Clamp01(newDensity);
            randomSeed = Mathf.Max(0, newRandomSeed);
            minimumCellSpacing = Mathf.Max(0, newMinimumCellSpacing);
            minimumBananaStage = Mathf.Max(0, newMinimumBananaStage);
            maximumBananaStage = Mathf.Max(minimumBananaStage, newMaximumBananaStage);
            randomScaleRange = new Vector2(
                Mathf.Max(0.05f, Mathf.Min(newRandomScaleRange.x, newRandomScaleRange.y)),
                Mathf.Max(0.05f, Mathf.Max(newRandomScaleRange.x, newRandomScaleRange.y)));
            addCollider = newAddCollider;
            requireVisibleMapTile = newRequireVisibleMapTile;
            skipWaterCells = newSkipWaterCells;
            autoSaveScene = newAutoSaveScene;
        }
    }

    [Serializable]
    public sealed class MapRegionDefinition
    {
        [SerializeField] private string regionName;
        [SerializeField] private string sceneName;
        [SerializeField] private Vector3Int minCell;
        [SerializeField] private Vector3Int maxCell;
        [SerializeField] private MapRegionScatterSettings scatterSettings = new MapRegionScatterSettings();

        public string RegionName => regionName;
        public string SceneName => sceneName;
        public Vector3Int MinCell => minCell;
        public Vector3Int MaxCell => maxCell;
        public int Width => maxCell.x - minCell.x + 1;
        public int Height => maxCell.y - minCell.y + 1;
        public int CellCount => Mathf.Max(0, Width) * Mathf.Max(0, Height);
        public MapRegionScatterSettings ScatterSettings => scatterSettings ?? (scatterSettings = new MapRegionScatterSettings());

        public MapRegionDefinition(string name, string scene, Vector3Int min, Vector3Int max)
        {
            regionName = name;
            sceneName = scene;
            minCell = new Vector3Int(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), 0);
            maxCell = new Vector3Int(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y), 0);
        }

        public void Update(string scene, Vector3Int min, Vector3Int max)
        {
            sceneName = scene;
            minCell = new Vector3Int(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), 0);
            maxCell = new Vector3Int(Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y), 0);
        }

        public bool Contains(Vector3Int cell)
        {
            return cell.x >= minCell.x && cell.x <= maxCell.x &&
                   cell.y >= minCell.y && cell.y <= maxCell.y;
        }
    }

    [CreateAssetMenu(fileName = "Map Region Collection", menuName = "Farming Kit/Map/Region Collection")]
    public sealed class MapRegionCollection : ScriptableObject
    {
        [SerializeField] private List<MapRegionDefinition> regions = new List<MapRegionDefinition>();

        public IReadOnlyList<MapRegionDefinition> Regions => regions;

        public MapRegionDefinition Find(string regionName)
        {
            if (string.IsNullOrWhiteSpace(regionName))
                return null;

            return regions.Find(region =>
                region != null && string.Equals(region.RegionName, regionName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public void AddOrUpdate(string regionName, string sceneName, Vector3Int minCell, Vector3Int maxCell)
        {
            string cleanName = regionName.Trim();
            MapRegionDefinition existing = Find(cleanName);
            if (existing != null)
            {
                existing.Update(sceneName, minCell, maxCell);
                return;
            }

            regions.Add(new MapRegionDefinition(cleanName, sceneName, minCell, maxCell));
            regions.Sort((left, right) => string.Compare(left.RegionName, right.RegionName, StringComparison.OrdinalIgnoreCase));
        }

        public bool Remove(string regionName)
        {
            MapRegionDefinition existing = Find(regionName);
            return existing != null && regions.Remove(existing);
        }
    }
}
