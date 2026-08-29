using Item.Inventory.Interfaces;
using TMPro;
using UnityEngine;

namespace Item.Inventory
{
    public class ItemAmountDisplayer : MonoBehaviour, ILoadItem
    {
        [SerializeField]
        private TextMeshProUGUI text;

        [SerializeField]
        private ItemData item;

        [SerializeField]
        private int addedLeadingZeroes = 0;

        public void OnItemLoaded(int index, ItemData data, int amount)
        {
            if (data == item)
            {
                text?.SetText(amount.ToString($"D{addedLeadingZeroes}"));
            }
        }
    }
}
