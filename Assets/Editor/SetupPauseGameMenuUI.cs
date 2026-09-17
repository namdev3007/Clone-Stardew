#if UNITY_EDITOR
using System.Linq;
using Event.Events;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using User_Interface;

public static class SetupPauseGameMenuUI
{
    private const string PrefabPath = "Assets/Prefabs/User Interface/Core/Pauze Menu.prefab";
    private const string ButtonSpriteFolder = "Assets/Sprites/Buttons/tieng-anh/";
    private const string SettingsSpriteFolder = "Assets/Sprites/settings-loadgame-ui/";
    private const int AlignmentVersion = 3;

    [InitializeOnLoadMethod]
    private static void AlignAfterCompile()
    {
        string key = "Meadom.PauseGameMainAlignment." + Application.dataPath.GetHashCode();
        if (EditorPrefs.GetInt(key, 0) >= AlignmentVersion)
            return;

        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AlignExistingContent();
            EditorPrefs.SetInt(key, AlignmentVersion);
        };
    }

    [MenuItem("Tools/Meadom UI/Align Pause Game Main Content")]
    public static void AlignExistingContent()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            Transform contents = prefabRoot.transform.Find("Contents");
            Transform mainPanel = contents != null
                ? contents.Find("Pause Game UI/Pause Game Main") ?? contents.Find("Pause Game Main")
                : null;
            if (mainPanel == null)
            {
                throw new MissingReferenceException("Pauze Menu prefab does not contain Contents/Pause Game Main.");
            }

            // Preserve the background size and move all controls to their own sibling layer.
            Transform buttonLayer = EnsureSeparatedStructure(contents, mainPanel);
            AlignPanelContents(buttonLayer, false);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            AssetDatabase.SaveAssets();
            AlignOpenSceneInstances();
            Debug.Log("Pause Game Main content aligned. Background RectTransform was preserved.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void AlignOpenSceneInstances()
    {
        bool changed = false;
        foreach (Transform candidate in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (candidate == null || candidate.name != "Pause Game Main" ||
                !candidate.gameObject.scene.IsValid() || EditorUtility.IsPersistent(candidate.gameObject))
                continue;

            Transform contents = candidate.parent != null && candidate.parent.name == "Pause Game UI"
                ? candidate.parent.parent
                : candidate.parent;
            Transform buttonLayer = EnsureSeparatedStructure(contents, candidate);
            AlignPanelContents(buttonLayer, true);
            EditorSceneManager.MarkSceneDirty(candidate.gameObject.scene);
            changed = true;
        }

        if (changed)
            EditorSceneManager.SaveOpenScenes();
    }

    private static void AlignPanelContents(Transform mainPanel, bool centerPanel)
    {
        RectTransform panelRect = mainPanel as RectTransform;
        if (centerPanel && panelRect != null)
            panelRect.anchoredPosition = Vector2.zero;

        SetChildPosition(mainPanel, "Title Pause", new Vector2(0f, 104f));
        AlignButton(mainPanel, "Button Continue", 54f);
        AlignButton(mainPanel, "Button Saves", 16f);
        AlignButton(mainPanel, "Button Settings", -22f);
        AlignButton(mainPanel, "Button Quit To Menu", -60f);
        AlignButton(mainPanel, "Button Quit To Desktop", -98f);
        ConfigureLocalizedVisuals(mainPanel);
    }

    private static Transform EnsureSeparatedStructure(Transform contents, Transform background)
    {
        if (contents == null || background == null)
            return background;

        Transform group = contents.Find("Pause Game UI");
        if (group == null)
        {
            GameObject groupObject = CreateRectObject("Pause Game UI", contents);
            group = groupObject.transform;
            SetStretch((RectTransform)group);
            group.SetSiblingIndex(background.GetSiblingIndex());
        }

        if (background.parent != group)
        {
            background.SetParent(group, false);
            if (background is RectTransform backgroundRect)
                backgroundRect.anchoredPosition = Vector2.zero;
        }

        Transform buttons = group.Find("Pause Game Buttons");
        if (buttons == null)
        {
            GameObject buttonsObject = CreateRectObject("Pause Game Buttons", group);
            buttons = buttonsObject.transform;
            SetStretch((RectTransform)buttons);
        }

        Transform[] controls = background.Cast<Transform>().ToArray();
        foreach (Transform control in controls)
            control.SetParent(buttons, false);

        background.SetAsFirstSibling();
        buttons.SetAsLastSibling();

        PauseGameMenuController controller = contents.GetComponentInParent<PauseGameMenuController>(true);
        if (controller != null)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("pauseMenuRoot").objectReferenceValue = group.gameObject;
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }

        return buttons;
    }

    private static void ConfigureLocalizedVisuals(Transform panel)
    {
        ConfigureLocalizedVisual(panel, "Title Pause",
            LoadSprite(ButtonSpriteFolder + "pause.png", "pause_0"), null,
            LoadSprite("Assets/Sprites/Buttons/tieng-viet/tam-dung.png", "tam-dung_0"), null, false);
        ConfigureLocalizedVisual(panel, "Button Continue",
            LoadSprite(ButtonSpriteFolder + "continue.png", "continue_0"),
            LoadSprite(ButtonSpriteFolder + "continue.png", "continue_1", false),
            LoadSprite("Assets/Sprites/Buttons/tieng-viet/tiep-tuc.png", "tiep-tuc_0"),
            LoadSprite("Assets/Sprites/Buttons/tieng-viet/tiep-tuc.png", "tiep-tuc_1", false), true);
        ConfigureLocalizedVisual(panel, "Button Saves",
            LoadSprite(ButtonSpriteFolder + "save.png", "save_0"),
            LoadSprite(ButtonSpriteFolder + "save.png", "save_1", false),
            LoadSprite("Assets/Sprites/Buttons/tieng-viet/luu.png", "luu_0"),
            LoadSprite("Assets/Sprites/Buttons/tieng-viet/luu.png", "luu_1", false), true);
        ConfigureLocalizedVisual(panel, "Button Settings",
            LoadSprite(ButtonSpriteFolder + "settings-mini.png", "settings-mini_0"),
            LoadSprite(ButtonSpriteFolder + "settings-mini.png", "settings-mini_1", false),
            LoadSprite("Assets/Sprites/Buttons/tieng-viet/cai-dat-mini.png", "cai-dat-mini_0"),
            LoadSprite("Assets/Sprites/Buttons/tieng-viet/cai-dat-mini.png", "cai-dat-mini_1", false), true);
        ConfigureLocalizedVisual(panel, "Button Quit To Menu",
            LoadSprite(ButtonSpriteFolder + "quit-to-menu.png", "quit-to-menu_0"),
            LoadSprite(ButtonSpriteFolder + "quit-to-menu.png", "quit-to-menu_1", false),
            LoadSprite("Assets/Sprites/Buttons/tieng-viet/ve-menu-chinh.png", "ve-menu-chinh_0"),
            LoadSprite("Assets/Sprites/Buttons/tieng-viet/ve-menu-chinh.png", "ve-menu-chinh_1", false), true);
        ConfigureLocalizedVisual(panel, "Button Quit To Desktop",
            LoadSprite(ButtonSpriteFolder + "quit-to-desktop.png", "quit-to-desktop_0"),
            LoadSprite(ButtonSpriteFolder + "quit-to-desktop.png", "quit-to-desktop_1", false),
            LoadSprite("Assets/Sprites/Buttons/tieng-viet/thoat-ra-man-hinh-chinh.png", "thoat-ra-man-hinh-chinh_0"),
            LoadSprite("Assets/Sprites/Buttons/tieng-viet/thoat-ra-man-hinh-chinh.png", "thoat-ra-man-hinh-chinh_1", false), true);
    }

    private static void ConfigureLocalizedVisual(Transform panel, string childName, Sprite englishNormal,
        Sprite englishSelected, Sprite vietnameseNormal, Sprite vietnameseSelected, bool nativeSize)
    {
        Transform child = panel.Find(childName);
        if (child == null)
            return;

        LocalizedSpriteButton localizer = child.GetComponent<LocalizedSpriteButton>();
        if (localizer == null)
            localizer = child.gameObject.AddComponent<LocalizedSpriteButton>();
        localizer.Configure(child.GetComponent<Image>(), child.GetComponent<Button>(), englishNormal,
            englishSelected, vietnameseNormal, vietnameseSelected, nativeSize);
    }

    private static void SetChildPosition(Transform parent, string childName, Vector2 position)
    {
        RectTransform rect = parent.Find(childName) as RectTransform;
        if (rect != null)
            rect.anchoredPosition = position;
    }

    private static void AlignButton(Transform parent, string buttonName, float y)
    {
        Button button = parent.Find(buttonName)?.GetComponent<Button>();
        if (button == null)
            return;

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        Image image = button.targetGraphic as Image;
        if (image != null && image.sprite != null)
            buttonRect.sizeDelta = image.sprite.rect.size;
        buttonRect.anchoredPosition = new Vector2(0f, y);

        RectTransform indicatorRect = button.transform.Find("Hover Indicator") as RectTransform;
        if (indicatorRect != null)
        {
            indicatorRect.anchoredPosition = new Vector2(
                -(buttonRect.sizeDelta.x * 0.5f + indicatorRect.sizeDelta.x * 0.5f + 4f),
                0f);
        }
    }

    [MenuItem("Tools/Meadom UI/Rebuild Pause Game Menu")]
    public static void Rebuild()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            Transform contents = prefabRoot.transform.Find("Contents");
            if (contents == null)
            {
                throw new MissingReferenceException("Pauze Menu prefab does not contain Contents.");
            }

            DeleteExisting(contents, "Pause Game UI");
            DeleteExisting(contents, "Pause Game Main");

            SetChildActive(contents, "Buttons", false);
            SetChildActive(contents, "Window", false);

            Transform settingsRoot = contents.Find("Window_Options");
            if (settingsRoot != null)
            {
                settingsRoot.gameObject.SetActive(false);
                DeleteExisting(settingsRoot, "Button Back To Pause");
            }

            GameObject pauseGroup = CreateRectObject("Pause Game UI", contents);
            SetStretch(pauseGroup.GetComponent<RectTransform>());

            GameObject mainPanel = CreateImageObject(
                "Pause Game Main",
                pauseGroup.transform,
                LoadSprite(SettingsSpriteFolder + "nền settings.png", "nền settings_0"));
            RectTransform panelRect = mainPanel.GetComponent<RectTransform>();
            SetCentered(panelRect, Vector2.zero, new Vector2(205f, 279f));
            mainPanel.GetComponent<Image>().preserveAspect = true;

            GameObject buttonLayer = CreateRectObject("Pause Game Buttons", pauseGroup.transform);
            SetStretch(buttonLayer.GetComponent<RectTransform>());

            PauseGameMenuController controller = prefabRoot.GetComponent<PauseGameMenuController>();
            if (controller == null)
            {
                controller = prefabRoot.AddComponent<PauseGameMenuController>();
            }

            SerializedObject controllerObject = new SerializedObject(controller);
            controllerObject.FindProperty("pauseMenuRoot").objectReferenceValue = pauseGroup;
            controllerObject.FindProperty("settingsRoot").objectReferenceValue = settingsRoot != null ? settingsRoot.gameObject : null;
            controllerObject.FindProperty("pauseStateEvent").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<BoolEvent>("Assets/ScriptableObjects/Events/OnPauzeGame.asset");
            controllerObject.FindProperty("displayPauseMenuEvent").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<BoolEvent>("Assets/ScriptableObjects/Events/Pauzing/Display Pauze Menu.asset");
            controllerObject.FindProperty("mainMenuScene").stringValue = "StartMenu 1";
            controllerObject.ApplyModifiedPropertiesWithoutUndo();

            CreateDecorativeImage(buttonLayer.transform, "Title Pause",
                LoadSprite(ButtonSpriteFolder + "pause.png", "pause_0"),
                new Vector2(0f, 104f), new Vector2(155f, 50f));

            CreateMenuButton(buttonLayer.transform, controller, "Button Continue", "continue.png", "continue_0", "continue_1", 54f,
                controller.ContinueGame);
            CreateMenuButton(buttonLayer.transform, controller, "Button Saves", "save.png", "save_0", "save_1", 16f,
                controller.SaveGame);
            CreateMenuButton(buttonLayer.transform, controller, "Button Settings", "settings-mini.png", "settings-mini_0", "settings-mini_1", -22f,
                controller.OpenSettings);
            CreateMenuButton(buttonLayer.transform, controller, "Button Quit To Menu", "quit-to-menu.png", "quit-to-menu_0", "quit-to-menu_1", -60f,
                controller.QuitToMenu);
            CreateMenuButton(buttonLayer.transform, controller, "Button Quit To Desktop", "quit-to-desktop.png", "quit-to-desktop_0", "quit-to-desktop_1", -98f,
                controller.QuitToDesktop);

            if (settingsRoot != null)
            {
                CreateBackButton(settingsRoot, controller);
            }

            pauseGroup.transform.SetAsLastSibling();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Pause Game UI rebuilt in Pauze Menu.prefab. Core 1 uses this prefab automatically.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void CreateMenuButton(Transform parent, PauseGameMenuController controller,
        string objectName, string textureName, string normalName, string selectedName, float y,
        UnityEngine.Events.UnityAction action)
    {
        Sprite normal = LoadSprite(ButtonSpriteFolder + textureName, normalName);
        Sprite selected = LoadSprite(ButtonSpriteFolder + textureName, selectedName, false);

        GameObject buttonObject = CreateImageObject(objectName, parent, normal);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        Vector2 buttonSize = normal.rect.size;
        SetCentered(rect, new Vector2(6f, y), buttonSize);

        Image image = buttonObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = true;

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = selected != null ? Selectable.Transition.SpriteSwap : Selectable.Transition.ColorTint;
        if (selected != null)
        {
            SpriteState state = button.spriteState;
            state.highlightedSprite = selected;
            state.selectedSprite = selected;
            state.pressedSprite = selected;
            button.spriteState = state;
        }
        UnityEventTools.AddPersistentListener(button.onClick, action);

        GameObject indicator = CreateImageObject("Hover Indicator", buttonObject.transform,
            LoadSprite(SettingsSpriteFolder + "text-dùng cho ui-settings/effect-chọn nút-lớn.png", "effect-chọn nút-lớn_0"));
        RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
        Vector2 indicatorSize = new Vector2(23f, 26f);
        float indicatorX = -(buttonSize.x * 0.5f + indicatorSize.x * 0.5f + 4f);
        SetCentered(indicatorRect, new Vector2(indicatorX, 0f), indicatorSize);
        indicator.GetComponent<Image>().preserveAspect = true;
        indicator.GetComponent<Image>().raycastTarget = false;
        indicator.SetActive(false);

        PauseMenuHoverIndicator hover = buttonObject.AddComponent<PauseMenuHoverIndicator>();
        SerializedObject hoverObject = new SerializedObject(hover);
        hoverObject.FindProperty("indicator").objectReferenceValue = indicator;
        hoverObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateBackButton(Transform settingsRoot, PauseGameMenuController controller)
    {
        Sprite normal = LoadSprite("Assets/Sprites/Buttons/other-button/back-lùi về.png", null);
        GameObject buttonObject = CreateImageObject("Button Back To Pause", settingsRoot, normal);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(28f, -24f);
        rect.sizeDelta = new Vector2(42f, 26f);

        Image image = buttonObject.GetComponent<Image>();
        image.preserveAspect = true;
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        UnityEventTools.AddPersistentListener(button.onClick, controller.ShowPauseMenu);
    }

    private static GameObject CreateImageObject(string name, Transform parent, Sprite sprite)
    {
        GameObject result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        result.layer = LayerMask.NameToLayer("UI");
        result.transform.SetParent(parent, false);
        result.GetComponent<Image>().sprite = sprite;
        return result;
    }

    private static GameObject CreateRectObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.layer = LayerMask.NameToLayer("UI");
        result.transform.SetParent(parent, false);
        return result;
    }

    private static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void CreateDecorativeImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        GameObject imageObject = CreateImageObject(name, parent, sprite);
        SetCentered(imageObject.GetComponent<RectTransform>(), position, size);
        Image image = imageObject.GetComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static Sprite LoadSprite(string path, string spriteName, bool required = true)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        Sprite result = string.IsNullOrEmpty(spriteName)
            ? sprites.FirstOrDefault()
            : sprites.FirstOrDefault(sprite => sprite.name == spriteName);

        if (result == null && required)
        {
            throw new MissingReferenceException($"Could not load sprite '{spriteName}' from '{path}'.");
        }

        return result;
    }

    private static void DeleteExisting(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }
    }

    private static void SetChildActive(Transform parent, string childName, bool active)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            child.gameObject.SetActive(active);
        }
    }
}
#endif
