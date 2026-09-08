using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Item.Inventory
{
    [DefaultExecutionOrder(-1000)]
    public class BagInventoryUIBuilder : MonoBehaviour
    {
        [SerializeField] private Sprite background;
        [SerializeField] private Sprite[] quickNormal = new Sprite[5];
        [SerializeField] private Sprite[] quickSelected = new Sprite[5];
        [SerializeField] private Sprite bagNormal;
        [SerializeField] private Sprite bagSelected;
        [SerializeField] private Sprite descriptionPanel;
        [SerializeField] private Sprite bagTitle;
        [SerializeField] private Sprite pocketTitle;

        private const string MarkerName = "BagInventoryLayout";

        private void Awake()
        {
            // The prefab is authored in Edit Mode. Never rebuild an existing bag
            // at runtime, otherwise manual RectTransform/TMP adjustments are
            // replaced as soon as Play Mode starts.
            if (FindExistingLayoutMarker() != null)
                return;

            Build();
        }

        private Transform FindExistingLayoutMarker()
        {
            return transform.Find(MarkerName);
        }

        private void Build()
        {
            RectTransform window = transform as RectTransform;
            window.sizeDelta = new Vector2(320f, 161f);

            Image windowImage = GetComponent<Image>();
            if (windowImage == null)
                windowImage = gameObject.AddComponent<Image>();
            windowImage.sprite = background;
            windowImage.color = Color.white;
            windowImage.preserveAspect = true;
            windowImage.raycastTarget = false;

            Transform slotContainer = transform.Find("InventorySlots");
            if (slotContainer == null)
                return;

            GridLayoutGroup grid = slotContainer.GetComponent<GridLayoutGroup>();
            if (grid != null)
                grid.enabled = false;

            RectTransform containerRect = slotContainer as RectTransform;
            containerRect.anchorMin = Vector2.zero;
            containerRect.anchorMax = Vector2.one;
            containerRect.pivot = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;
            containerRect.sizeDelta = Vector2.zero;

            InventorySlot[] slots = slotContainer.GetComponentsInChildren<InventorySlot>(true);
            System.Array.Sort(slots, (left, right) => GetExistingSlotIndex(left).CompareTo(GetExistingSlotIndex(right)));
            RectTransform quickSlots = CreateSlotRegion(slotContainer, "Quick Slots", new Vector2(-61f, 24f), new Vector2(189f, 33f));
            RectTransform bagSlots = CreateSlotRegion(slotContainer, "Bag Slots", new Vector2(-55f, -49f), new Vector2(172f, 72f));
            ConfigureHorizontalLayout(quickSlots, 6f);
            ConfigureVerticalLayout(bagSlots, 3f);

            RectTransform[] bagRows = new RectTransform[3];
            for (int row = 0; row < bagRows.Length; row++)
            {
                bagRows[row] = CreateLayoutRow(bagSlots, $"Bag Row {row + 1}");
                ConfigureHorizontalLayout(bagRows[row], 3f);
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (i >= 26)
                {
                    slots[i].gameObject.SetActive(false);
                    continue;
                }

                bool quick = i < 5;
                Sprite normal = quick ? quickNormal[Mathf.Clamp(i, 0, quickNormal.Length - 1)] : bagNormal;
                Sprite selected = quick ? quickSelected[Mathf.Clamp(i, 0, quickSelected.Length - 1)] : bagSelected;
                Transform parent = quick ? quickSlots : bagRows[(i - 5) / 7];
                slots[i].transform.SetParent(parent, false);
                slots[i].transform.SetAsLastSibling();
                ConfigureSlot(slots[i], i, quick, normal, selected);
            }

            GameObject marker = new GameObject(MarkerName, typeof(RectTransform));
            marker.layer = 5;
            RectTransform markerRect = marker.GetComponent<RectTransform>();
            markerRect.SetParent(transform, false);
            markerRect.anchorMin = Vector2.zero;
            markerRect.anchorMax = Vector2.one;
            markerRect.sizeDelta = Vector2.zero;
            markerRect.SetSiblingIndex(slotContainer.GetSiblingIndex());

            CreateImage(markerRect, "Title BAG", bagTitle, new Vector2(-113f, 68f), new Vector2(46f, 16f));
            CreateImage(markerRect, "Title TUI", pocketTitle, new Vector2(-20f, 68f), new Vector2(46f, 16f));
            CreateDescription(markerRect);
            slotContainer.SetAsLastSibling();
        }

        private void ConfigureSlot(InventorySlot slot, int index, bool quick, Sprite normal, Sprite selected)
        {
            slot.gameObject.SetActive(true);
            slot.gameObject.name = quick ? $"Quick Slot {index + 1}" : $"Bag Slot {index - 4}";
            RectTransform rect = slot.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            if (quick)
            {
                rect.sizeDelta = new Vector2(33f, 33f);
                rect.anchoredPosition = new Vector2(-78f + index * 39f, 0f);
            }
            else
            {
                int bagIndex = index - 5;
                rect.sizeDelta = new Vector2(22f, 22f);
                rect.anchoredPosition = new Vector2(-75f + bagIndex % 7 * 25f, 25f - bagIndex / 7 * 25f);
            }
            slot.ConfigureBagSlot(index, normal, selected, quick);
        }

        private static RectTransform CreateSlotRegion(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            Transform existing = parent.Find(objectName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform));
            child.layer = 5;
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static RectTransform CreateLayoutRow(Transform parent, string objectName)
        {
            Transform existing = parent.Find(objectName);
            GameObject child = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform));
            child.layer = 5;
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static void ConfigureHorizontalLayout(RectTransform rect, float spacing)
        {
            HorizontalLayoutGroup layout = rect.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;
        }

        private static void ConfigureVerticalLayout(RectTransform rect, float spacing)
        {
            VerticalLayoutGroup layout = rect.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;
        }

        private static int GetExistingSlotIndex(InventorySlot slot)
        {
            string objectName = slot.gameObject.name;
            if (objectName.StartsWith("Quick Slot ") && int.TryParse(objectName.Substring(11), out int quickIndex))
                return quickIndex - 1;
            if (objectName.StartsWith("Bag Slot ") && int.TryParse(objectName.Substring(9), out int bagIndex))
                return bagIndex + 4;
            return slot.transform.GetSiblingIndex();
        }

        private void CreateDescription(RectTransform parent)
        {
            Image panelImage = CreateImage(parent, "Item Description", descriptionPanel, new Vector2(105f, -5f), new Vector2(91f, 147f));
            BagItemDescriptionPanel panel = GetComponent<BagItemDescriptionPanel>();
            if (panel == null)
                panel = gameObject.AddComponent<BagItemDescriptionPanel>();
            Image icon = CreateImage(panelImage.rectTransform, "Item Icon", null, new Vector2(-26f, 48f), new Vector2(24f, 24f));
            TMP_FontAsset font = GetComponentInChildren<TextMeshProUGUI>(true)?.font;
            TextMeshProUGUI itemName = CreateText(panelImage.rectTransform, "Item Name", font, new Vector2(20f, 55f), new Vector2(45f, 14f), 7f, TextAlignmentOptions.Left);
            TextMeshProUGUI amount = CreateText(panelImage.rectTransform, "Item Amount", font, new Vector2(20f, 42f), new Vector2(45f, 10f), 6f, TextAlignmentOptions.Left);
            TextMeshProUGUI description = CreateText(panelImage.rectTransform, "Item Description Text", font, new Vector2(0f, -28f), new Vector2(76f, 76f), 6.5f, TextAlignmentOptions.TopLeft);
            panel.Configure(icon, itemName, amount, description);
        }

        private static Image CreateImage(RectTransform parent, string objectName, Sprite sprite, Vector2 position, Vector2 size)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            child.layer = 5;
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = child.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(RectTransform parent, string objectName, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.layer = 5;
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = new Color32(255, 222, 151, 255);
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            return text;
        }
    }
}
