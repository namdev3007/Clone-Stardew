#if UNITY_EDITOR
using System.Linq;
using Action.Actions;
using Main_Menu;
using Referencing.Scriptable_Variables.Variables;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using User_Interface;

public static class SetupNewGameNameUI
{
    private const string ScenePath = "Assets/MainScenes/StartMenu 1.unity";
    private const string ScreenName = "Screen _ New Game Name";
    private const string ConfirmSpritePath = "Assets/Sprites/Buttons/tieng-viet/xac-nhan.png";
    private const string ConfirmEnglishSpritePath = "Assets/Sprites/Buttons/tieng-anh/ok.png";
    private const string BackSpritePath = "Assets/Sprites/Buttons/other-button/back-lùi về.png";
    private const string BackgroundSpritePath = "Assets/Sprites/Background/screen-home.png";
    private const string FontPath = "Assets/fonts/Dùng cho text khác/binhthuong nhat'.asset";
    private const string PlayerNamePath = "Assets/ScriptableObjects/Variables/New Game Configuration/Character Name.asset";
    private const string WorldNamePath = "Assets/ScriptableObjects/Variables/New Game Configuration/Farm Name.asset";
    private const string NewGameActionPath = "Assets/ScriptableObjects/Actions/Action New Game.asset";
    private const string SaveSlotPrefabPath = "Assets/Prefabs/User Interface/Start Menu/UI Save Slot Displayer.prefab";
    private const int LayoutVersion = 3;

    private static string VersionKey => "Meadom.NewGameNameUI." + Application.dataPath.GetHashCode();

