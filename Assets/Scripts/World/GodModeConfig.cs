using System.Collections.Generic;
using Item;
using UnityEngine;

namespace World
{
    [CreateAssetMenu(fileName = "God Mode Config", menuName = "Farming/God Mode Config")]
    public sealed class GodModeConfig : ScriptableObject
    {
        [SerializeField] private List<ItemData> seedItems = new List<ItemData>();
        [SerializeField] private ItemData fertilizerItem;
        [SerializeField] private float stageSeconds = 1.0f;

        public IReadOnlyList<ItemData> SeedItems => seedItems;
        public ItemData FertilizerItem => fertilizerItem;
        public float StageSeconds => Mathf.Max(0.1f, stageSeconds);
    }
}
