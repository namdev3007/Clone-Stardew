using Item.Inventory.Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace Item.Inventory
{
    /// <summary>
    /// In case you would like to have the calls on the slot directly without this component:
    /// Add all the interfaces on this class to the item slot.
    /// </summary>
    public class InventorySlotCollection : MonoBehaviour, ILoadItem, IUseItem, IRemoveItem, ISelectItem, IInventoryLoaded
    {
        private Dictionary<int, InventorySlot> slots = new Dictionary<int, InventorySlot>();
        private Inventory inventory;
        private DynamicBagSlotGrid dynamicBagGrid;

        public void OnInventoryLoaded(Inventory inventory)
        {
            if (this.inventory != inventory)
            {
                if (this.inventory != null)
                    this.inventory.InventorySizeChanged -= OnInventorySizeChanged;

                this.inventory = inventory;
                if (this.inventory != null)
                    this.inventory.InventorySizeChanged += OnInventorySizeChanged;
            }

            if (dynamicBagGrid == null)
                dynamicBagGrid = GetComponent<DynamicBagSlotGrid>();
            if (dynamicBagGrid == null && transform.Find("Bag Slots") != null)
                dynamicBagGrid = gameObject.AddComponent<DynamicBagSlotGrid>();

            dynamicBagGrid?.EnsureCapacity(inventory.InventorySize);
            RebuildSlotLookup();

            foreach (InventorySlot slot in slots.Values)
            {
                slot.OnInventoryInitialized(inventory);
            }
        }

        private void OnDestroy()
        {
            if (inventory != null)
                inventory.InventorySizeChanged -= OnInventorySizeChanged;
        }

        private void OnInventorySizeChanged(int newSize)
        {
            dynamicBagGrid?.EnsureCapacity(newSize);
            RebuildSlotLookup();

            foreach (InventorySlot slot in slots.Values)
                slot.OnInventoryInitialized(inventory);

            inventory?.ReloadAllItemSlots();
        }

        private void RebuildSlotLookup()
        {
            InventorySlot[] getSlots = GetComponentsInChildren<InventorySlot>(true);
            slots.Clear();

            for (int i = 0; i < getSlots.Length; i++)
            {
                int slotIndex = getSlots[i].GetSlotIndex();
                slots[slotIndex] = getSlots[i];
            }

        }

        public void OnItemLoaded(int index, ItemData data, int amount)
        {
            if (!data.HasSlot)
                return;

            GetSlot(index)?.OnItemLoaded(index, data, amount);
        }

        public void OnItemSelect(int index, bool selected)
        {
            GetSlot(index)?.OnItemSelect(index, selected);
        }

        public void OnRemoveItem(int index)
        {
            GetSlot(index)?.OnRemoveItem(index);
        }

        public void OnUseItem(int index, ItemData data, int amount)
        {
            GetSlot(index)?.OnUseItem(index, data, amount);
        }

        private InventorySlot GetSlot(int index)
        {
            InventorySlot slot;
            slots.TryGetValue(index, out slot);
            return slot;
        }
    }
}
