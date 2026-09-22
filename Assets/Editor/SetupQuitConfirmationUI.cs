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
    private const string SharedConfirmationPrefabPath = "Assets/Prefabs/User Interface/Windows/Confirmation Window.prefab";
    private const string CoreScenePath = "Assets/MainScenes/Core 1.unity";
    private const string StartMenuScenePath = "Assets/MainScenes/StartMenu 1.unity";
    private const string ConfirmationReferencePath = "Assets/ScriptableObjects/Referencing/Scriptable References/Confirmation Window.asset";
    private const string BackgroundPath = "Assets/Sprites/settings-loadgame-ui/nền tùy chọn yes-no.png";
    private const string YesPath = "Assets/Sprites/Buttons/other-button/yes-có.png";
    private const string NoPath = "Assets/Sprites/Buttons/other-button/no-không.png";
    private const string FontPath = "Assets/fonts/Dùng cho text khác/binhthuong nhat'.asset";
    private const string VersionKey = "Meadom.QuitConfirmationUI.Version";
    private const int Version = 10;

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
        BuildSharedConfirmationPrefab();

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
        BuildStartMenuSceneConfirmation();
        Debug.Log("Quit Yes/No confirmation installed in Pauze Menu.prefab, Core 1.unity and StartMenu edit-time UI.");
    }

    private static void BuildStartMenuSceneConfirmation()
    {
        GameObject confirmationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SharedConfirmationPrefabPath);
        Referencing.Scriptable_Reference.ScriptableReference confirmationReference =
            AssetDatabase.LoadAssetAtPath<Referencing.Scriptable_Reference.ScriptableReference>(ConfirmationReferencePath);
        if (confirmationPrefab == null || confirmationReference == null)
            throw new MissingReferenceException("StartMenu confirmation prefab or reference asset is missing.");

        Scene scene = SceneManager.GetSceneByPath(StartMenuScenePath);
        bool openedByTool = !scene.IsValid() || !scene.isLoaded;
        if (openedByTool)
            scene = EditorSceneManager.OpenScene(StartMenuScenePath, OpenSceneMode.Additive);

        try
        {
            ConfirmationWindow window = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<ConfirmationWindow>(true))
                .FirstOrDefault(component => component.name == "Confirmation Window" ||
                                             component.name == "Confirmation Window(Clone)");

            if (window == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(confirmationPrefab, scene);
                instance.name = "Confirmation Window";
                window = instance.GetComponent<ConfirmationWindow>();
            }

            if (window == null)
                throw new MissingReferenceException("ConfirmationWindow component is missing from the shared prefab.");

            window.gameObject.name = "Confirmation Window";
            window.transform.SetAsLastSibling();

            RectTransform panelRect = window.transform.Find("Panel Yes No") as RectTransform;
            if (panelRect == null)
                throw new MissingReferenceException("Panel Yes No is missing from the StartMenu confirmation window.");
            Center(panelRect, new Vector2(0f, 9.0477f), new Vector2(248.1101f, 149.869f));

            Transform buttonYes = panelRect.Find("Button Yes");
            Transform buttonNo = panelRect.Find("Button No");
            if (buttonYes != null)
                buttonYes.gameObject.SetActive(true);
            if (buttonNo != null)
                buttonNo.gameObject.SetActive(true);

            SerializedObject serializedWindow = new SerializedObject(window);
            serializedWindow.FindProperty("sceneReference").objectReferenceValue = confirmationReference;
            serializedWindow.FindProperty("hideSceneInstanceOnAwake").boolValue = true;
            serializedWindow.ApplyModifiedPropertiesWithoutUndo();

            // Visible and editable in StartMenu. Awake registers and hides this
            // exact object before the first Play Mode frame.
            window.gameObject.SetActive(true);
            EditorUtility.SetDirty(window);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (openedByTool && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void BuildSharedConfirmationPrefab()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(SharedConfirmationPrefabPath);
        try
        {
            // Keep the existing prefab root so its GUID/local identity remains
            // valid for ScriptableReference users in StartMenu and gameplay.
            for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(prefabRoot.transform.GetChild(i).gameObject);

            Component[] rootComponents = prefabRoot.GetComponents<Component>();
            // Remove dependants (GraphicRaycaster/Scaler/etc.) before Canvas.
            for (int i = rootComponents.Length - 1; i >= 0; i--)
            {
                Component component = rootComponents[i];
                if (!(component is Transform))
                    Object.DestroyImmediate(component);
            }

            prefabRoot.name = "Confirmation Window";
            prefabRoot.layer = LayerMask.NameToLayer("UI");
            RectTransform rootRect = prefabRoot.transform as RectTransform;
            if (rootRect != null)
            {
                rootRect.localScale = Vector3.one;
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.pivot = new Vector2(0.5f, 0.5f);
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
            }

            Canvas canvas = prefabRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = prefabRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 2f;
            prefabRoot.AddComponent<GraphicRaycaster>();

            Image shade = prefabRoot.AddComponent<Image>();
            shade.color = new Color(0f, 0f, 0f, 0.55f);
            shade.raycastTarget = true;

            GameObject panel = CreateUiObject("Panel Yes No", prefabRoot.transform,
                typeof(CanvasRenderer), typeof(Image));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            Center(panelRect, Vector2.zero, new Vector2(205f, 119f));
            Image panelImage = panel.GetComponent<Image>();
            panelImage.sprite = LoadSprite(BackgroundPath);
            panelImage.preserveAspect = false;
            panelImage.raycastTarget = true;

            GameObject textObject = CreateUiObject("Question Text", panel.transform,
                typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            Center(textObject.GetComponent<RectTransform>(), new Vector2(0f, 24f), new Vector2(174f, 28f));
            TextMeshProUGUI question = textObject.GetComponent<TextMeshProUGUI>();
            question.text = "DO YOU WANT TO CONTINUE?";
            question.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            question.fontSize = 18f;
            question.fontStyle = FontStyles.Bold;
            question.color = new Color32(0xF7, 0xCA, 0x92, 0xFF);
            question.alignment = TextAlignmentOptions.Center;
            question.textWrappingMode = TextWrappingModes.Normal;
            question.overflowMode = TextOverflowModes.Overflow;
            question.raycastTarget = false;

            Button yes = CreateConfirmationButton(panel.transform, "Button Yes", LoadSprite(YesPath),
                new Vector2(-43f, -27f));
            Button no = CreateConfirmationButton(panel.transform, "Button No", LoadSprite(NoPath),
                new Vector2(43f, -27f));
            ConfirmationWindow confirmation = prefabRoot.AddComponent<ConfirmationWindow>();
            SerializedObject serializedWindow = new SerializedObject(confirmation);
            serializedWindow.FindProperty("textQuestion").objectReferenceValue = question;
            serializedWindow.FindProperty("buttonYes").objectReferenceValue = yes;
            serializedWindow.FindProperty("buttonNo").objectReferenceValue = no;
            serializedWindow.FindProperty("buttonAccept").objectReferenceValue = null;
            serializedWindow.ApplyModifiedPropertiesWithoutUndo();

            prefabRoot.SetActive(false);
            EditorUtility.SetDirty(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, SharedConfirmationPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
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

    private static Button CreateConfirmationButton(Transform parent, string name, Sprite sprite, Vector2 position)
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
        return button;
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
