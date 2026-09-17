using System.Globalization;
using Item;
using Item.Inventory;
using TMPro;
using UnityEngine;

namespace User_Interface
{
    /// <summary>
    /// HUD money board: shows how much of the currency item (Gold) the player
    /// carries, formatted with dot thousands separators (e.g. 500.000).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalletDisplay : MonoBehaviour
    {
        private const float RefreshInterval = 0.1f;
        private static readonly NumberFormatInfo DotSeparator = new NumberFormatInfo
        {
            NumberGroupSeparator = ".",
            NumberGroupSizes = new[] { 3 }
        };

        [SerializeField] private ItemData currencyItem;
        [SerializeField] private TextMeshProUGUI amountText;

        private Inventory playerInventory;
        private int shownAmount = -1;
        private float nextRefreshTime;

        /// <summary>Sets the references used by the editor setup tool.</summary>
        public void Configure(ItemData currency, TextMeshProUGUI text)
        {
            currencyItem = currency;
            amountText = text;
            shownAmount = -1;
        }

        /// <summary>Formats a coin amount the way the HUD shows it.</summary>
        public static string FormatAmount(int amount)
        {
            return Mathf.Max(0, amount).ToString("#,0", DotSeparator);
        }

        private void OnEnable()
        {
            shownAmount = -1;
            nextRefreshTime = 0f;
        }

        private void Update()
        {
            // Polled instead of event-driven: gold changes from shop, repairs,
            // passive income and save loading all go through Inventory.
            if (UnityEngine.Time.unscaledTime < nextRefreshTime)
                return;
            nextRefreshTime = UnityEngine.Time.unscaledTime + RefreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            if (amountText == null || currencyItem == null)
                return;

            if (playerInventory == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                playerInventory = player != null ? player.GetComponent<Inventory>() : null;
            }

            int amount = playerInventory != null ? playerInventory.GetItemAmount(currencyItem) : 0;
            if (amount == shownAmount)
                return;

            shownAmount = amount;
            amountText.text = FormatAmount(amount);
        }
    }
}
