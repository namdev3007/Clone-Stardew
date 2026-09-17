using Event.Events;
using User_Interface;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameSystem.Systems
{
    /// <summary>
    /// Handles pauze events
    /// </summary>
    [AddComponentMenu("Farming Kit/Systems/Pauze System")]
    public class PauzeSystem : GameSystem
    {
        [SerializeField]
        private GameEvent pauzeButton;

        [SerializeField]
        private BoolEvent pauzeEvent;

        [SerializeField]
        private BoolEvent displayPauzeMenu;

        [SerializeField]
        private GameEvent requestPauze;

        private bool isPauzed;

        public override void OnLoadSystem()
        {
            pauzeButton?.AddListener(OnPauzeButton);
            requestPauze?.AddListener(OnRequestPauze);
            pauzeEvent?.AddListener(OnPauze);
        }

        private void OnPauze(bool state)
        {
            isPauzed = state;
            UnityEngine.Time.timeScale = state ? 0f : 1f;
        }

        private void OnRequestPauze()
        {
            if (IsMainMenuScene())
            {
                EnsureClosedOnMainMenu();
                return;
            }
            SetPauzed(true);
        }

        private void OnPauzeButton()
        {
            if (IsMainMenuScene())
            {
                EnsureClosedOnMainMenu();
                return;
            }

            if (isPauzed && PauseGameMenuController.TryHandleSettingsBack())
                return;

            if (!isPauzed)
            {
                displayPauzeMenu?.Invoke(true);
            }
            else
            {
                displayPauzeMenu?.Invoke(false);
            }

            isPauzed = !isPauzed;
            SetPauzed(isPauzed);
        }

        private static bool IsMainMenuScene()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            return activeScene.IsValid() && activeScene.name.StartsWith("StartMenu",
                System.StringComparison.OrdinalIgnoreCase);
        }

        private void EnsureClosedOnMainMenu()
        {
            displayPauzeMenu?.Invoke(false);
            if (isPauzed || UnityEngine.Time.timeScale == 0f)
                SetPauzed(false);
        }

        public void SetPauzed(bool state)
        {
            isPauzed = state;
            UnityEngine.Time.timeScale = state ? 0f : 1f;
            pauzeEvent?.Invoke(state);
        }

        private void OnDestroy()
        {
            pauzeButton?.RemoveListener(OnPauzeButton);
            requestPauze?.RemoveListener(OnRequestPauze);
            pauzeEvent?.RemoveListener(OnPauze);
            if (isPauzed)
                UnityEngine.Time.timeScale = 1f;
        }
    }
}
