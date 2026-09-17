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
public static class SyncShopItemRows
{
    private const string CoreScenePath = "Assets/MainScenes/Core 1.unity";
    private const string ShopPrefabPath = "Assets/Prefabs/User Interface/Core/Shop UI.prefab";
    private const string ItemTemplatePath = "Assets/Prefabs/Shop Item 2.prefab";
    private const string BagAmountFontPath = "Assets/fonts/Dùng cho text khác/for shop.asset";
    private const string CurrencyItemPath = "Assets/ScriptableObjects/Items/Tools/Item_Gold.asset";
    private const string SessionKey = "Meadom.ShopItemRows.Synced.V6";
    private const float RowSpacing = 25f;

    static SyncShopItemRows()
    {
        EditorApplication.delayCall += SyncOnce;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += SyncOnce;
    }

    private static void SyncOnce()
    {
        if (SessionState.GetBool(SessionKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (SyncAll())
            SessionState.SetBool(SessionKey, true);
    }

    [MenuItem("Tools/Game/Sync Shop Item Rows From Prefab")]
    public static void SyncFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (SyncAll())
            SessionState.SetBool(SessionKey, true);
    }

    private static bool SyncAll()
    {
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(ItemTemplatePath);
        if (template == null)
        {
            Debug.LogError("Shop item template was not found: " + ItemTemplatePath);
            return false;
        }

        SyncReusableShopPrefab(template);
        SyncCoreScene(template);
        AssetDatabase.SaveAssets();
        Debug.Log("Shop Item 1-3 now use Shop Item 2.prefab. Other shop layout objects were preserved.");
        return true;
    }

    private static void SyncReusableShopPrefab(GameObject template)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(ShopPrefabPath);
        try
        {
            if (ReplaceRows(contents, template))
                PrefabUtility.SaveAsPrefabAsset(contents, ShopPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void SyncCoreScene(GameObject template)
    {
        Scene scene = SceneManager.GetSceneByPath(CoreScenePath);
        bool openedTemporarily = !scene.IsValid() || !scene.isLoaded;
        if (openedTemporarily)
            scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Additive);

        GameObject shopRoot = FindInScene(scene, "Shop UI");
        if (shopRoot != null && ReplaceRows(shopRoot, template))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (openedTemporarily)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static bool ReplaceRows(GameObject shopRoot, GameObject template)
    {
        Transform frame = FindDeepChild(shopRoot.transform, "Window_Shop");
        ShopWindowController controller = shopRoot.GetComponent<ShopWindowController>();
        if (frame == null || controller == null)
            return false;

        Transform[] oldRows = frame.Cast<Transform>()
            .Where(child => child.name.StartsWith("Shop Item ", StringComparison.Ordinal))
            .ToArray();

        int firstSibling = oldRows.Length > 0 ? oldRows.Min(row => row.GetSiblingIndex()) : 3;
        RectTransform templateRect = template.GetComponent<RectTransform>();
        Vector2 topPosition = templateRect != null ? templateRect.anchoredPosition : new Vector2(-59.2f, 68.6f);

        foreach (Transform row in oldRows)
            UnityEngine.Object.DestroyImmediate(row.gameObject);

        GameObject[] rows = new GameObject[3];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = (GameObject)PrefabUtility.InstantiatePrefab(template, frame);
            rows[i].name = "Shop Item " + (i + 1);
            RectTransform rect = rows[i].GetComponent<RectTransform>();
            rect.anchoredPosition = topPosition + Vector2.down * (RowSpacing * i);
            rect.SetSiblingIndex(firstSibling + i);
        }

        AssignControllerRows(controller, rows);
        ApplyButtonSprites(shopRoot);
        ApplyBagAmountFont(shopRoot);
        AssignCurrencyItem(controller);
        controller.ApplyEditorPreview();
        EditorUtility.SetDirty(controller);
        return true;
    }

    private static void ApplyButtonSprites(GameObject shopRoot)
    {
        Sprite[] buy = LoadSprites("Assets/Sprites/Buttons/tieng-anh/buy.png");
        Sprite[] sell = LoadSprites("Assets/Sprites/Buttons/tieng-anh/sell.png");
        Sprite[] quantity = LoadSprites("Assets/Sprites/bag-shop-ui/shop/cộng-trừ.png");
        Sprite[] pages = LoadSprites("Assets/Sprites/bag-shop-ui/shop/nút qua trang mới-shop.png");

        ConfigureSpriteButton(shopRoot, "Button_BUY", buy.ElementAtOrDefault(0), buy.ElementAtOrDefault(1));
        ConfigureSpriteButton(shopRoot, "Button_SELL", sell.ElementAtOrDefault(0), sell.ElementAtOrDefault(1));
        ConfigureSpriteButton(shopRoot, "Plus", quantity.ElementAtOrDefault(0), quantity.ElementAtOrDefault(1));
        ConfigureSpriteButton(shopRoot, "Minus", quantity.ElementAtOrDefault(2), quantity.ElementAtOrDefault(3));
        ConfigureSpriteButton(shopRoot, "Previous Page", pages.ElementAtOrDefault(0), pages.ElementAtOrDefault(2));
        ConfigureSpriteButton(shopRoot, "Next Page", pages.ElementAtOrDefault(1), pages.ElementAtOrDefault(3));
    }

    private static void ConfigureSpriteButton(GameObject root, string objectName, Sprite normal, Sprite pressed)
    {
        Transform transform = FindDeepChild(root.transform, objectName);
        Button button = transform != null ? transform.GetComponent<Button>() : null;
        Image image = transform != null ? transform.GetComponent<Image>() : null;
        if (button == null || image == null || normal == null)
            return;

        image.sprite = normal;
        image.color = Color.white;
        button.transition = Selectable.Transition.SpriteSwap;
        SpriteState state = button.spriteState;
        state.highlightedSprite = normal;
        state.pressedSprite = pressed;
        state.selectedSprite = normal;
        state.disabledSprite = normal;
        button.spriteState = state;
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(image);
    }

    private static void ApplyBagAmountFont(GameObject shopRoot)
    {
        TMP_FontAsset amountFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BagAmountFontPath);
        if (amountFont == null)
            return;

        foreach (TextMeshProUGUI text in shopRoot.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name != "Text_Amount")
                continue;

            text.font = amountFont;
            EditorUtility.SetDirty(text);
        }
    }

