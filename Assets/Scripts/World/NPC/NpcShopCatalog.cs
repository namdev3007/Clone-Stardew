using System;
using System.Collections.Generic;
using Item;
using UnityEngine;

namespace World.NPC
{
    [CreateAssetMenu(fileName = "NPC Shop Catalog", menuName = "NPC/Shop Catalog")]
    public sealed class NpcShopCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public ItemData item;
            public int futurePrice;
            public bool visible = true;
            [Tooltip("-1 means always available. Seed crops use 0..7 in progression order.")]
            public int cropUnlockOrder = -1;
            public SpecialCropAreaId requiredSpecialArea;
            public bool requiresHomeOrchard;

            public bool IsUnlocked(int highestUnlockedCropOrder)
            {
                bool cropOrderUnlocked = requiresHomeOrchard || cropUnlockOrder < 0 ||
                                         cropUnlockOrder <= highestUnlockedCropOrder;
                bool specialAreaUnlocked = requiredSpecialArea == SpecialCropAreaId.None ||
                    (SpecialCropProgressService.Instance != null &&
                     SpecialCropProgressService.Instance.IsRepaired(requiredSpecialArea));
                bool farmExpansionUnlocked = !requiresHomeOrchard || FarmExpansionRuntime.HomeOrchardUnlocked;
                return cropOrderUnlocked && specialAreaUnlocked && farmExpansionUnlocked;
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => entries;

        public void Configure(IEnumerable<ItemData> items)
        {
            entries.Clear();
            if (items == null)
                return;

            foreach (ItemData item in items)
            {
                if (item != null)
                    entries.Add(new Entry
                    {
                        item = item,
                        futurePrice = 0,
                        visible = true,
                        cropUnlockOrder = GetCropUnlockOrder(item.ItemName),
                        requiredSpecialArea = item.ItemName == "Cucumber Seed"
                            ? SpecialCropAreaId.CucumberTrellis
                            : item.ItemName == "Dragon Fruit Seed"
                                ? SpecialCropAreaId.DragonFruitTrellis
                                : SpecialCropAreaId.None,
                        requiresHomeOrchard = item.ItemName == "Banana Seed" || item.ItemName == "Mango Seed"
                    });
            }
        }

        public static int GetCropUnlockOrder(string itemName)
        {
            switch (itemName)
            {
                case "Carrot Seed": return 0;
                case "Onion Seed": return 1;
                case "Garlic Seed": return 2;
                case "Cabbage Seed": return 3;
                case "Potato Seed": return 4;
                case "Tomato Seed": return 5;
                case "Banana Seed": return 6;
                case "Mango Seed": return 7;
                default: return -1;
            }
        }
    }
}