    [InitializeOnLoadMethod]
    private static void InstallAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorPrefs.GetInt(VersionKey, 0) < LayoutVersion)
                Install(false);
        };
    }

    [MenuItem("Tools/UI/Setup New Game Name Screen")]
    private static void InstallFromMenu() => Install(true);

    private static void Install(bool selectScreen)
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedByTool = !scene.IsValid() || !scene.isLoaded;
        if (openedByTool)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject mainScreen = Find(scene, "Screen _ Main");
            if (mainScreen == null)
            {
                Debug.LogError("New Game UI setup: Screen _ Main was not found.");
                return;
            }

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Sprite[] confirmSprites = AssetDatabase.LoadAllAssetsAtPath(ConfirmSpritePath)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name)
                .ToArray();
            Sprite[] confirmEnglishSprites = AssetDatabase.LoadAllAssetsAtPath(ConfirmEnglishSpritePath)
                .OfType<Sprite>()
                .OrderBy(sprite => sprite.name)
                .ToArray();
            Sprite backSprite = AssetDatabase.LoadAllAssetsAtPath(BackSpritePath).OfType<Sprite>().FirstOrDefault();
            Sprite backgroundSprite = AssetDatabase.LoadAllAssetsAtPath(BackgroundSpritePath).OfType<Sprite>().FirstOrDefault();
            StringVariable playerName = AssetDatabase.LoadAssetAtPath<StringVariable>(PlayerNamePath);
            StringVariable worldName = AssetDatabase.LoadAssetAtPath<StringVariable>(WorldNamePath);
            ActionNewGame newGameAction = AssetDatabase.LoadAssetAtPath<ActionNewGame>(NewGameActionPath);
            if (font == null || confirmSprites.Length < 2 || confirmEnglishSprites.Length < 2 ||
                backSprite == null || backgroundSprite == null || playerName == null || worldName == null ||
                newGameAction == null)
            {
                Debug.LogError("New Game UI setup: a font, confirmation sprite, name variable or New Game action is missing.");
                return;
            }

            GameObject oldScreen = Find(scene, ScreenName);
            if (oldScreen != null)
                Object.DestroyImmediate(oldScreen);

            GameObject screen = new GameObject(ScreenName,
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(CanvasRenderer), typeof(Image), typeof(NewGameNameScreen));
            SceneManager.MoveGameObjectToScene(screen, scene);
            RectTransform screenRect = screen.GetComponent<RectTransform>();
            Stretch(screenRect);

            Canvas canvas = screen.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = screen.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            Image background = screen.GetComponent<Image>();
            background.sprite = backgroundSprite;
            background.color = Color.white;
            background.preserveAspect = false;
            background.raycastTarget = true;

            TextMeshProUGUI title = CreateText(screen.transform, "Title", font,
                "Welcome to Meadow!", 58f, TextAlignmentOptions.Center);
            SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 355f), new Vector2(1000f, 90f));

            TextMeshProUGUI prompt = CreateText(screen.transform, "Prompt", font,
                "What's your name?", 38f, TextAlignmentOptions.Center);
            SetRect(prompt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 265f), new Vector2(800f, 65f));

            TMP_InputField input = CreateInputField(screen.transform, font);
            SetRect(input.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(-170f, 135f), new Vector2(900f, 105f));

            Button confirm = CreateConfirmButton(screen.transform,
                confirmEnglishSprites[0], confirmEnglishSprites[1], confirmSprites[0], confirmSprites[1]);
            SetRect(confirm.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f),
                new Vector2(465f, 135f), new Vector2(339f, 99f));

            Button back = CreateBackButton(screen.transform, backSprite);
            SetRect(back.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                new Vector2(82f, -52f), new Vector2(132f, 66f));

            NewGameNameScreen controller = screen.GetComponent<NewGameNameScreen>();
            controller.Configure(input, confirm, back, title, prompt,
                input.placeholder as TMP_Text, mainScreen, playerName, worldName, newGameAction);

            Button mainNewGameButton = mainScreen.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "Button New Game");
            if (mainNewGameButton == null)
            {
                Debug.LogError("New Game UI setup: the main New Game button was not found.");
                Object.DestroyImmediate(screen);
                return;
            }

            for (int i = mainNewGameButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(mainNewGameButton.onClick, i);
            UnityEventTools.AddPersistentListener(mainNewGameButton.onClick, controller.Show);
            EditorUtility.SetDirty(mainNewGameButton);

            ConfigureSceneSaveSlots(scene, font);
            ConfigureSaveSlotPrefab(font);

            screen.SetActive(false);
            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            EditorPrefs.SetInt(VersionKey, LayoutVersion);

            if (selectScreen)
                Selection.activeGameObject = screen;
            Debug.Log("New Game name screen and world creation-date save slots are ready.");
        }
        finally
        {
            if (openedByTool && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static TMP_InputField CreateInputField(Transform parent, TMP_FontAsset font)
    {
        GameObject root = new GameObject("World Name Input",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        root.transform.SetParent(parent, false);
        Image background = root.GetComponent<Image>();
        background.color = new Color32(224, 224, 224, 255);

        GameObject textAreaObject = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textAreaObject.transform.SetParent(root.transform, false);
        RectTransform textArea = textAreaObject.GetComponent<RectTransform>();
        Stretch(textArea, new Vector2(34f, 12f), new Vector2(-34f, -12f));

        TextMeshProUGUI placeholder = CreateText(textArea, "Placeholder", font,
            "Enter Name", 42f, TextAlignmentOptions.MidlineLeft);
        placeholder.color = new Color32(128, 128, 128, 255);
        placeholder.fontStyle = FontStyles.Normal;
        Stretch(placeholder.rectTransform);

        TextMeshProUGUI text = CreateText(textArea, "Text", font,
            string.Empty, 42f, TextAlignmentOptions.MidlineLeft);
        text.color = new Color32(30, 30, 30, 255);
        Stretch(text.rectTransform);

        TMP_InputField input = root.GetComponent<TMP_InputField>();
        input.textViewport = textArea;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.characterLimit = 24;
        input.targetGraphic = background;
        input.caretColor = Color.black;
        input.selectionColor = new Color(0.42f, 0.65f, 0.9f, 0.65f);
        return input;
    }

    private static Button CreateConfirmButton(Transform parent, Sprite englishNormal, Sprite englishPressed,
        Sprite vietnameseNormal, Sprite vietnamesePressed)
    {
        GameObject root = new GameObject("Button Confirm",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.sprite = englishNormal;
        image.preserveAspect = true;
        image.color = Color.white;

        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        SpriteState state = button.spriteState;
        state.pressedSprite = englishPressed;
        state.selectedSprite = englishNormal;
        state.highlightedSprite = englishNormal;
        state.disabledSprite = englishNormal;
        button.spriteState = state;
        root.AddComponent<LocalizedSpriteButton>().Configure(image, button,
            englishNormal, englishPressed, vietnameseNormal, vietnamesePressed, false);
        return button;
    }

    private static Button CreateBackButton(Transform parent, Sprite sprite)
    {
        GameObject root = new GameObject("Button Back",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.color = Color.white;
        Button button = root.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, TMP_FontAsset font,
        string value, float size, TextAlignmentOptions alignment)
    {
        GameObject target = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        target.transform.SetParent(parent, false);
        TextMeshProUGUI text = target.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Normal;
        text.alignment = alignment;
        text.color = new Color32(28, 28, 28, 255);
        text.text = value;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void ConfigureSceneSaveSlots(Scene scene, TMP_FontAsset font)
    {
        foreach (UISaveSlotDisplayer displayer in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<UISaveSlotDisplayer>(true)))
            ConfigureSaveSlot(displayer, font);
    }

    private static void ConfigureSaveSlotPrefab(TMP_FontAsset font)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(SaveSlotPrefabPath);
        try
        {
            UISaveSlotDisplayer displayer = root.GetComponent<UISaveSlotDisplayer>();
            if (displayer != null)
                ConfigureSaveSlot(displayer, font);
            PrefabUtility.SaveAsPrefabAsset(root, SaveSlotPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ConfigureSaveSlot(UISaveSlotDisplayer displayer, TMP_FontAsset font)
    {
        SerializedObject serialized = new SerializedObject(displayer);
        SerializedProperty playerProperty = serialized.FindProperty("playerNameText");
        SerializedProperty worldProperty = serialized.FindProperty("farmNameText");
        SerializedProperty dateProperty = serialized.FindProperty("dateText");

        TextMeshProUGUI player = playerProperty.objectReferenceValue as TextMeshProUGUI;
        TextMeshProUGUI world = FindText(displayer.transform, "Text_idWorldName") ??
                                  FindText(displayer.transform, "Text_WorldName") ??
                                  worldProperty.objectReferenceValue as TextMeshProUGUI ?? player;
        TextMeshProUGUI date = FindText(displayer.transform, "Text_CreationDate") ??
                               FindText(displayer.transform, "Text_date") ??
                               dateProperty.objectReferenceValue as TextMeshProUGUI;

        if (world != null)
        {
            world.name = "Text_idWorldName";
            world.font = font;
            world.text = "World Name";
            world.gameObject.SetActive(true);
            worldProperty.objectReferenceValue = world;
        }
        if (date != null)
        {
            date.name = "Text_CreationDate";
            date.font = font;
            date.text = "17/09/2026";
            date.gameObject.SetActive(true);
            dateProperty.objectReferenceValue = date;
        }
        if (player != null && player != world)
            player.gameObject.SetActive(false);

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(displayer);
    }

    private static TextMeshProUGUI FindText(Transform parent, string name) =>
        parent.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(text => text.name == name);

    private static GameObject Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
            if (match != null)
                return match.gameObject;
        }
        return null;
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

    private static void Stretch(RectTransform rect) => Stretch(rect, Vector2.zero, Vector2.zero);

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = Vector2.one * 0.5f;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}
#endif
