using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FarmingKitEditor
{
    [InitializeOnLoad]
    public static class AutoLoadCoreScene
    {
        // Specify the scene to load additively
        private static readonly string BaseCoreScenePath = $"Scenes/{CORE_SCENE_NAME}.unity";
        private const string CORE_SCENE_NAME = "Core"; // Name of the scene that will be auto-loaded
        private const string APPLY_TO_FOLDER = "Levels"; // Only auto-load the core scene in scenes in this folder
    
        private static string coreScenePath;

        // Static constructor to register the event handler
        static AutoLoadCoreScene()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }
    
        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (Application.isPlaying || BuildPipeline.isBuildingPlayer)
                return;
        
            if (!scene.path.Contains(APPLY_TO_FOLDER))
                return;
        
            string sceneToLoad = GetSceneToLoad();
        
            // Check if the scene is being opened in Single mode
            if (mode == OpenSceneMode.Single)
            {
                // Avoid loading the same scene additively
                if (!SceneAlreadyLoaded(sceneToLoad))
                {
                    var coreScene = EditorSceneManager.OpenScene(sceneToLoad, OpenSceneMode.Additive);
                    SceneManager.SetActiveScene(coreScene);
                    EditorSceneManager.MoveSceneBefore(coreScene, scene);
                }
            }
        }
    
        static string GetSceneToLoad()
        {
            if (DoesSceneExist(BaseCoreScenePath))
                return BaseCoreScenePath;

            if (EditorPrefs.HasKey("CoreScenePath"))
            {
                coreScenePath = EditorPrefs.GetString("CoreScenePath");
                if (DoesSceneExist(coreScenePath))
                {
                    return coreScenePath;
                }
            }
            string[] guids = AssetDatabase.FindAssets($"{CORE_SCENE_NAME} t:Scene");
            if (guids.Length > 0)
            {
                coreScenePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                EditorPrefs.SetString("CoreScenePath", coreScenePath);
                return coreScenePath;
            }

            Debug.LogError("Core scene not found in project. Have you removed it?" +
                "If you have renamed it, please rename the CORE_SCENE_NAME field to the correct name in the AutoLoadCoreScene script.");
            return string.Empty;
        }

        static bool DoesSceneExist(string path) => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;

        private static bool SceneAlreadyLoaded(string scenePath)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (loadedScene.path == scenePath)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
