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
                    entries.Add(new Entry { item = item, futurePrice = 0, visible = true });
            }
        }
    }
}