    private static void AssignCurrencyItem(ShopWindowController controller)
    {
        ItemData currency = AssetDatabase.LoadAssetAtPath<ItemData>(CurrencyItemPath);
        if (currency == null)
        {
            Debug.LogError("Shop currency item was not found: " + CurrencyItemPath);
            return;
        }

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("currencyItem").objectReferenceValue = currency;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Sprite[] LoadSprites(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name)
            .ToArray();
    }

    private static void AssignControllerRows(ShopWindowController controller, GameObject[] rows)
    {
        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty slots = serializedController.FindProperty("shopSlots");
        slots.arraySize = rows.Length;

        for (int i = 0; i < rows.Length; i++)
        {
            GameObject row = rows[i];
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("button").objectReferenceValue = row.GetComponent<Button>();
            slot.FindPropertyRelative("background").objectReferenceValue = row.GetComponent<Image>();
            slot.FindPropertyRelative("icon").objectReferenceValue = FindComponent<Image>(row.transform, "Image_Icon");
            slot.FindPropertyRelative("primaryText").objectReferenceValue = FindComponent<TextMeshProUGUI>(row.transform, "Text_Name");

            TextMeshProUGUI price = FindComponent<TextMeshProUGUI>(row.transform, "Text_mony");
            if (price == null)
                price = FindComponent<TextMeshProUGUI>(row.transform, "Text_Name (1)");
            slot.FindPropertyRelative("secondaryText").objectReferenceValue = price;
        }

        serializedController.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T FindComponent<T>(Transform root, string objectName) where T : Component
    {
        Transform target = FindDeepChild(root, objectName);
        return target != null ? target.GetComponent<T>() : null;
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

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = FindDeepChild(root.transform, objectName);
            if (found != null)
                return found.gameObject;
        }
        return null;
    }
}
#endif
