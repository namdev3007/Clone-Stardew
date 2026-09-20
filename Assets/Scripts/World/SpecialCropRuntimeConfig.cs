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
        public Sprite[] dragonFruitBrokenSprites;
        /// <summary>
        /// Intact cucumber trellis row shown once the area is repaired (one per
        /// post row). The dragon fruit field has no trellis, only posts.
        /// </summary>
        public Sprite cucumberRepairedFence;
        [Header("Cucumber repaired trellis rows (world coordinates)")]
        public Vector2 cucumberUpperFencePosition;
        public Vector2 cucumberLowerFencePosition;
        public Vector2 cucumberFenceScale = Vector2.one;
        public Sprite lockSign;
        public ItemData currencyItem;
        /// <summary>Spawned when the gameplay scenes do not already contain a confirmation window.</summary>
        public GameObject confirmationWindowPrefab;
        [Header("Repair dialog (same art as the pause menu's quit confirmation)")]
        public Sprite dialogPanel;
        public Sprite dialogYesButton;
        public Sprite dialogNoButton;
        public TMPro.TMP_FontAsset dialogFont;
        /// <summary>Shared hover event so the lock sign can also be used with the mouse.</summary>
        public InteractionEvent mouseInteractionEvent;
    }
}
