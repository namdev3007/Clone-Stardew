#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using User_Interface;

public static class AlignOptionsWindowUI
{
    private const string PrefabPath = "Assets/Prefabs/User Interface/Core/Pauze Menu.prefab";
    private const string ScenePath = "Assets/MainScenes/Core 1.unity";
    private const string BackPath = "Assets/Sprites/Buttons/other-button/back-lùi về.png";
    private const string OkPath = "Assets/Sprites/Buttons/tieng-anh/ok.png";
    private const string ConfirmPath = "Assets/Sprites/Buttons/tieng-viet/xac-nhan.png";
    private const string TitlesPath = "Assets/Sprites/settings-loadgame-ui/text-dùng cho ui-settings/";
    private const int Version = 3;

    private static string VersionKey => "Meadom.AlignOptionsWindow." + Application.dataPath.GetHashCode();

    // [InitializeOnLoadMethod] - Disabled automatic background alignment so manual edits are preserved
    private static void QueueInstall()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode && EditorPrefs.GetInt(VersionKey, 0) < Version)
                Install();
        };
    }

    [MenuItem("Tools/UI/Align Options Window")]
    private static void InstallFromMenu() => Install();

    private static void Install()
    {
        GameObject prefab = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Align(prefab);
            PrefabUtility.SaveAsPrefabAsset(prefab, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefab);
        }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                Align(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        AssetDatabase.SaveAssets();
        EditorPrefs.SetInt(VersionKey, Version);
        Debug.Log("Window_Options: Back, OK and localized title positions aligned in prefab and Core 1 scene.");
    }

    private static void Align(GameObject root)
    {
        Transform window = root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(child => child.name == "Window_Options");
        PauseGameMenuController pause = root.GetComponentInChildren<PauseGameMenuController>(true);
        if (window == null || pause == null)
            return;

        Transform panel = window.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(child => child.name == "Sound Settings - Redesigned");
        if (panel == null)
            return;

        EnsureBack(window, pause);
        EnsureOk(window, pause);
        AlignSection(panel, "Sounds Section");
        AlignSection(panel, "Language Section");
        AlignSection(panel, "Controls Section");
        AlignTitle(panel, "Title Settings", "text-settings.png", "text-cài đặt.png",
            new Vector2(0f, 68f), new Vector2(0f, 70f),
            new Vector2(137f, 29f), new Vector2(128f, 48f));
        AlignTitle(panel, "Title Sounds", "text-sounds.png", "text-âm thanh.png",
            new Vector2(-116f, 48f), new Vector2(-116f, 46f),
            new Vector2(76f, 16f), new Vector2(105f, 27f));
        AlignTitle(panel, "Title Language", "text-language.png", "text-ngôn ngữ.png",
            new Vector2(0f, 48f), new Vector2(0f, 48f),
            new Vector2(103f, 16f), new Vector2(97f, 21f));
        AlignTitle(panel, "Title Controls", "text-controls.png", "text-điều khiển.png",
            new Vector2(116f, 48f), new Vector2(116f, 48f),
            new Vector2(103f, 16f), new Vector2(118f, 26f));
    }

    private static void AlignSection(Transform panel, string name)
    {
        RectTransform section = panel.Find(name) as RectTransform;
        if (section == null)
            return;

        section.anchorMin = Vector2.zero;
        section.anchorMax = Vector2.one;
        section.pivot = Vector2.one * 0.5f;
        section.anchoredPosition = Vector2.zero;
        section.sizeDelta = Vector2.zero;
        section.localScale = Vector3.one;
        section.localRotation = Quaternion.identity;
    }

    private static void EnsureBack(Transform window, PauseGameMenuController pause)
    {
        Transform found = window.Find("Button Back To Pause");
        if (found == null)
        {
            GameObject created = new GameObject("Button Back To Pause", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            created.layer = LayerMask.NameToLayer("UI");
            created.transform.SetParent(window, false);
            found = created.transform;
        }

        Image image = found.GetComponent<Image>() ?? found.gameObject.AddComponent<Image>();
        image.sprite = SpriteAt(BackPath);
        image.preserveAspect = true;
        image.raycastTarget = true;
        Button button = found.GetComponent<Button>() ?? found.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        SetPosition((RectTransform)found, new Vector2(-320f, 143f), new Vector2(54f, 32f));
        button.onClick.RemoveAllListeners();
        ClearPersistent(button);
        UnityEventTools.AddPersistentListener(button.onClick, pause.ShowPauseMenu);
        found.gameObject.SetActive(true);
        found.SetAsLastSibling();
    }

    private static void EnsureOk(Transform window, PauseGameMenuController pause)
    {
        Transform found = window.Find("Button OK");
        if (found == null)
        {
            GameObject created = new GameObject("Button OK", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LocalizedSpriteButton));
            created.layer = LayerMask.NameToLayer("UI");
            created.transform.SetParent(window, false);
            found = created.transform;
        }

        Image image = found.GetComponent<Image>() ?? found.gameObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = true;
        Button button = found.GetComponent<Button>() ?? found.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        SetPosition((RectTransform)found, new Vector2(315f, -143f), new Vector2(54f, 32f));
        LocalizedSpriteButton localized = found.GetComponent<LocalizedSpriteButton>() ??
                                          found.gameObject.AddComponent<LocalizedSpriteButton>();
        localized.Configure(image, button, SpriteAt(OkPath, "ok_0"), SpriteAt(OkPath, "ok_1"),
            SpriteAt(ConfirmPath, "xac-nhan_0"), SpriteAt(ConfirmPath, "xac-nhan_1"), false);
        localized.ConfigureSizes(new Vector2(54f, 32f), new Vector2(54f, 32f));
        button.onClick.RemoveAllListeners();
        ClearPersistent(button);
        UnityEventTools.AddPersistentListener(button.onClick, pause.ApplySettingsAndReturn);
        found.gameObject.SetActive(true);
        found.SetAsLastSibling();
    }

    private static void AlignTitle(Transform panel, string name, string englishFile, string vietnameseFile,
        Vector2 englishPos, Vector2 vietnamesePos, Vector2 englishSize, Vector2 vietnameseSize)
    {
        Transform found = panel.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == name);
        if (found == null)
            return;

        Sprite english = SpriteAt(TitlesPath + englishFile);
        Sprite vietnamese = SpriteAt(TitlesPath + vietnameseFile);
        Image image = found.GetComponent<Image>();
        if (image == null || english == null || vietnamese == null)
            return;

        SetPosition((RectTransform)found, englishPos, englishSize);
        image.preserveAspect = true;
        image.raycastTarget = false;
        LocalizedSpriteButton localized = found.GetComponent<LocalizedSpriteButton>() ??
                                          found.gameObject.AddComponent<LocalizedSpriteButton>();
        localized.Configure(image, null, english, null, vietnamese, null, false);
        localized.ConfigureSizes(englishSize, vietnameseSize);
        localized.ConfigurePositions(englishPos, vietnamesePos);
    }

    private static Vector2 SizeFor(Sprite sprite, float height, float maxWidth)
    {
        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        return new Vector2(Mathf.Min(height * aspect, maxWidth), height);
    }

    private static void SetPosition(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = Vector2.one * 0.5f;
        rect.anchorMax = Vector2.one * 0.5f;
        rect.pivot = Vector2.one * 0.5f;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static Sprite SpriteAt(string path, string name = null)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        return sprites.FirstOrDefault(sprite => sprite.name == name) ?? sprites.FirstOrDefault();
    }

    private static void ClearPersistent(Button button)
    {
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEventTools.RemovePersistentListener(button.onClick, i);
    }
}
#endif
