using Entity_Components.Player;
using Item.Inventory;
using Referencing;
using Referencing.Scriptable_Reference;
using System.Collections;
using UnityEngine;
using World;
using World.Objects;

namespace Item.Actions
{
    [CreateAssetMenu(fileName = "Item Action Plant Seed", menuName = "Items/Item Actions/Plant Seed")]
    public class ItemAction_PlantSeed : ItemAction
    {
        [SerializeField]
        private SaveablePrefab plantablePrefab;

        [SerializeField]
        private ScriptableReference gridManagerReference;

        [SerializeField]
        private CropDefinition cropDefinition;

        private GridManager gridManager;

        public int ShowcaseStageCount => cropDefinition != null && cropDefinition.GrowthSprites != null
            ? cropDefinition.GrowthSprites.Length
            : 0;
        public bool UsesPerennialFootprint => cropDefinition != null && cropDefinition.IsPerennialTree;

        public bool CheatPlantShowcase(GridManager targetGridManager, Vector3Int location, int stageIndex)
        {
            if (targetGridManager == null || cropDefinition == null || plantablePrefab == null)
                return false;

            if (!targetGridManager.CheatBeginShowcasePlanting(location, cropDefinition, out bool fertilized))
                return false;

            GameObject cropObject = plantablePrefab.Retrieve<GameObject>(scene: targetGridManager.gameObject.scene);
            if (cropObject == null)
            {
                targetGridManager.CancelPlanting(location);
                return false;
            }

            cropObject.transform.position = targetGridManager.GetWorldLocation(location);
            Crop crop = cropObject.GetComponent<Crop>();
            if (crop == null)
            {
                targetGridManager.CancelPlanting(location);
                cropObject.SetActive(false);
                return false;
            }

            bool harvestableDemo = stageIndex == ShowcaseStageCount - 1;
            crop.ConfigureShowcase(cropDefinition, stageIndex, harvestableDemo);
            return true;
        }

        public override IEnumerator ItemUseAction(Inventory.Inventory userInventory, int itemIndex)
        {
            yield return null;

            InventoryItem getItem = userInventory.GetItem(itemIndex);

            if (gridManager == null)
            {
                gridManager = gridManagerReference.Reference.GetComponent<GridManager>();
            }

            if (getItem != null && getItem.Amount > 0 && PlantAction(userInventory))
            {
                getItem.Amount -= 1;

                if (getItem.Amount <= 0)
                {
                    userInventory.RemoveItem(itemIndex);
                }
                else
                {
                    userInventory.ReloadItemSlot(itemIndex);
                }
            }
        }

        private bool PlantAction(Inventory.Inventory userInventory)
        {
            GridSelector gridSelector = userInventory.GetComponent<GridSelector>();
            Vector3Int selectionLocation = gridSelector.GetGridSelectionPosition();

            if (cropDefinition != null && gridManager.TryBeginPlanting(selectionLocation, cropDefinition, out bool fertilized))
            {
                var targetScene = gridManager.gameObject.scene;
                GameObject gameObject = plantablePrefab.Retrieve<GameObject>(scene: targetScene);
                if (gameObject == null)
                {
                    gridManager.CancelPlanting(selectionLocation);
                    return false;
                }

                gameObject.transform.position = gridManager.GetWorldLocation(selectionLocation);
                Crop crop = gameObject.GetComponent<Crop>();
                if (crop == null)
                {
                    gridManager.CancelPlanting(selectionLocation);
                    gameObject.SetActive(false);
                    return false;
                }

                crop.Configure(cropDefinition, fertilized);
                return true;
            }
            else
            {
                return false;
            }
        }

        public override bool ItemUseCondition(Inventory.Inventory userInventory, int itemIndex)
        {
            return userInventory.GetItem(itemIndex)?.Amount > 0;
        }
    }
}
