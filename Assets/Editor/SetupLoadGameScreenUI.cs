#if UNITY_EDITOR
using System.Linq;
using Main_Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SetupLoadGameScreenUI
{
    private const string ScenePath = "Assets/MainScenes/StartMenu 1.unity";
    private const string PanelPath = "Assets/Sprites/settings-loadgame-ui/bản-load-game.png";
    private const string AddButtonPath = "Assets/Sprites/settings-loadgame-ui/new-game-plus-1.png";
    private const string AddIconPath = "Assets/Sprites/settings-loadgame-ui/new-game-plus-2.png";
    private const int LayoutVersion = 1;

    [InitializeOnLoadMethod]
    private static void InstallAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                EditorPrefs.GetInt(VersionKey, 0) < LayoutVersion)
                Install();
        };
    }

    [MenuItem("Tools/UI/Setup Load Game Screen")]
    private static void InstallFromMenu() => Install();

    private static string VersionKey => "Meadom.LoadGameScreenUI." + Application.dataPath.GetHashCode();

    private static void Install()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedByTool = !scene.IsValid() || !scene.isLoaded;
        if (openedByTool)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject screen = Find(scene, "Screen _ SaveSlots");
            if (screen == null)
            {
                Debug.LogError("Load-game UI setup: Screen _ SaveSlots was not found.");
                return;
            }

            Sprite panelSprite = LoadSprite(PanelPath);
            Sprite buttonSprite = LoadSprite(AddButtonPath);
            Sprite iconSprite = LoadSprite(AddIconPath);
            if (panelSprite == null || buttonSprite == null || iconSprite == null)
            {
                Debug.LogError("Load-game UI setup: one or more replacement sprites could not be loaded.");
                return;
            }

            Image oldScreenImage = screen.GetComponent<Image>();
            if (oldScreenImage != null)
                oldScreenImage.enabled = false;

            RectTransform panel = CreateImage(screen.transform, "Load Game Panel", panelSprite);
            SetRect(panel, Vector2.one * 0.5f, new Vector2(0f, 65f), new Vector2(810f, 268.5f));
            panel.SetAsFirstSibling();

            Transform slotContainer = screen.transform.Find("Save Slot Displayers");
            if (slotContainer != null)
            {
                slotContainer.SetParent(panel, false);
                RectTransform slotsRect = (RectTransform)slotContainer;
                SetRect(slotsRect, Vector2.one * 0.5f, new Vector2(-28f, 22f), new Vector2(670f, 82f));

                VerticalLayoutGroup vertical = slotContainer.GetComponent<VerticalLayoutGroup>();
                if (vertical != null)
                    Object.DestroyImmediate(vertical);
                HorizontalLayoutGroup horizontal = slotContainer.GetComponent<HorizontalLayoutGroup>();
                if (horizontal == null)
                    horizontal = slotContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                horizontal.spacing = 8f;
                horizontal.childAlignment = TextAnchor.MiddleCenter;
                horizontal.childControlWidth = false;
                horizontal.childControlHeight = false;
                horizontal.childForceExpandWidth = false;
                horizontal.childForceExpandHeight = false;

                foreach (RectTransform slot in slotContainer.Cast<Transform>().OfType<RectTransform>())
                {
                    slot.localScale = Vector3.one;
                    slot.sizeDelta = new Vector2(160f, 72f);
                }
            }

            RectTransform addRect = CreateImage(screen.transform, "Button_Add_Save", buttonSprite);
            SetRect(addRect, Vector2.one * 0.5f, new Vector2(0f, -137f), new Vector2(816f, 144f));
            Button addButton = addRect.GetComponent<Button>();
            if (addButton == null)
                addButton = addRect.gameObject.AddComponent<Button>();
            addButton.targetGraphic = addRect.GetComponent<Image>();

            RectTransform icon = CreateImage(addRect, "Icon_Add", iconSprite);
            SetRect(icon, Vector2.one * 0.5f, Vector2.zero, new Vector2(95f, 96f));
            icon.GetComponent<Image>().raycastTarget = false;

            Button sourceNewGame = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true))
                .FirstOrDefault(button => button.name == "Button New Game" &&
                                          !button.transform.IsChildOf(screen.transform) &&
                                          button.interactable);

            LoadGameScreenUI bridge = screen.GetComponent<LoadGameScreenUI>();
            if (bridge == null)
                bridge = screen.AddComponent<LoadGameScreenUI>();
            bridge.Configure(addButton, sourceNewGame);

            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(bridge);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            EditorPrefs.SetInt(VersionKey, LayoutVersion);
            Debug.Log("Load-game screen UI installed in StartMenu 1.");
        }
        finally
        {
            if (openedByTool && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static RectTransform CreateImage(Transform parent, string name, Sprite sprite)
    {
        Transform existing = parent.Find(name);
        GameObject target = existing != null
            ? existing.gameObject
            : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null)
            target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = false;
        return target.GetComponent<RectTransform>();
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

    private static Sprite LoadSprite(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();

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
}
#endif
