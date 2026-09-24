using System;
using System.Collections.Generic;
using Item;
using Item.Inventory;
using UnityEngine;

namespace World
{
    /// <summary>Runtime switches and helpers for developer cheats.</summary>
    public static class FarmingCheats
    {
        private static bool godMode;
        private static GodModeConfig cachedConfig;

        public const float GodModeStageSeconds = 1.0f;
        public static event Action<bool> OnGodModeChanged;

        public static bool InfiniteWater { get; set; }

        public static bool GodMode
        {
            get => godMode;
            set
            {
                if (godMode == value)
                    return;

                godMode = value;
                if (godMode)
                {
                    InfiniteWater = true;
                }

                OnGodModeChanged?.Invoke(godMode);
            }
        }

        public static GodModeConfig GetConfig()
        {
            if (cachedConfig == null)
            {
                cachedConfig = Resources.Load<GodModeConfig>("Farming/God Mode Config");
            }
            return cachedConfig;
        }

        /// <summary>
        /// Supplies or tops up the player's inventory with the specified amount (default: 100)
        /// of every crop seed type in the game, as well as fertilizer.
        /// </summary>
        public static int GiveAllSeeds(Inventory playerInventory, int amount = 100)
        {
            if (playerInventory == null)
                return 0;

            GodModeConfig config = GetConfig();
            int totalItemsUpdated = 0;

            if (config != null && config.SeedItems != null)
            {
                foreach (ItemData seed in config.SeedItems)
                {
                    if (seed == null)
                        continue;

                    SupplyItem(playerInventory, seed, amount);
                    totalItemsUpdated++;
                }

                if (config.FertilizerItem != null)
                {
                    SupplyItem(playerInventory, config.FertilizerItem, amount);
                    totalItemsUpdated++;
                }
            }
            else
            {
                // Fallback: If config asset could not be loaded, find all loaded ItemData seed assets
                ItemData[] allItems = Resources.FindObjectsOfTypeAll<ItemData>();
                HashSet<string> addedGuids = new HashSet<string>();
                foreach (ItemData item in allItems)
                {
                    if (item == null || string.IsNullOrEmpty(item.ItemName))
                        continue;

                    if (item.ItemName.IndexOf("Seed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        item.ItemName.IndexOf("Fertilizer", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (addedGuids.Add(item.GetGuid()))
                        {
                            SupplyItem(playerInventory, item, amount);
                            totalItemsUpdated++;
                        }
                    }
                }
            }

            playerInventory.ReloadAllItemSlots();
            return totalItemsUpdated;
        }

        private static void SupplyItem(Inventory inventory, ItemData item, int targetAmount)
        {
            InventoryItem existing = inventory.GetItem(item, out int slotIndex);
            if (existing != null)
            {
                if (existing.Amount < targetAmount)
                {
                    existing.Amount = targetAmount;
                }
            }
            else
            {
                inventory.AddItem(item, targetAmount);
            }
        }
    }
}
