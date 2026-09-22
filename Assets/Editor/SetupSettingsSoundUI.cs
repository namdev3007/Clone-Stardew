#if UNITY_EDITOR
using System.Linq;
using Action.Actions;
using TMPro;
using User_Interface;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SetupSettingsSoundUI
{
    private const string PrefabPath = "Assets/Prefabs/User Interface/Core/Pauze Menu.prefab";
    private const string TrackPath = "Assets/Sprites/settings-loadgame-ui/cây âm thanh.png";
    private const string StatePath = "Assets/Sprites/settings-loadgame-ui/icon-âmthanh/icon-tăng-giảm-âm thanh.png";
    private const string MainLabelPath = "Assets/Sprites/settings-loadgame-ui/icon-âmthanh/main-sound.png";
    private const string MusicLabelPath = "Assets/Sprites/settings-loadgame-ui/icon-âmthanh/bgm.png";
    private const string SfxLabelPath = "Assets/Sprites/settings-loadgame-ui/icon-âmthanh/sound-effect.png";
    private const string SettingsBackgroundPath = "Assets/Sprites/settings-loadgame-ui/nền settings.png";
    private const string SettingsTitlePath = "Assets/Sprites/settings-loadgame-ui/text-dùng cho ui-settings/text-settings.png";
    private const string SoundsTitlePath = "Assets/Sprites/settings-loadgame-ui/text-dùng cho ui-settings/text-sounds.png";
    private const string LanguageTitlePath = "Assets/Sprites/settings-loadgame-ui/text-dùng cho ui-settings/text-language.png";
    private const string ControlsTitlePath = "Assets/Sprites/settings-loadgame-ui/text-dùng cho ui-settings/text-controls.png";
    private const string SettingsTitleViPath = "Assets/Sprites/settings-loadgame-ui/text-dùng cho ui-settings/text-cài đặt.png";
    private const string SoundsTitleViPath = "Assets/Sprites/settings-loadgame-ui/text-dùng cho ui-settings/text-âm thanh.png";
    private const string LanguageTitleViPath = "Assets/Sprites/settings-loadgame-ui/text-dùng cho ui-settings/text-ngôn ngữ.png";
    private const string ControlsTitleViPath = "Assets/Sprites/settings-loadgame-ui/text-dùng cho ui-settings/text-điều khiển.png";
    private const string VietnameseButtonPath = "Assets/Sprites/Buttons/tieng-viet/tieng-viet.png";
    private const string EnglishButtonPath = "Assets/Sprites/Buttons/tieng-anh/english.png";
    private const string SoundActionPath = "Assets/Content/Actions/Action Play Sound.asset";
    private const int LayoutVersion = 15;
    /// <summary>
    /// The Sounds/Language/Controls panels are drawn slightly larger than their
    /// layout rect. Set here because the layout sync resets local scale.
    /// </summary>
    private const float SectionBackgroundScale = 1.25f;

    [InitializeOnLoadMethod]
    private static void InstallAfterCompile()
    {
        EditorApplication.delayCall += TryInstall;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
            EditorApplication.delayCall += TryInstall;
    }

    private static void TryInstall()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (EditorPrefs.GetInt(VersionKey, 0) < LayoutVersion)
            Install();
        else
            SyncOpenSceneSettings();
    }

    [MenuItem("Tools/UI/Setup Settings Sound UI")]
    private static void InstallFromMenu() => Install();

    private static string VersionKey => "Meadom.SettingsSoundUI." + Application.dataPath.GetHashCode();

    private static void SyncOpenSceneSettings()
    {
        Sprite background = LoadFirstSprite(SettingsBackgroundPath);
        Sprite settingsTitle = LoadFirstSprite(SettingsTitlePath);
        Sprite soundsTitle = LoadFirstSprite(SoundsTitlePath);
        Sprite languageTitle = LoadFirstSprite(LanguageTitlePath);
        Sprite controlsTitle = LoadFirstSprite(ControlsTitlePath);
        Sprite settingsTitleVi = LoadFirstSprite(SettingsTitleViPath);
        Sprite soundsTitleVi = LoadFirstSprite(SoundsTitleViPath);
        Sprite languageTitleVi = LoadFirstSprite(LanguageTitleViPath);
        Sprite controlsTitleVi = LoadFirstSprite(ControlsTitleViPath);
        Sprite vietnameseButton = LoadNamedSprite(VietnameseButtonPath, "tieng-viet_0");
        Sprite englishButton = LoadNamedSprite(EnglishButtonPath, "english_0");

        if (background == null || settingsTitle == null || soundsTitle == null || languageTitle == null ||
            controlsTitle == null || settingsTitleVi == null || soundsTitleVi == null ||
            languageTitleVi == null || controlsTitleVi == null || vietnameseButton == null || englishButton == null)
            return;

        ConfigureOpenSceneHeaders(background, settingsTitle, soundsTitle, languageTitle, controlsTitle,
            settingsTitleVi, soundsTitleVi, languageTitleVi, controlsTitleVi, vietnameseButton, englishButton);
    }

    private static void Install()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform window = root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == "Window_Options");
            if (window == null)
            {
                Debug.LogError("Settings sound UI: Window_Options was not found.");
                return;
            }

            DisableOldControl(window, "Button_Fx");
            DisableOldControl(window, "Button_Music");
            DisableOldControl(window, "Slider_FxVolume");
            DisableOldControl(window, "Slider_MusicVolume");
            DisableOldControl(window, "Button Main Menu");

            Transform previous = window.Find("Sound Settings - Redesigned");
            if (previous != null)
                Object.DestroyImmediate(previous.gameObject);

            Sprite track = LoadFirstSprite(TrackPath);
            Sprite soundOn = LoadNamedSprite(StatePath, "icon-tăng-giảm-âm thanh_0");
            Sprite soundOff = LoadNamedSprite(StatePath, "icon-tăng-giảm-âm thanh_1");
            Sprite mainLabel = LoadFirstSprite(MainLabelPath);
            Sprite musicLabel = LoadFirstSprite(MusicLabelPath);
            Sprite sfxLabel = LoadFirstSprite(SfxLabelPath);
            Sprite settingsBackground = LoadFirstSprite(SettingsBackgroundPath);
            Sprite settingsTitle = LoadFirstSprite(SettingsTitlePath);
            Sprite soundsTitle = LoadFirstSprite(SoundsTitlePath);
            Sprite languageTitle = LoadFirstSprite(LanguageTitlePath);
            Sprite controlsTitle = LoadFirstSprite(ControlsTitlePath);
            Sprite settingsTitleVi = LoadFirstSprite(SettingsTitleViPath);
            Sprite soundsTitleVi = LoadFirstSprite(SoundsTitleViPath);
            Sprite languageTitleVi = LoadFirstSprite(LanguageTitleViPath);
            Sprite controlsTitleVi = LoadFirstSprite(ControlsTitleViPath);
            Sprite vietnameseButton = LoadNamedSprite(VietnameseButtonPath, "tieng-viet_0");
            Sprite englishButton = LoadNamedSprite(EnglishButtonPath, "english_0");

            if (track == null || soundOn == null || soundOff == null ||
                mainLabel == null || musicLabel == null || sfxLabel == null || settingsBackground == null ||
                settingsTitle == null || soundsTitle == null || languageTitle == null || controlsTitle == null ||
                settingsTitleVi == null || soundsTitleVi == null || languageTitleVi == null || controlsTitleVi == null ||
                vietnameseButton == null || englishButton == null)
            {
                Debug.LogError("Settings sound UI: one or more sprites could not be loaded.");
                return;
            }

            RectTransform panel = CreateRect(window, "Sound Settings - Redesigned");
            SetRect(panel, Vector2.one * 0.5f, new Vector2(0f, 0f), new Vector2(355f, 164f));
            panel.localScale = Vector3.one * 2f;
            panel.SetAsFirstSibling();

            RectTransform soundsSection = CreateRect(panel, "Sounds Section");
            SetStretch(soundsSection, Vector2.zero, Vector2.zero);
            RectTransform languageSection = CreateRect(panel, "Language Section");
            SetStretch(languageSection, Vector2.zero, Vector2.zero);
            RectTransform controlsSection = CreateRect(panel, "Controls Section");
            SetStretch(controlsSection, Vector2.zero, Vector2.zero);

            CreateSectionBackground(soundsSection, "Panel Sounds Background", settingsBackground,
                new Vector2(-116f, -12f), new Vector2(100f, 128f));
            CreateSectionBackground(languageSection, "Panel Language Background", settingsBackground,
                new Vector2(0f, -12f), new Vector2(100f, 128f));
            CreateSectionBackground(controlsSection, "Panel Controls Background", settingsBackground,
                new Vector2(116f, -12f), new Vector2(100f, 128f));

            Image settingsHeader = CreateHeader(panel, "Title Settings", settingsTitle, new Vector2(0f, 68f), new Vector2(90f, 16f));
            Image soundsHeader = CreateHeader(soundsSection, "Title Sounds", soundsTitle, new Vector2(-116f, 48f), new Vector2(90f, 16f));
            Image languageHeader = CreateHeader(languageSection, "Title Language", languageTitle, new Vector2(0f, 48f), new Vector2(90f, 16f));
            Image controlsHeader = CreateHeader(controlsSection, "Title Controls", controlsTitle, new Vector2(116f, 48f), new Vector2(90f, 16f));
            ConfigureLocalizedHeader(settingsHeader, settingsTitle, settingsTitleVi);
            ConfigureLocalizedHeader(soundsHeader, soundsTitle, soundsTitleVi);
            ConfigureLocalizedHeader(languageHeader, languageTitle, languageTitleVi);
            ConfigureLocalizedHeader(controlsHeader, controlsTitle, controlsTitleVi);

            Slider main = CreateVolumeControl(soundsSection, "Main", -142f, 1f, mainLabel, track, soundOn,
                out Image mainHandle);
            Slider music = CreateVolumeControl(soundsSection, "Music", -116f, 0.5f, musicLabel, track, soundOn,
                out Image musicHandle);
            Slider sfx = CreateVolumeControl(soundsSection, "SFX", -90f, 1f, sfxLabel, track, soundOn,
                out Image sfxHandle);

            CreateLanguageAndControls(languageSection, controlsSection, vietnameseButton, englishButton);

            SettingsSoundUI controller = panel.gameObject.AddComponent<SettingsSoundUI>();
            ActionPlaySound soundAction = AssetDatabase.LoadAssetAtPath<ActionPlaySound>(SoundActionPath);
            if (soundAction == null)
            {
                soundAction = AssetDatabase.FindAssets("t:ActionPlaySound")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<ActionPlaySound>)
                    .FirstOrDefault(asset => asset != null);
            }

            controller.Configure(main, music, sfx, soundOn, soundOff, soundAction,
                mainHandle, musicHandle, sfxHandle);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            CleanupLegacySceneBackgrounds();
            ConfigureOpenSceneHeaders(settingsBackground, settingsTitle, soundsTitle, languageTitle, controlsTitle,
                settingsTitleVi, soundsTitleVi, languageTitleVi, controlsTitleVi, vietnameseButton, englishButton);
            EditorPrefs.SetInt(VersionKey, LayoutVersion);
            Debug.Log("Settings sound UI installed in Pauze Menu.prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void CleanupLegacySceneBackgrounds()
    {
        bool changed = false;
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform candidate in allTransforms)
        {
            if (candidate == null || candidate.name != "Background Settings" ||
                candidate.parent == null || candidate.parent.name != "Sound Settings - Redesigned" ||
                !candidate.gameObject.scene.IsValid() || EditorUtility.IsPersistent(candidate.gameObject))
                continue;

            Object.DestroyImmediate(candidate.gameObject);
            changed = true;
        }

        if (changed)
            EditorSceneManager.SaveOpenScenes();
    }

    private static Slider CreateVolumeControl(RectTransform parent, string name, float x, float value,
        Sprite labelSprite, Sprite trackSprite, Sprite handleSprite, out Image handleImage)
    {
        RectTransform group = CreateRect(parent, "Volume " + name);
        SetRect(group, Vector2.one * 0.5f, new Vector2(x, -12f), new Vector2(25f, 108f));

        Image label = CreateImage(group, "Label " + name, labelSprite);
        SetRect(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -7f), new Vector2(19f, 12f));
        label.preserveAspect = true;

        RectTransform sliderRect = CreateRect(group, "Slider " + name);
        SetRect(sliderRect, Vector2.one * 0.5f, new Vector2(0f, -3f), new Vector2(18f, 78f));

        Image background = CreateImage(sliderRect, "Track", trackSprite);
        SetRect(background.rectTransform, Vector2.one * 0.5f, Vector2.zero, new Vector2(10f, 76f));
        background.preserveAspect = false;
        background.raycastTarget = true;

        RectTransform handleArea = CreateRect(sliderRect, "Handle Slide Area");
        SetStretch(handleArea, new Vector2(0f, 8f), new Vector2(0f, -8f));

        handleImage = CreateImage(handleArea, "Handle", handleSprite);
        Vector2 handleSize = handleSprite != null ? handleSprite.rect.size : new Vector2(15f, 16f);
        SetRect(handleImage.rectTransform, Vector2.one * 0.5f, Vector2.zero, handleSize);
        handleImage.preserveAspect = true;

        Slider slider = sliderRect.gameObject.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.BottomToTop;
        slider.targetGraphic = handleImage;
        slider.handleRect = handleImage.rectTransform;
        slider.value = value;

        return slider;
    }

    private static Image CreateHeader(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        Image header = CreateImage(parent, name, sprite);
        SetRect(header.rectTransform, Vector2.one * 0.5f, position, size);
        header.preserveAspect = true;
        header.raycastTarget = false;
        return header;
    }

    private static void ConfigureLocalizedHeader(Image image, Sprite english, Sprite vietnamese)
    {
        if (image == null)
            return;

        LocalizedSpriteButton localized = image.GetComponent<LocalizedSpriteButton>();
        if (localized == null)
            localized = image.gameObject.AddComponent<LocalizedSpriteButton>();
        localized.Configure(image, null, english, null, vietnamese, null, false);
        Vector2 englishSize = image.rectTransform.sizeDelta;
        localized.ConfigureSizes(englishSize, englishSize);
    }

    private static void ConfigureOpenSceneHeaders(Sprite background, Sprite settings, Sprite sounds,
        Sprite language, Sprite controls, Sprite settingsVi, Sprite soundsVi, Sprite languageVi,
        Sprite controlsVi, Sprite vietnameseButton, Sprite englishButton)
    {
        bool changed = false;
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform panel in allTransforms)
        {
            if (panel == null || panel.name != "Sound Settings - Redesigned" ||
                !panel.gameObject.scene.IsValid() || EditorUtility.IsPersistent(panel.gameObject))
                continue;

            if (panel is RectTransform panelRect)
                panelRect.localScale = Vector3.one * 2f;

            Transform soundsSection = EnsureSceneSection(panel, "Sounds Section", new[]
            {
                "Panel Sounds Background", "Title Sounds", "Volume Main", "Volume Music", "Volume SFX"
            });
            Transform languageSection = EnsureSceneSection(panel, "Language Section", new[]
            {
                "Panel Language Background", "Title Language", "Language Vietnamese", "Language English"
            });
            Transform controlsSection = EnsureSceneSection(panel, "Controls Section", new[]
            {
                "Panel Controls Background", "Title Controls", "Controls List"
            });

            EnsureSceneBackground(soundsSection, "Panel Sounds Background", background,
                new Vector2(-116f, -12f), new Vector2(100f, 128f));
            EnsureSceneBackground(languageSection, "Panel Language Background", background,
                new Vector2(0f, -12f), new Vector2(100f, 128f));
            EnsureSceneBackground(controlsSection, "Panel Controls Background", background,
                new Vector2(116f, -12f), new Vector2(100f, 128f));

            ConfigureSceneHeader(panel, "Title Settings", settings, settingsVi,
                new Vector2(0f, 68f), new Vector2(90f, 16f));
            ConfigureSceneHeader(soundsSection, "Title Sounds", sounds, soundsVi,
                new Vector2(-116f, 48f), new Vector2(90f, 16f));
            ConfigureSceneHeader(languageSection, "Title Language", language, languageVi,
                new Vector2(0f, 48f), new Vector2(90f, 16f));
            ConfigureSceneHeader(controlsSection, "Title Controls", controls, controlsVi,
                new Vector2(116f, 48f), new Vector2(90f, 16f));
            Transform backButton = panel.parent != null ? panel.parent.Find("Button Back To Pause") : null;
            if (backButton != null)
                backButton.gameObject.SetActive(true);
            EnsureSceneLanguageButton(languageSection, "Language Vietnamese", vietnameseButton, new Vector2(0f, 13f));
            EnsureSceneLanguageButton(languageSection, "Language English", englishButton, new Vector2(0f, -30f));

            Sprite soundOn = LoadNamedSprite(StatePath, "icon-tăng-giảm-âm thanh_0");
            ConfigureSceneHandle(soundsSection, "Main", soundOn);
            ConfigureSceneHandle(soundsSection, "Music", soundOn);
            ConfigureSceneHandle(soundsSection, "SFX", soundOn);
            DeleteSceneState(soundsSection, "Main");
            DeleteSceneState(soundsSection, "Music");
            DeleteSceneState(soundsSection, "SFX");
            changed = true;
        }

        if (changed)
            EditorSceneManager.SaveOpenScenes();
    }

    private static void ConfigureSceneHeader(Transform panel, string name, Sprite english, Sprite vietnamese,
        Vector2 position, Vector2 size)
    {
        Transform target = panel.Find(name);
        if (target != null)
        {
            SetRect((RectTransform)target, Vector2.one * 0.5f, position, size);
            ConfigureLocalizedHeader(target.GetComponent<Image>(), english, vietnamese);
        }
    }

    private static void EnsureSceneLanguageButton(Transform parent, string name, Sprite sprite, Vector2 position)
    {
        Transform target = parent.Find(name);
        if (target == null)
            target = CreateRect(parent, name);

        TMP_Text oldText = target.GetComponent<TMP_Text>();
        if (oldText != null)
            Object.DestroyImmediate(oldText);

        Image image = target.GetComponent<Image>();
        if (image == null)
            image = target.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = true;
        SetRect((RectTransform)target, Vector2.one * 0.5f, position, new Vector2(76f, 38f));

        Button button = target.GetComponent<Button>();
        if (button == null)
            button = target.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
    }

    private static Transform EnsureSceneSection(Transform panel, string sectionName, string[] childNames)
    {
        Transform section = panel.Find(sectionName);
        if (section == null)
        {
            section = CreateRect(panel, sectionName);
        }
        SetStretch((RectTransform)section, Vector2.zero, Vector2.zero);

        foreach (string objectName in childNames)
        {
            Transform target = panel.Find(objectName);
            if (target != null)
                target.SetParent(section, false);
        }

        Transform background = section.Cast<Transform>()
            .FirstOrDefault(child => child.name.StartsWith("Panel ", System.StringComparison.Ordinal));
        if (background != null)
            background.SetAsFirstSibling();
        return section;
    }

    private static void EnsureSceneBackground(Transform panel, string name, Sprite sprite,
        Vector2 position, Vector2 size)
    {
        Transform existing = panel.Find(name);
        Image background = existing != null ? existing.GetComponent<Image>() : null;
        if (background == null)
            background = CreateImage(panel, name, sprite);

        background.sprite = sprite;
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.raycastTarget = false;
        SetRect(background.rectTransform, Vector2.one * 0.5f, position, size);
        background.rectTransform.localScale = Vector3.one * SectionBackgroundScale;
        background.transform.SetAsFirstSibling();
    }

    private static void ConfigureSceneHandle(Transform panel, string name, Sprite sprite)
    {
        Transform handle = panel.Find($"Volume {name}/Slider {name}/Handle Slide Area/Handle");
        Image image = handle != null ? handle.GetComponent<Image>() : null;
        if (image == null || sprite == null)
            return;

        image.sprite = sprite;
        image.preserveAspect = true;
        image.rectTransform.sizeDelta = sprite.rect.size;
    }

    private static void DeleteSceneState(Transform panel, string name)
    {
        Transform state = panel.Find($"Volume {name}/State {name}");
        if (state != null)
            Object.DestroyImmediate(state.gameObject);
    }

    private static void CreateSectionBackground(Transform parent, string name, Sprite sprite,
        Vector2 position, Vector2 size)
    {
        Image background = CreateImage(parent, name, sprite);
        SetRect(background.rectTransform, Vector2.one * 0.5f, position, size);
        background.rectTransform.localScale = Vector3.one * SectionBackgroundScale;
        background.type = Image.Type.Sliced;
        background.preserveAspect = false;
        background.raycastTarget = false;
        background.transform.SetAsFirstSibling();
    }

    private static void CreateLanguageAndControls(RectTransform languageSection, RectTransform panel,
        Sprite vietnameseButton, Sprite englishButton)
    {
        CreateLanguageButton(languageSection, "Language Vietnamese", vietnameseButton, new Vector2(0f, 13f));
        CreateLanguageButton(languageSection, "Language English", englishButton, new Vector2(0f, -30f));

        const string controls = "W/A/S/D   MOVEMENT\n1/2/3/4/5   SLOTS\nB   BAG\nESC   PAUSE / BACK\nO   INTERACT\nLEFT MOUSE   USE TOOL\nRIGHT MOUSE   INTERACT / CANCEL SOIL";
        TMP_Text controlText = CreateText(panel, "Controls List", controls, new Vector2(116f, -13f),
            new Vector2(96f, 108f), 5.9f);
        controlText.alignment = TextAlignmentOptions.MidlineLeft;
        controlText.lineSpacing = 3f;
    }

    private static void CreateLanguageButton(Transform parent, string name, Sprite sprite, Vector2 position)
    {
        Image image = CreateImage(parent, name, sprite);
        SetRect(image.rectTransform, Vector2.one * 0.5f, position, new Vector2(76f, 38f));
        image.preserveAspect = true;
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, Vector2 position,
        Vector2 size, float fontSize)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        target.layer = 5;
        target.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)target.transform;
        SetRect(rect, Vector2.one * 0.5f, position, size);

        TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.color = new Color32(40, 27, 18, 255);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void DisableOldControl(Transform parent, string name)
    {
        Transform target = parent.Find(name);
        if (target != null)
            target.gameObject.SetActive(false);
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject target = new GameObject(name, typeof(RectTransform));
        target.layer = 5;
        target.transform.SetParent(parent, false);
        return (RectTransform)target.transform;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        target.layer = 5;
        target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        return image;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private static void SetStretch(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minOffset;
        rect.offsetMax = maxOffset;
        rect.localScale = Vector3.one;
    }

    private static Sprite LoadFirstSprite(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

    private static Sprite LoadNamedSprite(string path, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault(sprite => sprite.name == name);
}
#endif
