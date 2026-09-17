#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Item;
using World.NPC;

[InitializeOnLoad]
public static class SetupShopUI
{
    private const string CoreScenePath = "Assets/MainScenes/Core 1.unity";
    private const string PrefabPath = "Assets/Prefabs/User Interface/Core/Shop UI.prefab";
    private const string ShopItemTemplatePath = "Assets/Prefabs/Shop Item 2.prefab";
    private const string ShopRootName = "Shop UI";
    private const string FontPath = "Assets/fonts/Dùng cho text khác/dearpix-1.94 Ygygfu SDF.asset";
    private const string BagAmountFontPath = "Assets/fonts/Dùng cho text khác/for shop.asset";
    private const string CatalogPath = "Assets/ScriptableObjects/NPC/Uncle Hai Shop Catalog.asset";
    private const string CurrencyItemPath = "Assets/ScriptableObjects/Items/Tools/Item_Gold.asset";
    private const float UiScale = 2.535f;

    private static TMP_FontAsset font;
    private static TMP_FontAsset bagAmountFont;

    static SetupShopUI()
    {
        EditorApplication.delayCall += BuildIfMissing;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += BuildIfMissing;
    }

    [MenuItem("Tools/Game/Rebuild Uncle Hai Shop UI")]
    public static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Single);
        GameObject existing = FindInScene(scene, ShopRootName);
        if (existing != null)
            UnityEngine.Object.DestroyImmediate(existing);

        Build(scene);
    }

    private static void BuildIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene activeScene = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(CoreScenePath);
        bool openedTemporarily = !scene.IsValid() || !scene.isLoaded;

        if (openedTemporarily)
            scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject existing = FindInScene(scene, ShopRootName);
            if (existing == null)
            {
                Build(scene);
                return;
            }

            CanvasScaler scaler = existing.GetComponent<CanvasScaler>();
            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize || !Mathf.Approximately(scaler.scaleFactor, 2f))
            {
                UnityEngine.Object.DestroyImmediate(existing);
                Build(scene);
                return;
            }

            if (RepairMissingNextPageButton(existing))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        finally
        {
            if (openedTemporarily)
            {
                if (activeScene.IsValid() && activeScene.isLoaded)
                    SceneManager.SetActiveScene(activeScene);

                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static bool RepairMissingNextPageButton(GameObject root)
    {
        ShopWindowController controller = root.GetComponent<ShopWindowController>();
        Transform existingNext = FindDeepChild(root.transform, "Next Page");
        if (controller == null || existingNext != null)
            return false;

        Button previous = FindDeepChild(root.transform, "Previous Page")?.GetComponent<Button>();
        RectTransform counter = FindDeepChild(root.transform, "Page Counter") as RectTransform;
        if (previous == null)
            return false;

        GameObject clone = UnityEngine.Object.Instantiate(previous.gameObject, previous.transform.parent);
        clone.name = "Next Page";
        RectTransform previousRect = previous.transform as RectTransform;
        RectTransform nextRect = clone.transform as RectTransform;
        float counterX = counter != null ? counter.anchoredPosition.x : -59.2f;
        nextRect.anchoredPosition = new Vector2(
            counterX + (counterX - previousRect.anchoredPosition.x),
            previousRect.anchoredPosition.y);

        Sprite[] arrows = LoadSprites("Assets/Sprites/bag-shop-ui/shop/nút qua trang mới-shop.png");
        Image image = clone.GetComponent<Image>();
        Button next = clone.GetComponent<Button>();
        image.sprite = arrows.ElementAtOrDefault(1);
        SpriteState state = next.spriteState;
        state.highlightedSprite = arrows.ElementAtOrDefault(1);
        state.pressedSprite = arrows.ElementAtOrDefault(3);
        state.selectedSprite = arrows.ElementAtOrDefault(1);
        state.disabledSprite = arrows.ElementAtOrDefault(1);
        next.spriteState = state;
        next.onClick = new Button.ButtonClickedEvent();

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("nextPageButton").objectReferenceValue = next;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    private static void Build(Scene scene)
    {
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        bagAmountFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BagAmountFontPath);
        NpcShopCatalog catalog = AssetDatabase.LoadAssetAtPath<NpcShopCatalog>(CatalogPath);
        ItemData currencyItem = AssetDatabase.LoadAssetAtPath<ItemData>(CurrencyItemPath);

        Sprite[] frameSprites = LoadSprites("Assets/Sprites/bag-shop-ui/shop/giao diện shop.png");
        Sprite shopNormal = LoadSprite("Assets/Sprites/bag-shop-ui/shop/slot-shop-chưa chọn.png", 0);
        Sprite shopSelected = LoadSprite("Assets/Sprites/bag-shop-ui/shop/slot-shop-đã chọn.png", 0);
        Sprite bagNormal = LoadSprite("Assets/Sprites/bag-shop-ui/bag/slot-bag-chưa chọn.png", 0);
        Sprite bagSelected = LoadSprite("Assets/Sprites/bag-shop-ui/bag/slot-bag-đã chọn.png", 0);
        Sprite previewPanelSprite = LoadSprite("Assets/Sprites/bag-shop-ui/shop/bảng hiện items trong shop.png", 0);
        Sprite quantityPanelSprite = LoadSprite("Assets/Sprites/bag-shop-ui/shop/bảng số cộng trừ.png", 0);
        Sprite pagePanelSprite = LoadSprite("Assets/Sprites/bag-shop-ui/shop/bảng số trang-shop.png", 0);
        Sprite[] pageArrows = LoadSprites("Assets/Sprites/bag-shop-ui/shop/nút qua trang mới-shop.png");
        Sprite[] quantityButtons = LoadSprites("Assets/Sprites/bag-shop-ui/shop/cộng-trừ.png");
        Sprite[] buySprites = LoadSprites("Assets/Sprites/Buttons/tieng-anh/buy.png");
        Sprite[] sellSprites = LoadSprites("Assets/Sprites/Buttons/tieng-anh/sell.png");
        Sprite backSprite = LoadSprite("Assets/Sprites/Buttons/other-button/back-lùi về.png", 0);

        GameObject root = new GameObject(ShopRootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(root, scene);
        root.layer = 5;
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        // Match the authored Bag canvas exactly. The bag uses a constant 2x
        // canvas scale in addition to Window_Inventory's local 2.535 scale.
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 2f;

        ShopWindowController controller = root.AddComponent<ShopWindowController>();

        RectTransform frame = CreateRect("Window_Shop", root.transform, Vector2.zero, new Vector2(320f, 170f));
        frame.localScale = new Vector3(UiScale, UiScale, 1f);

        CreateImage("Shop Stock Background", frame, frameSprites.ElementAtOrDefault(0), new Vector2(-59.2f, 30.2f), new Vector2(201.6f, 109.6f), false);
        CreateImage("Player Bag Background", frame, frameSprites.ElementAtOrDefault(1), new Vector2(-59.2f, -54.5f), new Vector2(201.6f, 60.9f), false);
        CreateImage("Transaction Background", frame, frameSprites.ElementAtOrDefault(2), new Vector2(100.8f, 0f), new Vector2(118.4f, 170f), false);

        ShopWindowController.SlotView[] shopViews = new ShopWindowController.SlotView[3];
        for (int i = 0; i < shopViews.Length; i++)
            shopViews[i] = CreateShopRow(frame, i, new Vector2(-59.2f, 68.6f - 25f * i), shopNormal);

        Image pagePanel = CreateImage("Page Counter", frame, pagePanelSprite, new Vector2(-59.2f, -12f), new Vector2(62f, 24f), false);
        TextMeshProUGUI pageText = CreateText("Text_Page", pagePanel.transform, "1/1", 7f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(28f, 14f));
        Button previousPage = CreateSpriteButton("Previous Page", frame, pageArrows.ElementAtOrDefault(0), pageArrows.ElementAtOrDefault(2), new Vector2(-94f, -12f), new Vector2(10f, 15f));
        Button nextPage = CreateSpriteButton("Next Page", frame, pageArrows.ElementAtOrDefault(1), pageArrows.ElementAtOrDefault(3), new Vector2(-24f, -12f), new Vector2(11f, 16f));

        ShopWindowController.SlotView[] bagViews = new ShopWindowController.SlotView[14];
        const float bagSlotSize = 22f;
        const float gapX = 3f;
        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 7; column++)
            {
                int index = row * 7 + column;
                float x = -134.2f + column * (bagSlotSize + gapX);
                float y = row == 0 ? -41.7f : -67.7f;
                bagViews[index] = CreateBagSlot(frame, index, new Vector2(x, y), bagNormal);
            }
        }

        Image previewPanel = CreateImage("Selected Item Panel", frame, previewPanelSprite, new Vector2(100.8f, 35f), new Vector2(110f, 94f), false);
        Image previewIcon = CreateImage("Selected Item Icon", previewPanel.rectTransform, null, new Vector2(0f, 3f), new Vector2(48f, 48f), true);
        TextMeshProUGUI itemName = CreateText("Text_ItemName", previewPanel.transform, "SELECT ITEM", 6.5f, TextAlignmentOptions.Top,
            new Vector2(0f, 39f), new Vector2(96f, 13f));
        TextMeshProUGUI itemPrice = CreateText("Text_Price", previewPanel.transform, string.Empty, 5.5f, TextAlignmentOptions.Bottom,
            new Vector2(0f, -38f), new Vector2(98f, 13f));

        Image quantityPanel = CreateImage("Quantity Panel", frame, quantityPanelSprite, new Vector2(100.8f, -27f), new Vector2(60f, 28f), false);
        TextMeshProUGUI quantityText = CreateText("Text_Quantity", quantityPanel.transform, "1", 8f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(36f, 18f));
        Button minusButton = CreateSpriteButton("Minus", frame, quantityButtons.ElementAtOrDefault(2), quantityButtons.ElementAtOrDefault(3),
            new Vector2(66f, -27f), new Vector2(19f, 19f));
        Button plusButton = CreateSpriteButton("Plus", frame, quantityButtons.ElementAtOrDefault(0), quantityButtons.ElementAtOrDefault(1),
            new Vector2(135.5f, -27f), new Vector2(19f, 19f));

        Button buyButton = CreateSpriteButton("Button_BUY", frame, buySprites.ElementAtOrDefault(0), buySprites.ElementAtOrDefault(1),
            new Vector2(100.8f, -55f), new Vector2(101f, 18f));
        Button sellButton = CreateSpriteButton("Button_SELL", frame, sellSprites.ElementAtOrDefault(0), sellSprites.ElementAtOrDefault(1),
            new Vector2(100.8f, -76f), new Vector2(101f, 18f));
        Button backButton = CreateSpriteButton("Button_Back", frame, backSprite, null, new Vector2(-136f, 96f), new Vector2(48f, 23f));

        controller.Configure(catalog, currencyItem, shopViews, bagViews, shopNormal, shopSelected, bagNormal, bagSelected,
            previewIcon, itemName, itemPrice, quantityText, pageText, previousPage, nextPage,
            minusButton, plusButton, buyButton, sellButton, backButton);
        controller.ApplyEditorPreview();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        // Leave it visible in the authored scene so the layout can be adjusted.
        // ShopWindowController hides it automatically when Play Mode starts.
        root.SetActive(true);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Uncle Hai shop UI created in Core 1. Size matches the current bag window.");
    }

    private static ShopWindowController.SlotView CreateShopRow(RectTransform parent, int index, Vector2 position, Sprite backgroundSprite)
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(ShopItemTemplatePath);
        if (template != null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(template, parent);
            instance.name = "Shop Item " + (index + 1);
            RectTransform instanceRect = instance.GetComponent<RectTransform>();
            instanceRect.anchoredPosition = position;

            Button templateButton = instance.GetComponent<Button>();
            Image templateIcon = FindDeepChild(instance.transform, "Image_Icon")?.GetComponent<Image>();
            TextMeshProUGUI templateName = FindDeepChild(instance.transform, "Text_Name")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI templatePrice = FindDeepChild(instance.transform, "Text_mony")?.GetComponent<TextMeshProUGUI>();
            if (templatePrice == null)
                templatePrice = FindDeepChild(instance.transform, "Text_Name (1)")?.GetComponent<TextMeshProUGUI>();

            return new ShopWindowController.SlotView
            {
                button = templateButton,
                background = instance.GetComponent<Image>(),
                icon = templateIcon,
                primaryText = templateName,
                secondaryText = templatePrice
            };
        }

        Button button = CreateSpriteButton("Shop Item " + (index + 1), parent, backgroundSprite, null, position, new Vector2(194f, 28f));
        Image icon = CreateImage("Image_Icon", button.transform as RectTransform, null, new Vector2(-77f, 0f), new Vector2(22f, 22f), true);
        TextMeshProUGUI name = CreateText("Text_Name", button.transform, "ITEM", 6f, TextAlignmentOptions.Left,
            new Vector2(1f, 3f), new Vector2(122f, 10f));
        TextMeshProUGUI price = CreateText("Text_Price", button.transform, "0 Đ", 5.5f, TextAlignmentOptions.Left,
            new Vector2(1f, -7f), new Vector2(122f, 9f));
        return new ShopWindowController.SlotView { button = button, background = button.GetComponent<Image>(), icon = icon, primaryText = name, secondaryText = price };
    }

    private static ShopWindowController.SlotView CreateBagSlot(RectTransform parent, int index, Vector2 position, Sprite backgroundSprite)
    {
        Button button = CreateSpriteButton("Bag Shop Slot " + (index + 1), parent, backgroundSprite, null, position, new Vector2(22f, 22f));
        Image icon = CreateImage("Image_Icon", button.transform as RectTransform, null, Vector2.zero, new Vector2(15f, 15f), true);
        TextMeshProUGUI amount = CreateText("Text_Amount", button.transform, string.Empty, 5.5f, TextAlignmentOptions.BottomRight,
            new Vector2(-1f, 1f), new Vector2(19f, 18f));
        if (bagAmountFont != null)
            amount.font = bagAmountFont;
        return new ShopWindowController.SlotView { button = button, background = button.GetComponent<Image>(), icon = icon, primaryText = null, secondaryText = amount };
    }

    private static Button CreateSpriteButton(string name, Transform parent, Sprite normal, Sprite pressed, Vector2 position, Vector2 size)
    {
        Image image = CreateImage(name, parent as RectTransform, normal, position, size, false);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.transition = pressed != null ? Selectable.Transition.SpriteSwap : Selectable.Transition.ColorTint;
        if (pressed != null)
        {
            SpriteState state = button.spriteState;
            state.highlightedSprite = normal;
            state.pressedSprite = pressed;
            state.selectedSprite = normal;
            state.disabledSprite = normal;
            button.spriteState = state;
        }
        return button;
    }

    private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Vector2 position, Vector2 size, bool preserveAspect)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.layer = 5;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = child.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = preserveAspect;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 position, Vector2 rectSize)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.layer = 5;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = rectSize;
        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color32(74, 35, 14, 255);
        text.alignment = alignment;
        text.text = value;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject child = new GameObject(name, typeof(RectTransform));
        child.layer = 5;
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static Sprite LoadSprite(string path, int index)
    {
        return LoadSprites(path).ElementAtOrDefault(index);
    }

    private static Sprite[] LoadSprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.name == objectName)
                    return transform.gameObject;
            }
        }
        return null;
    }

    private static Transform FindDeepChild(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child;
        }
        return null;
    }
}
#endif
