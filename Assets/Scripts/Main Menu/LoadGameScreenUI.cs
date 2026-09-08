using UnityEngine;
using UnityEngine.UI;

namespace Main_Menu
{
    /// <summary>
    /// Bridges the new plus button on the load-game screen to the existing New
    /// Game button, preserving the project's original character/new-save flow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadGameScreenUI : MonoBehaviour
    {
        [SerializeField] private Button addSaveButton;
        [SerializeField] private Button existingNewGameButton;

        public void Configure(Button addButton, Button newGameButton)
        {
            addSaveButton = addButton;
            existingNewGameButton = newGameButton;
        }

        private void Awake()
        {
            BindButton();
        }

        private void OnEnable()
        {
            BindButton();
        }

        private void BindButton()
        {
            if (addSaveButton == null)
                return;

            addSaveButton.onClick.RemoveListener(OpenNewGameScreen);
            addSaveButton.onClick.AddListener(OpenNewGameScreen);
        }

        public void OpenNewGameScreen()
        {
            existingNewGameButton?.onClick.Invoke();
        }
    }
}
