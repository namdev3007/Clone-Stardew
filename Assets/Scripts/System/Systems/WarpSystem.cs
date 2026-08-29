using Data;
using Event.Events;
using Plugins.Lowscope.ComponentSaveSystem;
using Referencing.Scriptable_Reference;
using Referencing.Scriptable_Variables.References;
using UnityEngine;
using UnityEngine.SceneManagement;
using World;
using System;

namespace GameSystem.Systems
{
    /// <summary>
    /// Reacts to to RequestWarp events. When this happens it will load the target scene.
    /// Sends events on when a warp is started and ended.
    /// </summary>
    [AddComponentMenu("Farming Kit/Systems/Warp System")]
    public class WarpSystem : GameSystem
    {
        [SerializeField]
        private WarpEvent RequestWarp;

        [SerializeField]
        private StringEvent OnSceneWarp;

        [SerializeField]
        private ScriptableReference player;

        [SerializeField]
        private FloatEvent onWarpStart;

        [SerializeField]
        private FloatEvent onWarpEnd;

        [SerializeField]
        private float sceneWarpTime;

        [SerializeField]
        private StringReference startingScene;

        private string currentScene;

        public override void OnLoadSystem()
        {
            RequestWarp?.AddListener(Warp);
        }

        private void OnDestroy()
        {
            RequestWarp?.RemoveListener(Warp);
        }

        private async void Start()
        {
            if (SceneManager.sceneCount > 1)
            {
                // Get the additive scene (Does not equal this scene)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (scene.name != gameObject.scene.name)
                    {
                        currentScene = scene.name;
                        break;
                    }
                }

                return;
            }

            int loadedScenes = SceneManager.sceneCount;
            if (loadedScenes <= 1)
            {
                SaveMaster.GetMetaData("savedata", out string saveJson);
                // If there is save data, load the scene from the save data.
                if (!string.IsNullOrEmpty(saveJson))
                {
                    SaveData saveData = JsonUtility.FromJson<SaveData>(saveJson);
                    currentScene = saveData.lastScene;
                    if (!string.IsNullOrEmpty(currentScene))
                        await SceneManager.LoadSceneAsync(saveData.lastScene, LoadSceneMode.Additive);
                }
            }

            if (!string.IsNullOrEmpty(currentScene))
            {
                OnSceneWarp?.Invoke(currentScene);
            }
            else
            {
                currentScene = startingScene.Value;
                await SceneManager.LoadSceneAsync(startingScene.Value, LoadSceneMode.Additive);
            }
        }

        private async void Warp(WarpLocation location)
        {
            await SwitchScene(location.Scene, currentScene, location.Position);
        }

        private async Awaitable SwitchScene(string target, string previous, Vector3 playerLocation)
        {
            // If within the same scene, just warp the player
            if (target == previous)
            {
                player.Reference.transform.position = playerLocation;
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(target))
            {
                Debug.Log($"Could not load scene: {target}. Ensure it is added to the build settings.");
                return;
            }

            try
            {
                onWarpStart?.Invoke(sceneWarpTime * 0.5f);
                await Awaitable.WaitForSecondsAsync(sceneWarpTime * 0.5f);
                await SceneManager.UnloadSceneAsync(previous);
                await SceneManager.LoadSceneAsync(target, LoadSceneMode.Additive);
                currentScene = target;
                await Awaitable.WaitForSecondsAsync(sceneWarpTime * 0.5f);
                player.Reference.transform.position = playerLocation;
                onWarpEnd?.Invoke(sceneWarpTime * 0.5f);
                OnSceneWarp?.Invoke(target);
            } catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        [System.Serializable]
        public struct RuntimeData
        {
            public string scene;
        }
    }
}
