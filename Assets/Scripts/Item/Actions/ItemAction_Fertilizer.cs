using Entity_Components.Player;
using Item.Inventory;
using Referencing.Scriptable_Reference;
using System.Collections;
using UnityEngine;
using World;

namespace Item.Actions
{
    [CreateAssetMenu(fileName = "Item Action Fertilizer", menuName = "Items/Item Actions/Fertilizer")]
    public class ItemAction_Fertilizer : ItemAction
    {
        [SerializeField] private ScriptableReference gridManagerReference;

        [System.NonSerialized] private GridManager gridManager;

        public override IEnumerator ItemUseAction(Inventory.Inventory userInventory, int itemIndex)
        {
            yield return null;

            InventoryItem item = userInventory.GetItem(itemIndex);
            GridSelector selector = userInventory.GetComponent<GridSelector>();
            if (item == null || item.Energy.current < 1f || selector == null)
                yield break;

            if (gridManager == null)
                gridManager = gridManagerReference.Reference?.GetComponent<GridManager>() ?? selector.GetGridManager();

            if (gridManager == null || !gridManager.TryFertilizePlot(selector.GetGridSelectionPosition()))
                yield break;

            ItemEnergy energy = item.Energy;
            energy.current = Mathf.Max(energy.min, energy.current - 1f);
            item.Energy = energy;

            if (item.Energy.current <= item.Energy.min)
                userInventory.RemoveItem(itemIndex);
            else
                userInventory.ReloadItemSlot(itemIndex);
        }

        public override bool ItemUseCondition(Inventory.Inventory userInventory, int itemIndex)
        {
            InventoryItem item = userInventory.GetItem(itemIndex);
            return item != null && item.Energy.current >= 1f;
        }
    }
}
