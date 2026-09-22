using Event.Events;
using Plugins.Lowscope.ComponentSaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace User_Interface
{
    public sealed class PauseGameMenuController : MonoBehaviour
    {
        private static PauseGameMenuController activeController;

        [SerializeField] private GameObject pauseMenuRoot;
        [SerializeField] private GameObject settingsRoot;
        [SerializeField] private BoolEvent pauseStateEvent;
        [SerializeField] private BoolEvent displayPauseMenuEvent;
        [SerializeField] private string mainMenuScene = "StartMenu 1";
        [Header("Quit confirmation")]
        [SerializeField] private GameObject quitConfirmationRoot;
        [SerializeField] private TMP_Text quitConfirmationText;

        private bool quitToDesktopPending;

        private void Awake()
        {
            // The confirmation stays active in the scene so it is visible in
            // Edit Mode; hide it as soon as the game starts.
            CloseQuitConfirmation();
        }

        private void OnEnable()
        {
            activeController = this;
            displayPauseMenuEvent?.AddListener(OnDisplayPauseMenuChanged);
        }

        private void OnDisable()
        {
            displayPauseMenuEvent?.RemoveListener(OnDisplayPauseMenuChanged);
            if (activeController == this)
                activeController = null;
        }

        public static bool TryHandleSettingsBack()
        {
            if (activeController == null)
                return false;

            if (activeController.quitConfirmationRoot != null &&
                activeController.quitConfirmationRoot.activeSelf)
            {
                activeController.CancelQuit();
                return true;
            }

            if (activeController.settingsRoot == null || !activeController.settingsRoot.activeSelf)
                return false;

            activeController.ShowPauseMenu();
            return true;
        }

        private void OnDisplayPauseMenuChanged(bool visible)
        {
            if (visible)
            {
                ShowPauseMenu();
                return;
            }

            if (settingsRoot != null)
            {
                settingsRoot.SetActive(false);
            }

            CloseQuitConfirmation();
        }

        public void ContinueGame()
        {
            CloseQuitConfirmation();
            ShowPauseMenu();
            displayPauseMenuEvent?.Invoke(false);
            pauseStateEvent?.Invoke(false);
        }

        public void SaveGame()
        {
            if (SaveMaster.IsSlotLoaded())
            {
                SaveMaster.WriteActiveSaveToDisk();
                Debug.Log("Game saved from pause menu.");
            }
            else
                Debug.LogWarning("Pause menu could not save because no save slot is loaded.");
        }

        public void OpenSettings()
        {
            CloseQuitConfirmation();
            if (pauseMenuRoot != null)
            {
                pauseMenuRoot.SetActive(false);
            }

            if (settingsRoot != null)
            {
                settingsRoot.SetActive(true);
                settingsRoot.transform.SetAsLastSibling();
            }
        }

        public void ShowPauseMenu()
        {
            CloseQuitConfirmation();
            if (settingsRoot != null)
            {
                settingsRoot.SetActive(false);
            }

            if (pauseMenuRoot != null)
            {
                pauseMenuRoot.SetActive(true);
                pauseMenuRoot.transform.SetAsLastSibling();
            }
        }

        public void ApplySettingsAndReturn()
        {
            settingsRoot?.GetComponentInChildren<SettingsSoundUI>(true)?.SaveSettings();
            ShowPauseMenu();
        }

        public void QuitToMenu()
        {
            ShowQuitConfirmation(false);
        }

        public void QuitToDesktop()
        {
            ShowQuitConfirmation(true);
        }

        public void ConfirmQuit()
        {
            bool desktop = quitToDesktopPending;
            CloseQuitConfirmation();
            if (desktop)
                ExecuteQuitToDesktop();
            else
                ExecuteQuitToMenu();
        }

        public void CancelQuit()
        {
            CloseQuitConfirmation();
            if (pauseMenuRoot != null)
                pauseMenuRoot.SetActive(true);
        }

        private void ShowQuitConfirmation(bool desktop)
        {
            quitToDesktopPending = desktop;
            if (quitConfirmationText != null)
            {
                quitConfirmationText.color = new Color32(0xF7, 0xCA, 0x92, 0xFF);
                quitConfirmationText.text = desktop
                    ? "DO YOU WANT TO QUIT THE GAME?"
                    : "DO YOU WANT TO RETURN TO MENU?";
            }

            if (settingsRoot != null)
                settingsRoot.SetActive(false);
            if (pauseMenuRoot != null)
                pauseMenuRoot.SetActive(false);
            if (quitConfirmationRoot != null)
            {
                quitConfirmationRoot.SetActive(true);
                quitConfirmationRoot.transform.SetAsLastSibling();
            }
        }

        private void CloseQuitConfirmation()
        {
            if (quitConfirmationRoot != null)
                quitConfirmationRoot.SetActive(false);
        }

        private void ExecuteQuitToMenu()
        {
            SaveGame();
            displayPauseMenuEvent?.Invoke(false);
            pauseStateEvent?.Invoke(false);
            UnityEngine.Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuScene);
        }

        private void ExecuteQuitToDesktop()
        {
            SaveGame();
            UnityEngine.Time.timeScale = 1f;

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
