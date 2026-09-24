using Item;
using Referencing.Scriptable_Assets;
using UnityEngine;

namespace World.Objects
{
    public enum PlantingZone
    {
        Normal,
        CucumberTrellis,
        DragonFruitTrellis
    }

    [CreateAssetMenu(fileName = "Crop Definition", menuName = "Farming/Crop Definition")]
    public class CropDefinition : ScriptableAsset
    {
        [SerializeField] private string displayName;
        [SerializeField] private float firstGrowthSeconds = 15f;
        [SerializeField] private float regrowthSeconds;
        [SerializeField] private int maximumHarvests = 1;
        [SerializeField, Min(1)] private int harvestYield = 1;
        [SerializeField] private int regrowthStageStart;
        [SerializeField, Tooltip("Perennial trees are planted directly on hoeable ground and reserve a 3x3 area.")]
        private bool perennialTree;
        [SerializeField] private PlantingZone plantingZone;
        [SerializeField] private Sprite[] growthSprites;
        [SerializeField, Tooltip("How each growth sprite is anchored in its soil cell, using the same array index.")]
        private CropStageAnchor[] stageAnchorModes;
        [SerializeField, Tooltip("Anchor pixel of each growth sprite (from the sprite rect's bottom-left), used by the Pixel anchor modes.")]
        private Vector2[] stageAnchorPixels;
        [SerializeField, Tooltip("Local X/Y correction for each growth sprite, applied after its anchor.")]
        private Vector2[] stagePositionOffsets;
        [SerializeField, Min(0f), Tooltip("Height of the ground line above the soil cell bottom, in world units.")]
        private float groundInset = CropSpriteAlignment.DefaultGroundInset;
        [SerializeField] private ItemData harvestedItem;

        public string DisplayName => displayName;
        public float FirstGrowthSeconds => Mathf.Max(0.1f, firstGrowthSeconds);
        public float RegrowthSeconds => Mathf.Max(0.1f, regrowthSeconds);
        public int MaximumHarvests => Mathf.Max(1, maximumHarvests);
        public int HarvestYield => Mathf.Max(1, harvestYield);
        public int RegrowthStageStart => Mathf.Clamp(regrowthStageStart, 0, Mathf.Max(0, growthSprites.Length - 1));
        public bool IsPerennialTree => perennialTree;
        public PlantingZone PlantingZone => plantingZone;
        public Sprite[] GrowthSprites => growthSprites;
        public ItemData HarvestedItem => harvestedItem;
        public float GroundInset => groundInset;

        /// <summary>Index of <paramref name="sprite"/> in the growth sprites, or -1.</summary>
        public int GetStageIndex(Sprite sprite)
        {
            if (sprite == null || growthSprites == null)
                return -1;

            for (int i = 0; i < growthSprites.Length; i++)
            {
                if (growthSprites[i] == sprite)
                    return i;
            }

            return -1;
        }

        public CropStageAnchor GetStageAnchorMode(int stageIndex)
        {
            return stageAnchorModes != null && stageIndex >= 0 && stageIndex < stageAnchorModes.Length
                ? stageAnchorModes[stageIndex]
                : CropStageAnchor.Auto;
        }

        public Vector2 GetStageAnchorPixel(int stageIndex)
        {
            return stageAnchorPixels != null && stageIndex >= 0 && stageIndex < stageAnchorPixels.Length
                ? stageAnchorPixels[stageIndex]
                : Vector2.zero;
        }

        public Vector2 GetStagePositionOffset(int stageIndex)
        {
            return stagePositionOffsets != null && stageIndex >= 0 && stageIndex < stagePositionOffsets.Length
                ? stagePositionOffsets[stageIndex]
                : Vector2.zero;
        }

        public Vector2 GetStagePositionOffset(Sprite sprite)
        {
            return GetStagePositionOffset(GetStageIndex(sprite));
        }

        /// <summary>
        /// Local position of a growth sprite's renderer (scaled by <paramref name="scale"/>)
        /// under a crop root placed at the centre of its soil cell: the stage's
        /// anchor followed by its position offset.
        /// </summary>
        public Vector2 GetStageLocalPosition(Sprite sprite, float scale, float cellHeight)
        {
            int index = GetStageIndex(sprite);
            Vector2 anchored = CropSpriteAlignment.GetAnchoredOffset(
                sprite, scale, cellHeight, GetStageAnchorMode(index), GetStageAnchorPixel(index), groundInset);
            return anchored + GetStagePositionOffset(index);
        }
    }
}
