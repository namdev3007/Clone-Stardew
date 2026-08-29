
namespace Item.Inventory.Interfaces
{
    public interface IDropItem
    {
        void OnDropItem(int index, LootableItem lootable);
    }
}