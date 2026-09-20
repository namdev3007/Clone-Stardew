using System.Globalization;
using Item;
using Item.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using World;
using World.NPC;

namespace User_Interface
{
    /// <summary>
    /// HUD money board: shows how much of the currency item (Gold) the player
    /// carries, formatted with dot thousands separators (e.g. 500.000).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalletDisplay : MonoBehaviour, IPointerClickHandler
    {
        private const float RefreshInterval = 0.1f;
        private const float CheatClickWindow = 2.5f;
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
        private int cheatClickCount;
        private float lastCheatClickTime;

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
            cheatClickCount = 0;

            // The money board used to be display-only. It now owns the hidden
            // five-click developer shortcut, so its graphic must receive clicks.
            Image image = GetComponent<Image>();
            if (image != null)
                image.raycastTarget = true;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
                return;

            float now = UnityEngine.Time.unscaledTime;
            if (now - lastCheatClickTime > CheatClickWindow)
                cheatClickCount = 0;

            lastCheatClickTime = now;
            cheatClickCount++;
            if (cheatClickCount < 5)
                return;

            cheatClickCount = 0;
            RuntimeCheatPanel.Show(currencyItem, amountText != null ? amountText.font : null);
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

    /// <summary>
    /// Lightweight runtime-only developer menu. It is intentionally created in
    /// code so the cheat UI never needs to be authored into or saved with a scene.
    /// </summary>
    public sealed class RuntimeCheatPanel : MonoBehaviour
    {
        private static RuntimeCheatPanel instance;

        private ItemData currencyItem;
        private Inventory playerInventory;
        private TMP_FontAsset uiFont;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI infiniteWaterButtonText;

        public static bool AnyOpen => instance != null && instance.gameObject.activeInHierarchy;

        public static void Show(ItemData currency, TMP_FontAsset font)
        {
            if (instance == null)
                instance = CreatePanel();

            instance.currencyItem = currency;
            instance.uiFont = font;
            instance.ApplyFont();
            instance.ResolvePlayerInventory();
            instance.SetStatus("Chế độ kiểm thử - thay đổi được lưu vào save hiện tại.");
            instance.RefreshToggleLabels();
            instance.gameObject.SetActive(true);
        }

        public static bool TryCloseOpen()
        {
            if (!AnyOpen)
                return false;

            instance.gameObject.SetActive(false);
            return true;
        }

        private static RuntimeCheatPanel CreatePanel()
        {
            GameObject root = new GameObject("Runtime Cheat Tool", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RuntimeCheatPanel));
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RuntimeCheatPanel panel = root.GetComponent<RuntimeCheatPanel>();
            panel.BuildVisuals();
            root.SetActive(false);
            return panel;
        }

        private void BuildVisuals()
        {
            Image blocker = CreateImage("Screen Blocker", transform, new Color(0f, 0f, 0f, 0.62f));
            Stretch(blocker.rectTransform);

            Image panel = CreateImage("Panel", blocker.transform, new Color32(86, 48, 25, 255));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 690f);
            panelRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI title = CreateText("Title", panel.transform, "CHEAT TOOL", 40f,
                new Color32(247, 202, 146, 255));
            SetRect(title.rectTransform, new Vector2(0f, -45f), new Vector2(700f, 60f));
            title.fontStyle = FontStyles.Bold;

            AddButton(panel.transform, "+ 100 Đ", new Vector2(-185f, -125f), () => AddMoney(100));
            AddButton(panel.transform, "+ 1.000 Đ", new Vector2(185f, -125f), () => AddMoney(1000));
            AddButton(panel.transform, "Mở toàn bộ hạt giống", new Vector2(-185f, -200f), UnlockAllSeeds);
            AddButton(panel.transform, "Mở toàn bộ ruộng", new Vector2(185f, -200f), UnlockAllFields);
            AddButton(panel.transform, "Cuốc toàn bộ đất", new Vector2(-185f, -275f), HoeAllDirt);
            AddButton(panel.transform, "Tưới toàn bộ ruộng", new Vector2(185f, -275f), WaterAllPlots);
            AddButton(panel.transform, "Cho cây lớn ngay", new Vector2(-185f, -350f), GrowAllCrops);
            AddButton(panel.transform, "Thu hoạch cây chín", new Vector2(185f, -350f), HarvestAllCrops);
            infiniteWaterButtonText = AddButton(panel.transform, "", new Vector2(-185f, -425f), ToggleInfiniteWater);
            AddButton(panel.transform, "Làm mới toàn bộ đất", new Vector2(185f, -425f), ResetFarmLand);

            statusText = CreateText("Status", panel.transform, "", 23f, Color.white);
            SetRect(statusText.rectTransform, new Vector2(0f, -515f), new Vector2(700f, 70f));
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 16f;
            statusText.fontSizeMax = 23f;

            AddButton(panel.transform, "ĐÓNG (ESC)", new Vector2(0f, -610f), Close, new Vector2(330f, 58f));
        }

