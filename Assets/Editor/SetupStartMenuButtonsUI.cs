#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using User_Interface;

public static class SetupStartMenuButtonsUI
{
    private const string ScenePath = "Assets/MainScenes/StartMenu 1.unity";

    private const string EnNewGamePath = "Assets/Sprites/Buttons/tieng-anh/new-game.png";
    private const string ViNewGamePath = "Assets/Sprites/Buttons/tieng-viet/choi-moi.png";

    private const string EnLoadGamePath = "Assets/Sprites/Buttons/tieng-anh/load-game.png";
    private const string ViLoadGamePath = "Assets/Sprites/Buttons/tieng-viet/choi-tiep.png";

    private const string EnSettingsPath = "Assets/Sprites/Buttons/tieng-anh/settings.png";
    private const string ViSettingsPath = "Assets/Sprites/Buttons/tieng-viet/cai-dat.png";

    private const string EnExitPath = "Assets/Sprites/Buttons/tieng-anh/exit.png";
    private const string ViExitPath = "Assets/Sprites/Buttons/tieng-viet/thoat.png";

    [MenuItem("Tools/UI/Setup Start Menu Buttons UI")]
    public static void InstallFromMenu() => Install();

    public static void Install()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedByTool = !scene.IsValid() || !scene.isLoaded;
        if (openedByTool)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject mainScreen = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "Screen _ Main")?.gameObject;

            if (mainScreen == null)
            {
                Debug.LogError("Start Menu UI setup: Screen _ Main was not found.");
                return;
            }

            Sprite enNewGame = LoadSprite(EnNewGamePath);
            Sprite viNewGame = LoadSprite(ViNewGamePath);
            Sprite enLoadGame = LoadSprite(EnLoadGamePath);
            Sprite viLoadGame = LoadSprite(ViLoadGamePath);
            Sprite enSettings = LoadSprite(EnSettingsPath);
            Sprite viSettings = LoadSprite(ViSettingsPath);
            Sprite enExit = LoadSprite(EnExitPath);
            Sprite viExit = LoadSprite(ViExitPath);

            // 1. Button New Game
            Transform newGameTransform = mainScreen.transform.Find("Button New Game");
            if (newGameTransform != null)
            {
                Image image = newGameTransform.Find("Image_New Game")?.GetComponent<Image>()
                              ?? newGameTransform.GetComponent<Image>();
                Button button = newGameTransform.GetComponent<Button>();
                ConfigureButton(newGameTransform.gameObject, image, button, enNewGame, viNewGame);
            }

            // 2. Button Continue (Load Game)
            Transform continueTransform = mainScreen.transform.Find("Button Continue");
            if (continueTransform != null)
            {
                Image image = continueTransform.Find("Image_Continue")?.GetComponent<Image>()
                              ?? continueTransform.GetComponent<Image>();
                Button button = continueTransform.GetComponent<Button>();
                ConfigureButton(continueTransform.gameObject, image, button, enLoadGame, viLoadGame);
            }

            // 3. Button Settings
            Transform settingsTransform = mainScreen.transform.Find("Button Settings");
            if (settingsTransform != null)
            {
                Image image = settingsTransform.GetComponent<Image>();
                Button button = settingsTransform.GetComponent<Button>();
                ConfigureButton(settingsTransform.gameObject, image, button, enSettings, viSettings);
            }

            // 4. Button Exit
            Transform exitTransform = mainScreen.transform.Find("Button Exit");
            if (exitTransform != null)
            {
                Image image = exitTransform.GetComponent<Image>();
                Button button = exitTransform.GetComponent<Button>();
                ConfigureButton(exitTransform.gameObject, image, button, enExit, viExit);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Successfully installed Start Menu button localization for Screen _ Main.");
        }
        finally
        {
            if (openedByTool && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ConfigureButton(GameObject host, Image image, Button button, Sprite enNormal, Sprite viNormal)
    {
        if (host == null || image == null)
            return;

        LocalizedSpriteButton localizer = host.GetComponent<LocalizedSpriteButton>();
        if (localizer == null)
            localizer = host.AddComponent<LocalizedSpriteButton>();

        localizer.Configure(image, button, enNormal, null, viNormal, null, false);
        localizer.RefreshLanguage();
        EditorUtility.SetDirty(localizer);
        EditorUtility.SetDirty(image);
        if (button != null)
            EditorUtility.SetDirty(button);
    }

    private static Sprite LoadSprite(string assetPath)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath)
               ?? AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().FirstOrDefault();
    }
}
#endif
