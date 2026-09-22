using System;
using System.Collections.Generic;
using Item;
using Item.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace World.NPC
{
    [DisallowMultipleComponent]
    public sealed class ShopWindowController : MonoBehaviour
    {
        private static ShopWindowController openWindow;

        [Serializable]
        public sealed class SlotView
        {
            public Button button;
            public Image background;
            public Image icon;
            public TextMeshProUGUI primaryText;
            public TextMeshProUGUI secondaryText;
        }

        private const int EntriesPerPage = 3;
        private const int BagSlotsPerRow = 7;
        private const float BagSlotSize = 22f;
        private const float BagSlotSpacing = 3f;
        // The shop reuses the same inventory order as the main bag:
        // quick slots first, followed by the regular bag slots.
        private const int FirstVisibleInventoryIndex = 0;
        private static readonly Color DisabledTransactionColor = new Color32(95, 95, 95, 255);

        [Header("Data")]
        [SerializeField] private NpcShopCatalog defaultCatalog;
        [SerializeField] private ItemData currencyItem;

        [Header("Slots")]
        [SerializeField] private SlotView[] shopSlots = new SlotView[EntriesPerPage];
        [SerializeField] private SlotView[] bagSlots = new SlotView[14];
        [SerializeField] private Sprite shopNormal;
        [SerializeField] private Sprite shopSelected;
        [SerializeField] private Sprite bagNormal;
        [SerializeField] private Sprite bagSelected;

        [Header("Details")]
        [SerializeField] private Image previewIcon;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemPriceText;
        [SerializeField] private TextMeshProUGUI quantityText;
        [SerializeField] private TextMeshProUGUI pageText;

        [Header("Controls")]
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button buyButton;
        [SerializeField] private Button sellButton;
        [SerializeField] private Button backButton;

        private readonly List<NpcShopCatalog.Entry> visibleEntries = new List<NpcShopCatalog.Entry>();
        private NpcShopCatalog activeCatalog;
        private Inventory playerInventory;
        private System.Action onClosed;
        private int pageIndex;
        private int selectedShopIndex = -1;
        private int selectedBagIndex = -1;
        private int quantity = 1;
        private bool buttonsBound;
        private bool openedExplicitly;
        private RectTransform bagScrollViewport;
        private RectTransform bagScrollContent;
        private SlotView bagSlotTemplate;
        private Image draggedBagIcon;
        private int draggedBagIndex = -1;

        public bool IsOpen => gameObject.activeSelf;
        public static bool AnyOpen => openWindow != null && openWindow.gameObject.activeSelf;

        public static bool TryCloseOpen()
        {
            ShopWindowController shop = openWindow;
            if (shop == null || !shop.gameObject.activeSelf)
                shop = FindFirstObjectByType<ShopWindowController>(FindObjectsInactive.Include);
            if (shop == null || !shop.gameObject.activeSelf || !shop.openedExplicitly)
                return false;

            shop.Close();
            return true;
        }

        private void Awake()
        {
            ResolveMissingControls();
            BindButtons();
        }

        private void Update()
        {
            if (!openedExplicitly)
                return;

            float scroll = Input.mouseScrollDelta.y;
            if (bagScrollViewport != null &&
                RectTransformUtility.RectangleContainsScreenPoint(bagScrollViewport, Input.mousePosition))
                return;

            if (scroll > 0.01f)
                PreviousPage();
            else if (scroll < -0.01f)
                NextPage();
        }

        private void Start()
        {
            // Keep the authored window visible in Edit Mode for layout work, but do
            // not cover gameplay until Uncle Hai's Browse Shop action opens it.
            if (!openedExplicitly)
                gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            ClearDraggedBagIcon();
            UnsubscribeFromInventorySize();
            if (openWindow == this)
                openWindow = null;
        }

        public void Configure(
            NpcShopCatalog catalog,
            ItemData authoredCurrencyItem,
            SlotView[] authoredShopSlots,
            SlotView[] authoredBagSlots,
            Sprite normalShopSprite,
            Sprite selectedShopSprite,
            Sprite normalBagSprite,
            Sprite selectedBagSprite,
            Image authoredPreviewIcon,
            TextMeshProUGUI authoredItemName,
            TextMeshProUGUI authoredItemPrice,
            TextMeshProUGUI authoredQuantity,
            TextMeshProUGUI authoredPage,
            Button authoredPrevious,
            Button authoredNext,
            Button authoredMinus,
            Button authoredPlus,
            Button authoredBuy,
            Button authoredSell,
            Button authoredBack)
        {
            defaultCatalog = catalog;
            currencyItem = authoredCurrencyItem;
            shopSlots = authoredShopSlots;
            bagSlots = authoredBagSlots;
            shopNormal = normalShopSprite;
            shopSelected = selectedShopSprite;
            bagNormal = normalBagSprite;
            bagSelected = selectedBagSprite;
            previewIcon = authoredPreviewIcon;
            itemNameText = authoredItemName;
            itemPriceText = authoredItemPrice;
            quantityText = authoredQuantity;
            pageText = authoredPage;
            previousPageButton = authoredPrevious;
            nextPageButton = authoredNext;
            minusButton = authoredMinus;
            plusButton = authoredPlus;
            buyButton = authoredBuy;
            sellButton = authoredSell;
            backButton = authoredBack;
        }

        public void Open(NpcShopCatalog catalog, System.Action closed)
        {
            openedExplicitly = true;
            openWindow = this;
            activeCatalog = catalog != null ? catalog : defaultCatalog;
            onClosed = closed;
            pageIndex = 0;
            selectedShopIndex = -1;
            selectedBagIndex = -1;
            quantity = 1;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            playerInventory = player != null ? player.GetComponent<Inventory>() : null;

            gameObject.SetActive(true);
            SubscribeToInventorySize();
            EnsureDynamicBagSlots(playerInventory != null ? playerInventory.InventorySize : bagSlots.Length);
            BindButtons();
            RebuildCatalog();
            RefreshBagSlots();

            if (visibleEntries.Count > 0)
                SelectShopSlot(0);
            else
                RefreshSelection();
        }

        public void Close()
        {
            UnsubscribeFromInventorySize();
            openedExplicitly = false;
            if (openWindow == this)
                openWindow = null;
            gameObject.SetActive(false);
            System.Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        public void CloseSilently()
        {
            UnsubscribeFromInventorySize();
            openedExplicitly = false;
            if (openWindow == this)
                openWindow = null;
            onClosed = null;
            gameObject.SetActive(false);
        }

        public void ApplyEditorPreview()
        {
            activeCatalog = defaultCatalog;
            pageIndex = 0;
            selectedShopIndex = 0;
            selectedBagIndex = -1;
            quantity = 1;
            RebuildCatalog();
            RefreshBagSlots();
            RefreshSelection();
        }

        private void BindButtons()
        {
            if (buttonsBound)
                return;

            for (int i = 0; i < shopSlots.Length; i++)
            {
                int captured = i;
                shopSlots[i]?.button?.onClick.AddListener(() => SelectShopSlot(captured));
            }

            for (int i = 0; i < bagSlots.Length; i++)
            {
                int captured = i;
                bagSlots[i]?.button?.onClick.AddListener(() => SelectBagSlot(captured));
            }

            previousPageButton?.onClick.AddListener(PreviousPage);
            nextPageButton?.onClick.AddListener(NextPage);
            minusButton?.onClick.AddListener(() => ChangeQuantity(-1));
            plusButton?.onClick.AddListener(() => ChangeQuantity(1));
            backButton?.onClick.AddListener(Close);

            buyButton?.onClick.AddListener(BuySelectedItem);
            sellButton?.onClick.AddListener(SellSelectedItem);
            buttonsBound = true;
        }

        private void SubscribeToInventorySize()
        {
            if (playerInventory == null)
                return;
            playerInventory.InventorySizeChanged -= OnInventorySizeChanged;
            playerInventory.InventorySizeChanged += OnInventorySizeChanged;
        }

        private void UnsubscribeFromInventorySize()
        {
            if (playerInventory != null)
                playerInventory.InventorySizeChanged -= OnInventorySizeChanged;
        }

        private void OnInventorySizeChanged(int newSize)
        {
            EnsureDynamicBagSlots(newSize);
            RefreshBagSlots();
        }

        /// <summary>
        /// The shop shows the same complete slot list as the player inventory.
        /// It keeps the authored 7-column look, adds rows as capacity grows, and
        /// clips them inside a two-row vertical scroll view.
        /// </summary>
        private void EnsureDynamicBagSlots(int requiredSlots)
        {
            requiredSlots = Mathf.Max(1, requiredSlots);
            List<SlotView> views = bagSlots != null
                ? new List<SlotView>(bagSlots)
                : new List<SlotView>();
            views.RemoveAll(view => view == null || view.button == null);
            if (views.Count == 0)
                return;

            bagSlotTemplate ??= views[Mathf.Min(views.Count - 1, 3)];
            EnsureBagScrollHierarchy(views);

            while (views.Count < requiredSlots)
            {
                int index = views.Count;
                GameObject clone = Instantiate(bagSlotTemplate.button.gameObject, bagScrollContent);
                clone.name = $"Bag Shop Slot {index + 1}";
                clone.SetActive(true);
                SlotView view = BuildSlotView(clone);
                int captured = index;
                view.button.onClick.RemoveAllListeners();
                view.button.onClick.AddListener(() => SelectBagSlot(captured));
                views.Add(view);
            }

            bagSlots = views.ToArray();
            for (int i = 0; i < bagSlots.Length; i++)
            {
                SlotView view = bagSlots[i];
                view.button.gameObject.SetActive(i < requiredSlots);
                view.button.gameObject.name = $"Bag Shop Slot {i + 1}";
                view.button.transform.SetParent(bagScrollContent, false);
                RectTransform rect = (RectTransform)view.button.transform;
                rect.localScale = Vector3.one;
                rect.sizeDelta = new Vector2(BagSlotSize, BagSlotSize);
                BindBagDrag(view, i);
            }

            int rowCount = Mathf.Max(1, Mathf.CeilToInt(requiredSlots / (float)BagSlotsPerRow));
            float contentHeight = rowCount * BagSlotSize + Mathf.Max(0, rowCount - 1) * BagSlotSpacing;
            bagScrollContent.sizeDelta = new Vector2(0f, contentHeight);
            LayoutRebuilder.ForceRebuildLayoutImmediate(bagScrollContent);
        }

        private void BindBagDrag(SlotView view, int index)
        {
            if (view?.button == null)
                return;
            ShopBagSlotDragHandler handler = view.button.GetComponent<ShopBagSlotDragHandler>() ??
                                             view.button.gameObject.AddComponent<ShopBagSlotDragHandler>();
            handler.Configure(this, index);
        }

        internal void BeginBagDrag(int index, PointerEventData data)
        {
            if (!openedExplicitly || playerInventory?.GetItem(FirstVisibleInventoryIndex + index)?.Data?.Icon == null)
                return;

            ClearDraggedBagIcon();
            draggedBagIndex = index;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return;

            GameObject preview = new GameObject("Dragged Shop Bag Item", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            preview.transform.SetParent(canvas.rootCanvas.transform, false);
            preview.transform.SetAsLastSibling();
            draggedBagIcon = preview.GetComponent<Image>();
            draggedBagIcon.sprite = playerInventory.GetItem(FirstVisibleInventoryIndex + index).Data.Icon;
            draggedBagIcon.preserveAspect = true;
            draggedBagIcon.raycastTarget = false;
            RectTransform sourceRect = bagSlots[index].icon != null
                ? bagSlots[index].icon.rectTransform : null;
            Vector2 sourceSize = sourceRect != null ? sourceRect.rect.size : new Vector2(BagSlotSize, BagSlotSize);
            draggedBagIcon.rectTransform.sizeDelta = sourceSize * 2f;
            UpdateBagDrag(data);
        }

        internal void UpdateBagDrag(PointerEventData data)
        {
            if (draggedBagIcon == null)
                return;
            Canvas canvas = draggedBagIcon.GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas.transform as RectTransform;
            UnityEngine.Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, data.position, camera, out Vector2 localPoint))
                draggedBagIcon.rectTransform.anchoredPosition = localPoint;
        }

        internal void EndBagDrag(PointerEventData data)
        {
            int source = draggedBagIndex;
            ClearDraggedBagIcon();
            if (source < 0 || playerInventory == null || EventSystem.current == null)
                return;

            List<RaycastResult> hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            foreach (RaycastResult hit in hits)
            {
                Button target = hit.gameObject.GetComponentInParent<Button>();
                if (target == null)
                    continue;
                for (int destination = 0; destination < bagSlots.Length; destination++)
                {
                    if (bagSlots[destination]?.button != target || destination == source)
                        continue;
                    InventoryItem sourceItem = playerInventory.GetItem(FirstVisibleInventoryIndex + source);
                    InventoryItem targetItem = playerInventory.GetItem(FirstVisibleInventoryIndex + destination);
                    bool mergesStack = sourceItem != null && targetItem != null &&
                        sourceItem.Data == targetItem.Data && sourceItem.Data.CanStack;
                    playerInventory.MoveItem(FirstVisibleInventoryIndex + source,
                        FirstVisibleInventoryIndex + destination);
                    if (selectedBagIndex == source)
                        selectedBagIndex = destination;
                    else if (selectedBagIndex == destination && !mergesStack)
                        selectedBagIndex = source;
                    RefreshBagSlots();
                    RefreshSelection();
                    return;
                }
            }
        }

        private void ClearDraggedBagIcon()
        {
            if (draggedBagIcon != null)
                Destroy(draggedBagIcon.gameObject);
            draggedBagIcon = null;
            draggedBagIndex = -1;
        }

        private void EnsureBagScrollHierarchy(IReadOnlyList<SlotView> views)
        {
            if (bagScrollViewport != null && bagScrollContent != null)
                return;

            RectTransform parent = views[0].button.transform.parent as RectTransform;
            if (parent == null)
                return;

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            foreach (SlotView view in views)
            {
                RectTransform rect = (RectTransform)view.button.transform;
                Vector2 position = rect.anchoredPosition;
                minX = Mathf.Min(minX, position.x - rect.rect.width * 0.5f);
                maxX = Mathf.Max(maxX, position.x + rect.rect.width * 0.5f);
                minY = Mathf.Min(minY, position.y - rect.rect.height * 0.5f);
                maxY = Mathf.Max(maxY, position.y + rect.rect.height * 0.5f);
            }

            Transform existingViewport = parent.Find("Bag Shop Scroll View");
            if (existingViewport != null)
            {
                bagScrollViewport = existingViewport as RectTransform;
                bagScrollContent = bagScrollViewport != null
                    ? bagScrollViewport.Find("Content") as RectTransform
                    : null;
            }

            if (bagScrollViewport == null)
            {
                GameObject viewportObject = new GameObject("Bag Shop Scroll View", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
                viewportObject.layer = parent.gameObject.layer;
                bagScrollViewport = viewportObject.GetComponent<RectTransform>();
                bagScrollViewport.SetParent(parent, false);
                bagScrollViewport.anchorMin = bagScrollViewport.anchorMax = new Vector2(0.5f, 0.5f);
                bagScrollViewport.pivot = new Vector2(0.5f, 0.5f);
                bagScrollViewport.anchoredPosition = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
                bagScrollViewport.sizeDelta = new Vector2(maxX - minX + 5f, maxY - minY + 2f);
            }

            if (bagScrollContent == null)
            {
                GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup));
                contentObject.layer = parent.gameObject.layer;
                bagScrollContent = contentObject.GetComponent<RectTransform>();
                bagScrollContent.SetParent(bagScrollViewport, false);
            }

            bagScrollContent.anchorMin = new Vector2(0.5f, 1f);
            bagScrollContent.anchorMax = new Vector2(0.5f, 1f);
            bagScrollContent.pivot = new Vector2(0.5f, 1f);
            bagScrollContent.anchoredPosition = Vector2.zero;
            bagScrollContent.sizeDelta = new Vector2(BagSlotsPerRow * BagSlotSize + (BagSlotsPerRow - 1) * BagSlotSpacing, 0f);

            GridLayoutGroup grid = bagScrollContent.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(BagSlotSize, BagSlotSize);
            grid.spacing = new Vector2(BagSlotSpacing, BagSlotSpacing);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = BagSlotsPerRow;

            ScrollRect scrollRect = bagScrollViewport.GetComponent<ScrollRect>();
            scrollRect.viewport = bagScrollViewport;
            scrollRect.content = bagScrollContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 18f;
            // No visible scrollbar: extra bag rows are scrolled with the mouse wheel.
            scrollRect.verticalScrollbar = null;

            foreach (SlotView view in views)
                view.button.transform.SetParent(bagScrollContent, false);
            bagScrollViewport.SetAsLastSibling();
        }

        private static SlotView BuildSlotView(GameObject root)
        {
            Image icon = null;
            TextMeshProUGUI amount = null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Image_Icon")
                    icon = child.GetComponent<Image>();
                else if (child.name == "Text_Amount")
                    amount = child.GetComponent<TextMeshProUGUI>();
            }

            return new SlotView
            {
                button = root.GetComponent<Button>(),
                background = root.GetComponent<Image>(),
                icon = icon,
                primaryText = null,
                secondaryText = amount
            };
        }

        /// <summary>
        /// Keeps manually adjusted scene copies functional when a serialized
        /// reference is lost. In particular, older Core 1 copies did not contain
        /// the right-hand page button even though the reusable prefab did.
        /// </summary>
        private void ResolveMissingControls()
        {
            previousPageButton = previousPageButton != null ? previousPageButton : FindButton("Previous Page");
            nextPageButton = nextPageButton != null ? nextPageButton : FindButton("Next Page");
            minusButton = minusButton != null ? minusButton : FindButton("Minus");
            plusButton = plusButton != null ? plusButton : FindButton("Plus");
            buyButton = buyButton != null ? buyButton : FindButton("Button_BUY");
            sellButton = sellButton != null ? sellButton : FindButton("Button_SELL");
            backButton = backButton != null ? backButton : FindButton("Button_Back");

            // Preserve the authored layout and create only the missing arrow.
            if (Application.isPlaying && nextPageButton == null && previousPageButton != null)
            {
                GameObject clone = Instantiate(previousPageButton.gameObject, previousPageButton.transform.parent);
                clone.name = "Next Page";
                RectTransform previousRect = previousPageButton.transform as RectTransform;
                RectTransform nextRect = clone.transform as RectTransform;
                RectTransform counterRect = pageText != null ? pageText.transform.parent as RectTransform : null;
                if (previousRect != null && nextRect != null)
                {
                    float counterX = counterRect != null ? counterRect.anchoredPosition.x : -59.2f;
                    nextRect.anchoredPosition = new Vector2(
                        counterX + (counterX - previousRect.anchoredPosition.x),
                        previousRect.anchoredPosition.y);
                    Vector3 scale = nextRect.localScale;
                    scale.x = -Mathf.Abs(scale.x);
                    nextRect.localScale = scale;
                }
                nextPageButton = clone.GetComponent<Button>();
                nextPageButton.onClick.RemoveAllListeners();
            }
        }

        private Button FindButton(string objectName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == objectName)
                    return children[i].GetComponent<Button>();
            }
            return null;
        }

        private void BuySelectedItem()
        {
            if (playerInventory == null || currencyItem == null ||
                selectedShopIndex < 0 || selectedShopIndex >= visibleEntries.Count)
                return;

            NpcShopCatalog.Entry entry = visibleEntries[selectedShopIndex];
            int unitPrice = GetBuyPrice(entry);
            int totalPrice = unitPrice * quantity;
            if (entry.item == null || unitPrice <= 0)
            {
                ShowTransactionMessage("ITEM NOT FOR SALE");
                return;
            }

            if (playerInventory.GetItemAmount(currencyItem) < totalPrice)
            {
                ShowTransactionMessage("NOT ENOUGH MONEY");
                return;
            }

            if (!playerInventory.CanAddItem(entry.item, quantity))
            {
                ShowTransactionMessage("INVENTORY FULL");
                return;
            }

            if (!playerInventory.TryRemoveItemAmount(currencyItem, totalPrice))
            {
                ShowTransactionMessage("PURCHASE FAILED");
                return;
            }

            if (!playerInventory.AddItem(entry.item, quantity))
            {
                // Keep the transaction atomic if the inventory changes between
                // the capacity check and the actual insertion.
                playerInventory.AddItem(currencyItem, totalPrice);
                ShowTransactionMessage("INVENTORY FULL");
                return;
            }

            quantity = 1;
            RefreshBagSlots();
            RefreshSelection();
        }

        private void SellSelectedItem()
        {
            if (playerInventory == null || currencyItem == null || selectedBagIndex < 0)
                return;

            int slotIndex = FirstVisibleInventoryIndex + selectedBagIndex;
            InventoryItem item = playerInventory.GetItem(slotIndex);
            int unitPrice = GetSellPrice(item?.Data);
            if (item == null || unitPrice <= 0)
            {
                ShowTransactionMessage("ITEM CANNOT BE SOLD");
                return;
            }

            int soldQuantity = item.Data.CanStack ? Mathf.Min(quantity, item.Amount) : 1;
            int totalPrice = unitPrice * soldQuantity;
            if (!playerInventory.TryRemoveItemAmount(slotIndex, soldQuantity))
            {
                ShowTransactionMessage("SALE FAILED");
                return;
            }

            if (!playerInventory.AddItem(currencyItem, totalPrice))
            {
                // Gold is an invisible stackable item, so this should never fail;
                // restore the sold item defensively if its data is misconfigured.
                playerInventory.AddItem(item.Data, soldQuantity);
                ShowTransactionMessage("SALE FAILED");
                return;
            }

            InventoryItem remaining = playerInventory.GetItem(slotIndex);
            if (remaining == null)
                selectedBagIndex = -1;
            quantity = 1;
            RefreshBagSlots();
            RefreshSelection();
        }

        private void ShowTransactionMessage(string message)
        {
            if (itemPriceText != null)
                itemPriceText.text = message;
        }

        private void RebuildCatalog()
        {
            visibleEntries.Clear();
            int highestUnlockedCropOrder = TutorialProgressService.Instance.HighestUnlockedCropOrder;
            if (activeCatalog != null)
            {
                foreach (NpcShopCatalog.Entry entry in activeCatalog.Entries)
                {
                    if (entry != null && entry.visible && entry.item != null &&
                        entry.item.ItemName != "Kale Seed" && entry.item.ItemName != "Water Can" &&
                        entry.IsUnlocked(highestUnlockedCropOrder) &&
                        GetBuyPrice(entry) > 0)
                        visibleEntries.Add(entry);
                }
            }

            int pageCount = Mathf.Max(1, Mathf.CeilToInt(visibleEntries.Count / (float)EntriesPerPage));
            pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
            int firstEntry = pageIndex * EntriesPerPage;

            for (int i = 0; i < shopSlots.Length; i++)
            {
                SlotView view = shopSlots[i];
                if (view == null)
                    continue;

                int entryIndex = firstEntry + i;
                bool hasEntry = entryIndex < visibleEntries.Count;
                view.button.gameObject.SetActive(hasEntry);
                if (!hasEntry)
                    continue;

                NpcShopCatalog.Entry entry = visibleEntries[entryIndex];
                view.icon.sprite = entry.item.Icon;
                view.icon.enabled = entry.item.Icon != null;
                view.primaryText.text = entry.item.ItemName;
                view.secondaryText.text = GetBuyPrice(entry) + " Đ";
                view.background.sprite = entryIndex == selectedShopIndex ? shopSelected : shopNormal;
            }

            if (pageText != null)
                pageText.text = (pageIndex + 1) + "/" + pageCount;
            if (previousPageButton != null)
                previousPageButton.interactable = pageIndex > 0;
            if (nextPageButton != null)
                nextPageButton.interactable = pageIndex < pageCount - 1;
        }

        private void RefreshBagSlots()
        {
            for (int i = 0; i < bagSlots.Length; i++)
            {
                SlotView view = bagSlots[i];
                if (view == null)
                    continue;

                InventoryItem item = playerInventory?.GetItem(FirstVisibleInventoryIndex + i);
                if (view.background != null)
                    view.background.sprite = i == selectedBagIndex ? bagSelected : bagNormal;
                if (view.icon != null)
                {
                    view.icon.sprite = item?.Data?.Icon;
                    view.icon.enabled = view.icon.sprite != null;
                }
                if (view.primaryText != null)
                    view.primaryText.text = string.Empty;
                if (view.secondaryText != null)
                    view.secondaryText.text = item != null && item.Data.CanStack ? item.Amount.ToString() : string.Empty;
            }
        }

        private void SelectShopSlot(int visibleRow)
        {
            int entryIndex = pageIndex * EntriesPerPage + visibleRow;
            if (entryIndex < 0 || entryIndex >= visibleEntries.Count)
                return;

            selectedShopIndex = entryIndex;
            selectedBagIndex = -1;
            quantity = 1;
            RebuildCatalog();
            RefreshBagSlots();
            RefreshSelection();
        }

        private void SelectBagSlot(int visibleBagIndex)
        {
            InventoryItem item = playerInventory?.GetItem(FirstVisibleInventoryIndex + visibleBagIndex);
            if (item == null)
                return;

            selectedBagIndex = visibleBagIndex;
            selectedShopIndex = -1;
            quantity = 1;
            RebuildCatalog();
            RefreshBagSlots();
            RefreshSelection();
        }

        private void PreviousPage()
        {
            if (pageIndex <= 0)
                return;
            pageIndex--;
            selectedShopIndex = -1;
            RebuildCatalog();
            SelectShopSlot(0);
        }

        private void NextPage()
        {
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(visibleEntries.Count / (float)EntriesPerPage));
            if (pageIndex >= pageCount - 1)
                return;
            pageIndex++;
            selectedShopIndex = -1;
            RebuildCatalog();
            SelectShopSlot(0);
        }

        private void ChangeQuantity(int delta)
        {
            int maximum = GetMaximumQuantity();
            quantity = Mathf.Clamp(quantity + delta, 1, maximum);
            RefreshSelection();
        }

        private int GetMaximumQuantity()
        {
            if (selectedBagIndex >= 0)
            {
                InventoryItem item = playerInventory?.GetItem(FirstVisibleInventoryIndex + selectedBagIndex);
                return item != null && item.Data.CanStack ? Mathf.Clamp(item.Amount, 1, 99) : 1;
            }

            if (selectedShopIndex < 0 || selectedShopIndex >= visibleEntries.Count)
                return 1;

            NpcShopCatalog.Entry entry = visibleEntries[selectedShopIndex];
            if (entry.item == null || !entry.item.CanStack)
                return 1;

            int price = GetBuyPrice(entry);
            int wallet = playerInventory != null && currencyItem != null
                ? playerInventory.GetItemAmount(currencyItem)
                : 0;
            return Mathf.Clamp(price > 0 ? wallet / price : 1, 1, 99);
        }

        private void RefreshSelection()
        {
            ItemData item = null;
            int unitPrice = 0;
            bool buying = selectedShopIndex >= 0 && selectedShopIndex < visibleEntries.Count;

            if (buying)
            {
                NpcShopCatalog.Entry entry = visibleEntries[selectedShopIndex];
                item = entry.item;
                unitPrice = GetBuyPrice(entry);
            }
            else if (selectedBagIndex >= 0)
            {
                InventoryItem inventoryItem = playerInventory?.GetItem(FirstVisibleInventoryIndex + selectedBagIndex);
                item = inventoryItem?.Data;
                unitPrice = GetSellPrice(item);
            }

            if (previewIcon != null)
            {
                previewIcon.sprite = item?.Icon;
                previewIcon.enabled = previewIcon.sprite != null;
            }
            if (itemNameText != null)
                itemNameText.text = item != null ? item.ItemName : "SELECT ITEM";
            if (itemPriceText != null)
                itemPriceText.text = item != null ? unitPrice + " Đ  x " + quantity + " = " + unitPrice * quantity + " Đ" : string.Empty;
            int maximum = GetMaximumQuantity();
            quantity = Mathf.Clamp(quantity, 1, maximum);
            if (quantityText != null)
                quantityText.text = quantity.ToString();
            bool canAfford = !buying || (playerInventory != null && currencyItem != null &&
                playerInventory.GetItemAmount(currencyItem) >= unitPrice * quantity);
            bool hasCapacity = !buying || (playerInventory != null && item != null &&
                playerInventory.CanAddItem(item, quantity));
            SetTransactionButtonState(buyButton, buying && unitPrice > 0 && canAfford && hasCapacity);
            SetTransactionButtonState(sellButton, !buying && item != null && unitPrice > 0);
            if (minusButton != null)
                minusButton.interactable = quantity > 1;
            if (plusButton != null)
                plusButton.interactable = quantity < maximum;
        }

        private static void SetTransactionButtonState(Button button, bool enabled)
        {
            if (button == null)
                return;

            button.interactable = enabled;
            Graphic graphic = button.targetGraphic;
            if (graphic != null)
                graphic.color = enabled ? Color.white : DisabledTransactionColor;
        }

        private static int GetBuyPrice(NpcShopCatalog.Entry entry)
        {
            if (entry == null || entry.item == null)
                return 0;
            if (entry.futurePrice > 0)
                return entry.futurePrice;

            switch (entry.item.ItemName)
            {
                case "Axe": return 6;
                case "Hoe": return 3;
                case "Fertilizer": return 6;
                case "Carrot Seed": return 1;
                case "Onion Seed": return 2;
                case "Garlic Seed": return 3;
                case "Cabbage Seed": return 4;
                case "Potato Seed": return 4;
                case "Tomato Seed": return 7;
                case "Banana Seed": return 10;
                case "Mango Seed": return 18;
                case "Cucumber Seed": return 6;
                case "Dragon Fruit Seed": return 20;
                default: return 0;
            }
        }

        private static int GetSellPrice(ItemData item)
        {
            if (item == null)
                return 0;
            switch (item.ItemName)
            {
                case "Carrot": return 3;
                case "Onion": return 5;
                case "Garlic": return 7;
                case "Cabbage": return 8;
                case "Potato": return 9;
                case "Tomato": return 4;
                case "Banana": return 4;
                case "Mango": return 9;
                case "Cucumber": return 3;
                case "Dragon Fruit": return 10;
                default: return 0;
            }
        }
    }
}
