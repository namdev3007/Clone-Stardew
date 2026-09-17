using Event.Events;
using Item;
using UnityEngine;

namespace World
{
    [CreateAssetMenu(fileName = "Special Crop Runtime Config", menuName = "Farming/Special Crop Runtime Config")]
    public sealed class SpecialCropRuntimeConfig : ScriptableObject
    {
        public MapRegionCollection regions;
        public Sprite[] cucumberStages;
        public Sprite[] dragonFruitStages;
        public Sprite[] cucumberBrokenSprites;
        public Sprite lockSign;
        public ItemData currencyItem;
        /// <summary>Spawned when the gameplay scenes do not already contain a confirmation window.</summary>
        public GameObject confirmationWindowPrefab;
        /// <summary>Shared hover event so the lock sign can also be used with the mouse.</summary>
        public InteractionEvent mouseInteractionEvent;
    }
}