        private TextMeshProUGUI AddButton(Transform parent, string label, Vector2 position,
            UnityEngine.Events.UnityAction action, Vector2? size = null)
        {
            Image image = CreateImage(label, parent, new Color32(169, 99, 48, 255));
            RectTransform rect = image.rectTransform;
            SetRect(rect, position, size ?? new Vector2(340f, 58f));

            Button button = image.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(action);

            TextMeshProUGUI text = CreateText("Label", image.transform, label, 25f,
                new Color32(255, 224, 177, 255));
            Stretch(text.rectTransform, 10f);
            text.fontStyle = FontStyles.Bold;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.transform.SetParent(parent, false);
            Image image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, string content, float fontSize, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            child.transform.SetParent(parent, false);
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            if (uiFont != null)
                text.font = uiFont;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private void ApplyFont()
        {
            if (uiFont == null)
                return;
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
                labels[i].font = uiFont;
        }

        private void ResolvePlayerInventory()
        {
            if (playerInventory != null)
                return;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            playerInventory = player != null ? player.GetComponent<Inventory>() : null;
        }

        private GridManager ResolveGrid()
        {
            return FindFirstObjectByType<GridManager>();
        }

        private void AddMoney(int amount)
        {
            ResolvePlayerInventory();
            if (playerInventory == null || currencyItem == null)
            {
                SetStatus("Không tìm thấy túi đồ hoặc loại tiền của người chơi.");
                return;
            }

            bool added = playerInventory.AddItem(currencyItem, amount);
            SetStatus(added ? "Đã cộng " + WalletDisplay.FormatAmount(amount) + " Đ."
                : "Không thể cộng tiền vào túi đồ.");
        }

        private void UnlockAllSeeds()
        {
            TutorialProgressService.Instance.CheatUnlockAllCrops();
            // Cucumber, dragon fruit, banana and mango are also gated by land
            // progression, so unlock their areas to make every seed purchasable.
            UnlockAllFieldsInternal();
            SetStatus("Đã mở toàn bộ hạt giống trong shop.");
        }

        private void UnlockAllFields()
        {
            UnlockAllFieldsInternal();
            SetStatus("Đã mở ruộng dưa chuột, thanh long và vườn quanh nhà.");
        }

        private static void UnlockAllFieldsInternal()
        {
            SpecialCropProgressService progress = SpecialCropProgressService.Instance;
            if (progress == null)
                progress = FindFirstObjectByType<SpecialCropProgressService>();
            if (progress == null)
                return;

            progress.MarkRepaired(SpecialCropAreaId.CucumberTrellis);
            progress.MarkRepaired(SpecialCropAreaId.DragonFruitTrellis);
            progress.UnlockHomeOrchard();
        }

        private void HoeAllDirt()
        {
            GridManager grid = ResolveGrid();
            SetStatus(grid != null ? "Đã cuốc " + grid.CheatHoeAllDirt() + " ô đất."
                : "Không tìm thấy Grid Manager.");
        }

        private void WaterAllPlots()
        {
            GridManager grid = ResolveGrid();
            SetStatus(grid != null ? "Đã tưới " + grid.CheatWaterAllPlots() + " ô đất."
                : "Không tìm thấy Grid Manager.");
        }

        private void GrowAllCrops()
        {
            GridManager grid = ResolveGrid();
            SetStatus(grid != null ? "Đã cho " + grid.CheatGrowAllCrops() + " cây lớn ngay."
                : "Không tìm thấy Grid Manager.");
        }

        private void HarvestAllCrops()
        {
            GridManager grid = ResolveGrid();
            SetStatus(grid != null ? "Đã thu hoạch " + grid.CheatHarvestAllCrops() + " cây chín."
                : "Không tìm thấy Grid Manager.");
        }

        private void ResetFarmLand()
        {
            GridManager grid = ResolveGrid();
            SetStatus(grid != null ? "Đã làm mới " + grid.CheatResetAllFarmLand() + " ô đất."
                : "Không tìm thấy Grid Manager.");
        }

        private void ToggleInfiniteWater()
        {
            FarmingCheats.InfiniteWater = !FarmingCheats.InfiniteWater;
            RefreshToggleLabels();
            SetStatus(FarmingCheats.InfiniteWater ? "Đã bật nước vô hạn." : "Đã tắt nước vô hạn.");
        }

        private void RefreshToggleLabels()
        {
            if (infiniteWaterButtonText != null)
                infiniteWaterButtonText.text = FarmingCheats.InfiniteWater
                    ? "Nước vô hạn: BẬT"
                    : "Nước vô hạn: TẮT";
        }

        private void SetStatus(string message)
        {
            if (statusText != null)
                statusText.text = message;
        }

        private void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
