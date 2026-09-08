using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Item.Inventory
{
    public class BagItemDescriptionPanel : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI itemName;
        [SerializeField] private TextMeshProUGUI amount;
        [SerializeField] private TextMeshProUGUI description;

        private int pinnedSlot = -1;
        private InventoryItem pinnedItem;

        private void Awake()
        {
            if (icon != null && itemName != null && amount != null && description != null)
                Clear();
        }

        public void Configure(Image iconImage, TextMeshProUGUI nameText, TextMeshProUGUI amountText, TextMeshProUGUI descriptionText)
        {
            icon = iconImage;
            itemName = nameText;
            amount = amountText;
            description = descriptionText;
            Clear();
        }

        public void Show(int slotIndex, InventoryItem item, bool pin)
        {
            if (pin)
            {
                pinnedSlot = slotIndex;
                pinnedItem = item;
            }

            bool hasItem = item != null && item.Data != null;
            icon.gameObject.SetActive(hasItem && item.Data.Icon != null);
            icon.sprite = hasItem ? item.Data.Icon : null;
            itemName.text = hasItem ? item.Data.ItemName : string.Empty;
            amount.text = hasItem && item.Data.CanStack ? $"x{item.Amount}" : string.Empty;
            description.text = hasItem ? item.Data.Description : string.Empty;
        }

        public void ClearIfNotPinned(int slotIndex)
        {
            if (pinnedSlot >= 0)
                Show(pinnedSlot, pinnedItem, false);
            else
                ClearVisuals();
        }

        public void Clear()
        {
            pinnedSlot = -1;
            pinnedItem = null;
            ClearVisuals();
        }

        private void ClearVisuals()
        {
            if (icon == null || itemName == null || amount == null || description == null)
                return;
            icon.gameObject.SetActive(false);
            icon.sprite = null;
            itemName.text = string.Empty;
            amount.text = string.Empty;
            description.text = string.Empty;
        }
    }
}
