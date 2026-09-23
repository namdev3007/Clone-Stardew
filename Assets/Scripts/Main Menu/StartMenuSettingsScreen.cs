using Action.Actions;
using Referencing;
using UnityEngine;
using UnityEngine.UI;
using User_Interface;

namespace Main_Menu
{
    [DisallowMultipleComponent]
    public sealed class StartMenuSettingsScreen : MonoBehaviour
    {
        [SerializeField] private GameObject mainScreen;
        [SerializeField] private SettingsSoundUI settingsSoundUI;
        [SerializeField] private Button backButton;
        [SerializeField] private Button okButton;
        [SerializeField] private ActionPlaySound soundAction;
        [SerializeField] private SoundCollection clickSound;

        public void Configure(
            GameObject main,
            SettingsSoundUI soundUI,
            Button back,
            Button ok,
            ActionPlaySound action,
            SoundCollection sound)
        {
            mainScreen = main;
            settingsSoundUI = soundUI;
            backButton = back;
            okButton = ok;
            soundAction = action;
            clickSound = sound;
        }

        private void Awake()
        {
            Bind();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }
        }

        private void Bind()
        {
            Unbind();
            if (backButton != null)
                backButton.onClick.AddListener(Hide);
            if (okButton != null)
                okButton.onClick.AddListener(Hide);
        }

        private void Unbind()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(Hide);
            if (okButton != null)
                okButton.onClick.RemoveListener(Hide);
        }

        public void Show()
        {
            if (mainScreen != null)
                mainScreen.SetActive(false);

            gameObject.SetActive(true);

            if (settingsSoundUI != null)
                settingsSoundUI.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (soundAction != null && clickSound != null)
                soundAction.PlaySoundCollection(clickSound);

            if (settingsSoundUI != null)
                settingsSoundUI.SaveSettings();

            gameObject.SetActive(false);

            if (mainScreen != null)
                mainScreen.SetActive(true);
        }
    }
}
