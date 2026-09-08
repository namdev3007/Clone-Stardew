#if UNITY_EDITOR
using Item.Inventory;
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class SetupBagInventoryUI
{
    private const string PrefabPath = "Assets/Prefabs/User Interface/Core/Pauze Menu.prefab";
    private const string SpriteRoot = "Assets/Sprites/bag-shop-ui/bag";
    private const string MarkerName = "BagInventoryLayout";

    [InitializeOnLoadMethod]
    private static void QueueSetup()
    {
        EditorApplication.delayCall += () => Configure(false);
    }

    [MenuItem("Tools/UI/Setup New Bag Inventory")]
    public static void RunFromMenu()
    {
        Configure(true);
    }

    private static void Configure(bool force)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // The redesigned bag is the inventory surface now. Remove the old
            // pause-menu inventory tab so it cannot overlap or reopen the legacy UI.
            Transform oldInventoryButton = FindDeepChild(root.transform, "Button_Inventory");
            bool removedOldInventoryButton = oldInventoryButton != null;
            if (removedOldInventoryButton)
                UnityEngine.Object.DestroyImmediate(oldInventoryButton.gameObject);

            Transform window = FindDeepChild(root.transform, "Window_Inventory");
            Transform slotContainer = FindDeepChild(window, "InventorySlots");
            if (window == null || slotContainer == null)
                throw new InvalidOperationException("Window_Inventory or InventorySlots was not found in Pauze Menu.prefab.");

            // Keep the bag hidden in Edit Mode and when the scene first loads.
            // BagWindow lives on the active parent and enables this object on Tab.
            bool windowWasActive = window.gameObject.activeSelf;
            window.gameObject.SetActive(false);

            Transform oldMarker = window.Find(MarkerName);
            if (oldMarker != null && !force)
            {
                if (removedOldInventoryButton || windowWasActive)
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                return;
            }

            if (force)
            {
                Transform currentMarker = window.Find(MarkerName);
                if (currentMarker != null)
                    UnityEngine.Object.DestroyImmediate(currentMarker.gameObject);
            }

            ConfigureWindow(window);
            ConfigureSlots(slotContainer);
            CreateDecorationAndDescription(window, slotContainer);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("New bag inventory UI setup completed in Pauze Menu.prefab.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureWindow(Transform window)
    {
        RectTransform rect = (RectTransform)window;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(320f, 161f);

        Image background = window.GetComponent<Image>();
        if (background == null)
            background = window.gameObject.AddComponent<Image>();
        background.sprite = LoadFirstSprite($"{SpriteRoot}/giao diện-bag.png");
        background.color = Color.white;
        background.preserveAspect = true;
        background.raycastTarget = false;
    }

    private static void ConfigureSlots(Transform slotContainer)
    {
        GridLayoutGroup oldGrid = slotContainer.GetComponent<GridLayoutGroup>();
        if (oldGrid != null)
            UnityEngine.Object.DestroyImmediate(oldGrid);

        RectTransform containerRect = (RectTransform)slotContainer;
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = Vector2.zero;

        InventorySlot[] slots = slotContainer.GetComponentsInChildren<InventorySlot>(true)
            .OrderBy(GetExistingSlotIndex)
            .ToArray();

        for (int i = slots.Length - 1; i >= 26; i--)
            UnityEngine.Object.DestroyImmediate(slots[i].gameObject);

        Sprite[] quickNormal = LoadSprites($"{SpriteRoot}/slot-dùng nhanh.png");
        Sprite[] quickSelected = LoadSprites($"{SpriteRoot}/slot-dùng nhanh-đã chọn.png");
        Sprite bagNormal = LoadFirstSprite($"{SpriteRoot}/slot-bag-chưa chọn.png");
        Sprite bagSelected = LoadFirstSprite($"{SpriteRoot}/slot-bag-đã chọn.png");

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

        for (int i = 0; i < Mathf.Min(26, slots.Length); i++)
        {
            bool quickSlot = i < 5;
            int quickIndex = Mathf.Clamp(i, 0, Mathf.Max(0, quickNormal.Length - 1));
            Sprite normal = quickSlot && quickNormal.Length > 0 ? quickNormal[quickIndex] : bagNormal;
            Sprite selected = quickSlot && quickSelected.Length > 0
                ? quickSelected[Mathf.Clamp(i, 0, quickSelected.Length - 1)]
                : bagSelected;

            Transform parent = quickSlot ? quickSlots : bagRows[(i - 5) / 7];
            slots[i].transform.SetParent(parent, false);
            slots[i].transform.SetAsLastSibling();
            ConfigureSlot(slots[i], i, quickSlot, normal, selected);
        }
    }

    private static RectTransform CreateLayoutRow(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject gameObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        gameObject.layer = 5;
        RectTransform rect = gameObject.GetComponent<RectTransform>();
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

    private static RectTransform CreateSlotRegion(Transform parent, string name, Vector2 position, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject gameObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        gameObject.layer = 5;
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return rect;
    }

    private static int GetExistingSlotIndex(InventorySlot slot)
    {
        string name = slot.gameObject.name;
        if (name.StartsWith("Quick Slot ") && int.TryParse(name.Substring(11), out int quickIndex))
            return quickIndex - 1;
        if (name.StartsWith("Bag Slot ") && int.TryParse(name.Substring(9), out int bagIndex))
            return bagIndex + 4;
        return slot.transform.GetSiblingIndex();
    }

    private static void ConfigureSlot(InventorySlot slot, int index, bool quickSlot, Sprite normal, Sprite selected)
    {
        GameObject gameObject = slot.gameObject;
        gameObject.SetActive(true);
        gameObject.name = quickSlot ? $"Quick Slot {index + 1}" : $"Bag Slot {index - 4}";
        gameObject.layer = 5;

        RectTransform rect = (RectTransform)slot.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        if (quickSlot)
        {
            rect.sizeDelta = new Vector2(33f, 33f);
            rect.anchoredPosition = new Vector2(-78f + index * 39f, 0f);
        }
        else
        {
            int bagIndex = index - 5;
            int column = bagIndex % 7;
            int row = bagIndex / 7;
            rect.sizeDelta = new Vector2(22f, 22f);
            rect.anchoredPosition = new Vector2(-75f + column * 25f, 25f - row * 25f);
        }

        Image background = gameObject.GetComponent<Image>();
        if (background == null)
            background = gameObject.AddComponent<Image>();
        background.sprite = normal;
        background.color = Color.white;
        background.preserveAspect = true;
        background.raycastTarget = true;

        SerializedObject serializedSlot = new SerializedObject(slot);
        SerializedProperty references = serializedSlot.FindProperty("references");
        SerializedProperty settings = serializedSlot.FindProperty("settings");
        serializedSlot.FindProperty("forcedSlotIndex").intValue = index;
        Image highlight = references.FindPropertyRelative("Highlight").objectReferenceValue as Image;
        Image icon = references.FindPropertyRelative("Icon").objectReferenceValue as Image;
        TextMeshProUGUI slotText = references.FindPropertyRelative("SlotText").objectReferenceValue as TextMeshProUGUI;
        TextMeshProUGUI amountText = references.FindPropertyRelative("AmountText").objectReferenceValue as TextMeshProUGUI;

        settings.FindPropertyRelative("displaySlotNumber").boolValue = false;
        settings.FindPropertyRelative("equipItemOnClick").boolValue = quickSlot;
        settings.FindPropertyRelative("moveItemOnDrag").boolValue = true;
        settings.FindPropertyRelative("slotIndexOffset").intValue = 0;
        settings.FindPropertyRelative("hasSelectionHighlight").boolValue = true;
        serializedSlot.ApplyModifiedPropertiesWithoutUndo();

        if (slotText != null)
            slotText.gameObject.SetActive(false);

        if (highlight != null)
        {
            RectTransform highlightRect = highlight.rectTransform;
            highlightRect.anchorMin = Vector2.zero;
            highlightRect.anchorMax = Vector2.one;
            highlightRect.anchoredPosition = Vector2.zero;
            highlightRect.sizeDelta = Vector2.zero;
            highlight.sprite = selected;
            highlight.color = Color.white;
            highlight.preserveAspect = true;
            highlight.raycastTarget = false;
            highlight.transform.SetAsFirstSibling();
            highlight.gameObject.SetActive(false);
        }

        if (icon != null)
        {
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = quickSlot ? new Vector2(0f, -1f) : Vector2.zero;
            iconRect.sizeDelta = quickSlot ? new Vector2(23f, 23f) : new Vector2(16f, 16f);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        if (amountText != null)
        {
            RectTransform amountRect = amountText.rectTransform;
            amountRect.anchorMin = amountRect.anchorMax = new Vector2(1f, 0f);
            amountRect.pivot = new Vector2(1f, 0f);
            amountRect.anchoredPosition = new Vector2(-2f, 2f);
            amountRect.sizeDelta = new Vector2(18f, 10f);
            amountText.fontSize = quickSlot ? 7f : 6f;
            amountText.alignment = TextAlignmentOptions.BottomRight;
            amountText.margin = Vector4.zero;
            amountText.textWrappingMode = TextWrappingModes.NoWrap;
            amountText.overflowMode = TextOverflowModes.Overflow;
            amountText.raycastTarget = false;
        }
    }

    private static void CreateDecorationAndDescription(Transform window, Transform slotContainer)
    {
        GameObject marker = new GameObject(MarkerName, typeof(RectTransform));
        marker.layer = 5;
        RectTransform markerRect = marker.GetComponent<RectTransform>();
        markerRect.SetParent(window, false);
        markerRect.anchorMin = Vector2.zero;
        markerRect.anchorMax = Vector2.one;
        markerRect.sizeDelta = Vector2.zero;
        markerRect.SetSiblingIndex(slotContainer.GetSiblingIndex());

        CreateImage(markerRect, "Title BAG", LoadFirstSprite($"{SpriteRoot}/text-bag.png"), new Vector2(-113f, 68f), new Vector2(46f, 16f));
        CreateImage(markerRect, "Title TUI", LoadFirstSprite($"{SpriteRoot}/text-túi.png"), new Vector2(-20f, 68f), new Vector2(46f, 16f));

        Image panelImage = CreateImage(markerRect, "Item Description", LoadFirstSprite($"{SpriteRoot}/bảng môi tả item-bag.png"), new Vector2(105f, -5f), new Vector2(91f, 147f));
        BagItemDescriptionPanel panel = window.GetComponent<BagItemDescriptionPanel>();
        if (panel == null)
            panel = window.gameObject.AddComponent<BagItemDescriptionPanel>();

        Image icon = CreateImage(panelImage.rectTransform, "Item Icon", null, new Vector2(-26f, 48f), new Vector2(24f, 24f));
        icon.preserveAspect = true;

        TMP_FontAsset font = window.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        TextMeshProUGUI itemName = CreateText(panelImage.rectTransform, "Item Name", font, new Vector2(20f, 55f), new Vector2(45f, 14f), 7f, TextAlignmentOptions.Left);
        TextMeshProUGUI amount = CreateText(panelImage.rectTransform, "Item Amount", font, new Vector2(20f, 42f), new Vector2(45f, 10f), 6f, TextAlignmentOptions.Left);
        TextMeshProUGUI description = CreateText(panelImage.rectTransform, "Item Description Text", font, new Vector2(0f, -28f), new Vector2(76f, 76f), 6.5f, TextAlignmentOptions.TopLeft);

        SerializedObject serializedPanel = new SerializedObject(panel);
        serializedPanel.FindProperty("icon").objectReferenceValue = icon;
        serializedPanel.FindProperty("itemName").objectReferenceValue = itemName;
        serializedPanel.FindProperty("amount").objectReferenceValue = amount;
        serializedPanel.FindProperty("description").objectReferenceValue = description;
        serializedPanel.ApplyModifiedPropertiesWithoutUndo();

        slotContainer.SetAsLastSibling();
    }

    private static Image CreateImage(RectTransform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.layer = 5;
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = gameObject.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, string name, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        gameObject.layer = 5;
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = new Color32(255, 222, 151, 255);
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static Sprite[] LoadSprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(sprite => NumericSuffix(sprite.name))
            .ToArray();
    }

    private static Sprite LoadFirstSprite(string path)
    {
        return LoadSprites(path).FirstOrDefault();
    }

    private static int NumericSuffix(string name)
    {
        int separator = name.LastIndexOf('_');
        return separator >= 0 && int.TryParse(name.Substring(separator + 1), out int value) ? value : 0;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null)
            return null;
        if (parent.name == name)
            return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), name);
            if (result != null)
                return result;
        }
        return null;
    }

}
#endif
