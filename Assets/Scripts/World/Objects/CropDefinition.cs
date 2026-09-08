using Item;
using Referencing.Scriptable_Assets;
using UnityEngine;

namespace World.Objects
{
    [CreateAssetMenu(fileName = "Crop Definition", menuName = "Farming/Crop Definition")]
    public class CropDefinition : ScriptableAsset
    {
        [SerializeField] private string displayName;
        [SerializeField] private float firstGrowthSeconds = 15f;
        [SerializeField] private float regrowthSeconds;
        [SerializeField] private int maximumHarvests = 1;
        [SerializeField] private int regrowthStageStart;
        [SerializeField, Tooltip("Perennial trees are planted directly on hoeable ground and reserve a 3x3 area.")]
        private bool perennialTree;
        [SerializeField] private Sprite[] growthSprites;
        [SerializeField, Tooltip("Local X/Y correction for each growth sprite, using the same array index.")]
        private Vector2[] stagePositionOffsets;
        [SerializeField] private ItemData harvestedItem;

        public string DisplayName => displayName;
        public float FirstGrowthSeconds => Mathf.Max(0.1f, firstGrowthSeconds);
        public float RegrowthSeconds => Mathf.Max(0.1f, regrowthSeconds);
        public int MaximumHarvests => Mathf.Max(1, maximumHarvests);
        public int RegrowthStageStart => Mathf.Clamp(regrowthStageStart, 0, Mathf.Max(0, growthSprites.Length - 1));
        public bool IsPerennialTree => perennialTree;
        public Sprite[] GrowthSprites => growthSprites;
        public ItemData HarvestedItem => harvestedItem;

        public Vector2 GetStagePositionOffset(Sprite sprite)
        {
            if (sprite == null || growthSprites == null || stagePositionOffsets == null)
                return Vector2.zero;

            int count = Mathf.Min(growthSprites.Length, stagePositionOffsets.Length);
            for (int i = 0; i < count; i++)
            {
                if (growthSprites[i] == sprite)
                    return stagePositionOffsets[i];
            }

            return Vector2.zero;
        }
    }
}
