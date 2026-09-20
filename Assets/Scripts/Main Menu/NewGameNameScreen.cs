using Action.Actions;
using Referencing.Scriptable_Variables.Variables;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using User_Interface;

namespace Main_Menu
{
    /// <summary>
    /// Single-step New Game screen. The entered name is intentionally shared
    /// by the player and world so save creation and the load-game list stay in sync.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NewGameNameScreen : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nameInput;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text questionText;
        [SerializeField] private TMP_Text placeholderText;
        [SerializeField] private GameObject mainScreen;
        [SerializeField] private StringVariable playerName;
        [SerializeField] private StringVariable worldName;
        [SerializeField] private ActionNewGame newGameAction;

        public void Configure(
            TMP_InputField input,
            Button confirm,
            Button back,
            TMP_Text title,
            TMP_Text question,
            TMP_Text placeholder,
            GameObject main,
            StringVariable playerNameVariable,
            StringVariable worldNameVariable,
            ActionNewGame action)
        {
            nameInput = input;
            confirmButton = confirm;
            backButton = back;
            titleText = title;
            questionText = question;
            placeholderText = placeholder;
            mainScreen = main;
            playerName = playerNameVariable;
            worldName = worldNameVariable;
            newGameAction = action;
        }

        private void Awake()
        {
            Bind();
        }

        private void OnEnable()
        {
            Bind();
            SettingsSoundUI.LanguageChanged += ApplyLanguage;
            ApplyLanguage(SettingsSoundUI.UseVietnamese);
            if (nameInput == null)
                return;

            nameInput.text = string.Empty;
            RefreshConfirmState(string.Empty);
            nameInput.Select();
            nameInput.ActivateInputField();
        }

        private void OnDestroy()
        {
            SettingsSoundUI.LanguageChanged -= ApplyLanguage;
            if (nameInput != null)
                nameInput.onValueChanged.RemoveListener(RefreshConfirmState);
            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(Confirm);
            if (backButton != null)
                backButton.onClick.RemoveListener(Hide);
        }

        private void OnDisable()
        {
            SettingsSoundUI.LanguageChanged -= ApplyLanguage;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
                return;
            }

            if ((UnityEngine.Input.GetKeyDown(KeyCode.Return) ||
                 UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter)) &&
                confirmButton != null && confirmButton.interactable)
            {
                Confirm();
            }
        }

        private void Bind()
        {
            if (nameInput != null)
            {
                nameInput.onValueChanged.RemoveListener(RefreshConfirmState);
                nameInput.onValueChanged.AddListener(RefreshConfirmState);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(Confirm);
                confirmButton.onClick.AddListener(Confirm);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(Hide);
                backButton.onClick.AddListener(Hide);
            }
        }

        private void ApplyLanguage(bool vietnamese)
        {
            if (titleText != null)
                titleText.text = vietnamese ? "Chào mừng đến với Meadow!" : "Welcome to Meadow!";
            if (questionText != null)
                questionText.text = vietnamese ? "Tên của bạn là gì?" : "What's your name?";
            if (placeholderText != null)
                placeholderText.text = vietnamese ? "Nhập tên" : "Enter Name";
        }

        public void Show()
        {
            mainScreen?.SetActive(false);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            mainScreen?.SetActive(true);
        }

        private void RefreshConfirmState(string value)
        {
            if (confirmButton != null)
                confirmButton.interactable = !string.IsNullOrWhiteSpace(value);
        }

        public void Confirm()
        {
            string enteredName = nameInput != null ? nameInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(enteredName) || playerName == null || worldName == null || newGameAction == null)
                return;

            playerName.Value = enteredName;
            worldName.Value = enteredName;
            newGameAction.Execute(enteredName);
        }
    }
}
