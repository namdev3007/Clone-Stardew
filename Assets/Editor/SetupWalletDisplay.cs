#if UNITY_EDITOR
using System.Linq;
using Item;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using User_Interface;

/// <summary>
/// Builds the HUD money board (top-right, "bảng-tiền" sprite) inside the
/// Inventory Bar prefab so it is visible in Edit Mode and in every scene that
/// uses the prefab.
/// </summary>
public static class SetupWalletDisplay
{
    private const string PrefabPath = "Assets/Prefabs/User Interface/Core/Inventory Bar.prefab";
    private const string BoardSpritePath = "Assets/Sprites/Inventory Bar/bảng-tiền.png";
    private const string FontPath = "Assets/fonts/Dùng cho text khác/for shop.asset";
    private const string CurrencyItemPath = "Assets/ScriptableObjects/Items/Tools/Item_Gold.asset";
    private const string PanelName = "Money Panel";
    private const string VersionKey = "Meadom.WalletDisplay.Version";
    private const int Version = 1;

    [InitializeOnLoadMethod]
    private static void InstallAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetInt(VersionKey, 0) >= Version)
                return;
            if (Install())
                EditorPrefs.SetInt(VersionKey, Version);
        };
    }

    [MenuItem("Tools/Meadom UI/Build HUD Money Board")]
    public static void InstallFromMenu() => Install();

    private static bool Install()
    {
        Sprite board = AssetDatabase.LoadAllAssetsAtPath(BoardSpritePath).OfType<Sprite>().FirstOrDefault();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        ItemData gold = AssetDatabase.LoadAssetAtPath<ItemData>(CurrencyItemPath);
        if (board == null || font == null || gold == null)
        {
            Debug.LogError("HUD money board: sprite, font or Item_Gold was not found.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform previous = root.transform.Find(PanelName);
            if (previous != null)
                Object.DestroyImmediate(previous.gameObject);

            GameObject panel = CreateUiObject(PanelName, root.transform, typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-6f, -6f);
            panelRect.sizeDelta = board.rect.size;
            Image image = panel.GetComponent<Image>();
            image.sprite = board;
            image.raycastTarget = false;

            GameObject textObject = CreateUiObject("Text_Money", panel.transform,
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            // Leave room for the coin drawn on the right side of the board.
            textRect.offsetMin = new Vector2(8f, 2f);
            textRect.offsetMax = new Vector2(-26f, -2f);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 14f;
            text.color = new Color32(255, 246, 222, 255);
            text.alignment = TextAlignmentOptions.MidlineRight;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.text = "0";

            panel.AddComponent<WalletDisplay>().Configure(gold, text);
            panel.transform.SetAsLastSibling();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Debug.Log("HUD money board added to Inventory Bar.prefab.");
        return true;
    }

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject result = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray());
        result.layer = LayerMask.NameToLayer("UI");
        result.transform.SetParent(parent, false);
        return result;
    }
}
#endif
