#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using User_Interface;

public static class SetupQuitConfirmationUI
{
    private const string PrefabPath = "Assets/Prefabs/User Interface/Core/Pauze Menu.prefab";
    private const string CoreScenePath = "Assets/MainScenes/Core 1.unity";
    private const string BackgroundPath = "Assets/Sprites/settings-loadgame-ui/nền tùy chọn yes-no.png";
    private const string YesPath = "Assets/Sprites/Buttons/other-button/yes-có.png";
    private const string NoPath = "Assets/Sprites/Buttons/other-button/no-không.png";
    private const string FontPath = "Assets/fonts/Dùng cho text khác/binhthuong nhat'.asset";
    private const string VersionKey = "Meadom.QuitConfirmationUI.Version";
    private const int Version = 3;

    [InitializeOnLoadMethod]
    private static void InstallAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorPrefs.GetInt(VersionKey, 0) >= Version)
                return;
            Install();
            EditorPrefs.SetInt(VersionKey, Version);
        };
    }

    [MenuItem("Tools/Meadom UI/Build Quit Yes-No Confirmation")]
    public static void InstallFromMenu() => Install();

    public static void InstallFromCommandLine() => Install();

    private static void Install()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            PauseGameMenuController controller = prefabRoot.GetComponent<PauseGameMenuController>();
            if (controller == null)
                throw new MissingReferenceException("PauseGameMenuController was not found in Pauze Menu.prefab.");

            BuildFor(controller);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Scene scene = SceneManager.GetSceneByPath(CoreScenePath);
        bool openedByTool = !scene.IsValid() || !scene.isLoaded;
        if (openedByTool)
            scene = EditorSceneManager.OpenScene(CoreScenePath, OpenSceneMode.Additive);
        PauseGameMenuController sceneController = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PauseGameMenuController>(true))
            .FirstOrDefault();
        if (sceneController == null)
            throw new MissingReferenceException("PauseGameMenuController was not found in Core 1.");

        // Keep the editable scene instance synchronized as well. Core 1 carries
        // local UI overrides, so updating only the prefab leaves its Question
        // Text at the old font size and alignment settings.
        BuildFor(sceneController);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedByTool)
            EditorSceneManager.CloseScene(scene, true);
        Debug.Log("Quit Yes/No confirmation installed in Pauze Menu.prefab and Core 1.unity.");
    }

    private static void BuildFor(PauseGameMenuController controller)
    {
        Transform contents = controller.transform.Find("Contents");
        if (contents == null)
            throw new MissingReferenceException("Pause menu does not contain Contents.");

        Transform previous = contents.Find("Quit Confirmation UI");
        if (previous != null)
            Object.DestroyImmediate(previous.gameObject);

        GameObject root = CreateUiObject("Quit Confirmation UI", contents, typeof(CanvasRenderer), typeof(Image));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect);
        Image shade = root.GetComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.55f);
        shade.raycastTarget = true;

        GameObject panel = CreateUiObject("Panel Yes No", root.transform, typeof(CanvasRenderer), typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        Center(panelRect, Vector2.zero, new Vector2(205f, 119f));
        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = LoadSprite(BackgroundPath);
        panelImage.preserveAspect = false;
        panelImage.raycastTarget = true;

        GameObject textObject = CreateUiObject("Question Text", panel.transform,
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        Center(textRect, new Vector2(0f, 24f), new Vector2(174f, 28f));
        TextMeshProUGUI question = textObject.GetComponent<TextMeshProUGUI>();
        question.text = "DO YOU WANT TO QUIT THE GAME?";
        question.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        question.fontSize = 18f;
        question.fontStyle = FontStyles.Bold;
        question.color = new Color32(66, 37, 17, 255);
        question.alignment = TextAlignmentOptions.Center;
        question.textWrappingMode = TextWrappingModes.NoWrap;
        question.overflowMode = TextOverflowModes.Overflow;
        question.raycastTarget = false;

        CreateButton(panel.transform, controller, "Button Yes", LoadSprite(YesPath),
            new Vector2(-43f, -27f), controller.ConfirmQuit);
        CreateButton(panel.transform, controller, "Button No", LoadSprite(NoPath),
            new Vector2(43f, -27f), controller.CancelQuit);

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("quitConfirmationRoot").objectReferenceValue = root;
        serializedController.FindProperty("quitConfirmationText").objectReferenceValue = question;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        root.transform.SetAsLastSibling();
        // Left active so it can be seen in Edit Mode. PauseGameMenuController
        // hides it on Awake in Play Mode.
        root.SetActive(true);
        EditorUtility.SetDirty(controller);
    }

    private static void CreateButton(Transform parent, PauseGameMenuController controller,
        string name, Sprite sprite, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = CreateUiObject(name, parent, typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        Center(rect, position, sprite != null ? sprite.rect.size : new Vector2(66f, 33f));

        Image image = buttonObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = true;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.86f);
        colors.pressedColor = new Color(0.76f, 0.76f, 0.76f, 1f);
        button.colors = colors;
        UnityEventTools.AddPersistentListener(button.onClick, action);
    }

    private static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    private static GameObject CreateUiObject(string name, Transform parent, params System.Type[] components)
    {
        GameObject result = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray());
        result.layer = LayerMask.NameToLayer("UI");
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }
}
#endif
