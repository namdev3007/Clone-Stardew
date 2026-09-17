using System;
using System.Collections.Generic;
using UnityEngine;

namespace World
{
    public enum PropPlacementStatus
    {
        Valid = 0,
        SkippedOnPath = 1,
        SkippedInWater = 2,
        SkippedInCropArea = 3,
        SkippedAtNpcOrSign = 4,
        SkippedOutsideMap = 5,
        SkippedDuplicate = 6
    }

    [Serializable]
    public sealed class MapPropEntry
    {
        public string id;
        public Vector2Int sourcePixel;
        public Vector2Int sourceCell;
        public Vector3Int targetCell;
        public Vector3 worldAnchor;
        public Sprite sprite;
        public float scale = 1f;
        public bool flipX;
        public bool addCollider;
        public string regionName;
        public int sortingOrder;
        public PropPlacementStatus status;

        public MapPropEntry() { }

        public MapPropEntry(
            string id,
            Vector2Int sourcePixel,
            Vector2Int sourceCell,
            Vector3Int targetCell,
            Vector3 worldAnchor,
            Sprite sprite,
            float scale,
            bool flipX,
            bool addCollider,
            string regionName,
            int sortingOrder,
            PropPlacementStatus status)
        {
            this.id = id;
            this.sourcePixel = sourcePixel;
            this.sourceCell = sourceCell;
            this.targetCell = targetCell;
            this.worldAnchor = worldAnchor;
            this.sprite = sprite;
            this.scale = scale;
            this.flipX = flipX;
            this.addCollider = addCollider;
            this.regionName = regionName;
            this.sortingOrder = sortingOrder;
            this.status = status;
        }
    }

    [CreateAssetMenu(fileName = "Map Prop Placement Manifest", menuName = "Farming Kit/Map/Prop Placement Manifest")]
    public sealed class MapPropPlacementManifest : ScriptableObject
    {
        [SerializeField] private List<MapPropEntry> entries = new List<MapPropEntry>();
        [SerializeField] private string lastAnalysisTimestamp;
        [SerializeField] private int validCount;
        [SerializeField] private int skippedCount;

        public IReadOnlyList<MapPropEntry> Entries => entries;
        public string LastAnalysisTimestamp => lastAnalysisTimestamp;
        public int ValidCount => validCount;
        public int SkippedCount => skippedCount;

        public void SetEntries(List<MapPropEntry> newEntries)
        {
            entries = newEntries ?? new List<MapPropEntry>();
            lastAnalysisTimestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            validCount = 0;
            skippedCount = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].status == PropPlacementStatus.Valid)
                    validCount++;
                else
                    skippedCount++;
            }
        }

        public void Clear()
        {
            entries.Clear();
            validCount = 0;
            skippedCount = 0;
            lastAnalysisTimestamp = string.Empty;
        }
    }
}
