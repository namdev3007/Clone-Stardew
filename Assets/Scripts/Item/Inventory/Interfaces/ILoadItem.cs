
namespace Item.Inventory.Interfaces
{
    public interface ILoadItem
    {
        void OnItemLoaded(int index, ItemData data, int amount);
    }
}
