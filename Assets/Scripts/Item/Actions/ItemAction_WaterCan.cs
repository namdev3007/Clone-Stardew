using Entity_Components;
using Entity_Components.Character;
using Entity_Components.Player;
using Item.Inventory;
using Referencing.Scriptable_Reference;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using World;
using World.Objects;

namespace Item.Actions
{
    [CreateAssetMenu(fileName = "Item Action Water Can", menuName = "Items/Item Actions/Water Can")]
    public class ItemAction_WaterCan : ItemAction
    {
        [SerializeField]
        private float speed;

        // Manipulate the tile on this tilemap
        [SerializeField]
        private string targetTilemapName;

        // Only do the action the current tile coordinate has the tile map name
        [SerializeField]
        private string requiredTileMapName;

        // In case the current target is water, which we want to refill
        [SerializeField]
        private string waterTileMapName;

        [SerializeField]
        private ScriptableReference gridManagerReference;

        [SerializeField]
        private float energyCost;

        [SerializeField]
        private float waterEnergyRecovery;

        [SerializeField]
        private UnityEvent OnWateredGround;

        [SerializeField]
        private UnityEvent OnObtainedWater;

        // Cache repeated use cases

        [System.NonSerialized]
        private GridManager gridManager;

        public override IEnumerator ItemUseAction(Inventory.Inventory userInventory, int itemIndex)
        {
            InventoryItem getInventoryItem = userInventory.GetItem(itemIndex);

            GridSelector gridSelector = userInventory.GetComponent<GridSelector>();

            if (gridManager == null)
            {
                gridManager = gridManagerReference.Reference?.GetComponent<GridManager>();
            }

            if (gridSelector != null && gridManager != null)
            {
                Vector3Int location = gridSelector.GetGridSelectionPosition();

                bool obtainingWater = gridManager.HasWater(location);
                Crop targetCrop = gridManager.GetCrop(location);
                bool cropNeedsWater = targetCrop != null && targetCrop.NeedsWater;
                bool dryFarmGround = !gridManager.HasWateredDirt(location) && gridManager.HasDirtHole(location);
                bool canWaterGround = gridManager.HasDirtHole(location) && (cropNeedsWater || dryFarmGround);

                // Validate before starting the animation. Held mouse input can ask
                // to use the can again just after a tile became wet; previously the
                // second request still played the full animation before doing nothing.
                if (!obtainingWater && !canWaterGround)
                    yield break;

                if (!obtainingWater && getInventoryItem != null && !FarmingCheats.InfiniteWater
                    && getInventoryItem.Energy.current < energyCost)
                    yield break;

                Aimer aimer = userInventory.GetComponent<Aimer>();
                aimer?.SetAimDirection(gridSelector.GetMouseLookDirection());

                Mover getMover = userInventory.GetComponent<Mover>();
                getMover?.FreezeMovement(true);

                FullBodyPlayerSpriteAnimator[] getEntityAnimator = userInventory.GetComponentsInChildren<FullBodyPlayerSpriteAnimator>();

                float animationTime = 0;

                for (int i = 0; i < getEntityAnimator.Length; i++)
                {
                    getEntityAnimator[i].PlayAction(FullBodyPlayerSpriteAnimator.ActionType.Water, speed);
                    animationTime = 1 / speed;
                }

                gridSelector.SetFrozen(true);

                yield return new WaitForSeconds(animationTime * 0.5f);

                if (!gridManager.HasWater(location))
                {
                    Crop crop = gridManager.GetCrop(location);
                    cropNeedsWater = crop != null && crop.NeedsWater;
                    dryFarmGround = !gridManager.HasWateredDirt(location) && gridManager.HasDirtHole(location);

                    if (gridManager.HasDirtHole(location) && (cropNeedsWater || dryFarmGround))
                    {
                        ItemEnergy currentItemEnergy = getInventoryItem.Energy;

                        if (FarmingCheats.InfiniteWater || currentItemEnergy.current >= energyCost)
                        {
                            if (!gridManager.HasWateredDirt(location))
                                gridManager.SetWateredDirtTile(location);

                            crop?.TryWater();

                            float newEnergy = FarmingCheats.InfiniteWater
                                ? currentItemEnergy.max
                                : currentItemEnergy.current - energyCost;
                            if (!FarmingCheats.InfiniteWater && newEnergy < energyCost)
                            {
                                newEnergy = 0;
                            }

                            getInventoryItem.Energy = new ItemEnergy()
                            {
                                min = currentItemEnergy.min,
                                max = currentItemEnergy.max,
                                current = newEnergy
                            };

                            OnWateredGround.Invoke();

                            userInventory.ReloadItemSlot(itemIndex);
                        }
                    }
                }
                else
                {
                    ItemEnergy currentItemEnergy = getInventoryItem.Energy;

                    getInventoryItem.Energy = new ItemEnergy()
                    {
                        min = currentItemEnergy.min,
                        max = currentItemEnergy.max,
                        current = Mathf.Clamp(currentItemEnergy.current + waterEnergyRecovery, currentItemEnergy.min, currentItemEnergy.max)
                    };

                    OnObtainedWater.Invoke();

                    userInventory.ReloadItemSlot(itemIndex);
                }

                yield return new WaitForSeconds(animationTime * 0.5f);

                getMover.FreezeMovement(false);
                gridSelector.SetFrozen(false);

            }
        }

        public override bool ItemUseCondition(Inventory.Inventory userInventory, int itemIndex)
        {
            return true;
        }

        public override void ItemActiveAction(Inventory.Inventory userInventory, int itemIndex)
        {
            //userInventory.GetComponent<GridSelector>()?.Display(true);
        }

        public override void ItemUnactiveAction(Inventory.Inventory userInventory, int itemIndex)
        {
            //userInventory.GetComponent<GridSelector>()?.Display(false);
        }
    }
}
