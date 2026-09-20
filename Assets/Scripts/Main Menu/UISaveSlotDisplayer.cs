using Data;
using Event.Unity_Events;
using Plugins.Lowscope.ComponentSaveSystem;
using Referencing.Scriptable_Reference;
using Referencing.Scriptable_Variables.Variables;
using Saving;
using System;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using User_Interface;

namespace Main_Menu
{
    public class UISaveSlotDisplayer : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI playerNameText;

        [SerializeField]
        private TextMeshProUGUI farmNameText;

        [SerializeField]
        private TextMeshProUGUI dateText;

        [SerializeField]
        private Image characterImage;

        [SerializeField]
        private GameObject characterRendererPrefab;

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private GameObject slotAvailableObjects;

        [SerializeField]
        private GameObject slotUsedObjects;

        [SerializeField]
        private Button buttonRequestRemove;

        [SerializeField]
        private ScriptableReference confirmationWindow;

        [SerializeField]
        private UnityEvent OnRemoveRequested;

        [SerializeField]
        private UnityEventInt onPressedSlot = new UnityEventInt();

        [SerializeField]
        private DisplayCharacterBodyMetaData displayCharacterBodyData;

        [SerializeField]
        private StringVariable startingLevel;

        private int slot;

        private bool isInitialized;

        public void LoadSave()
        {
            if (slot == -1)
            {
                return;
            }

            string saveJson = "";
            SaveData saveData;
            if (SaveMaster.GetMetaData("savedata", out saveJson, slot))
            {
                saveData = JsonUtility.FromJson<SaveData>(saveJson);

                if (string.IsNullOrEmpty(saveData.lastScene))
                {
                    // Older save files may not contain lastScene. Keep the slot
                    // loadable even if the prefab reference was lost during a UI edit.
                    saveData.lastScene = startingLevel != null
                        ? startingLevel.Value
                        : "Level_Farm";
                    SaveMaster.SetMetaData("savedata", JsonUtility.ToJson(saveData), slot);
                }

                if (!Application.CanStreamedLevelBeLoaded(saveData.lastScene))
                {
                    confirmationWindow.Reference.GetComponent<ConfirmationWindow>().Configure(new ConfirmationWindow.Configuration()
                    {
                        acceptOnly = true,
                        question = "Load Error \n (Scene file does not exist in build)",
                        answerYes = "Ok"
                    });

                    return;
                }
            }

            onPressedSlot.Invoke(slot);
        }

        private void Initialize()
        {
            isInitialized = true;
            ResolveTextReferences();

            if (characterImage != null)
            {
                characterImage.preserveAspect = true;
            }

            buttonRequestRemove.onClick.AddListener(RequestRemoveSlot);
        }

        private void ResolveTextReferences()
        {
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            TextMeshProUGUI worldLabel = labels.FirstOrDefault(label =>
                label.name == "Text_idWorldName" || label.name == "Text_WorldName");
            TextMeshProUGUI creationLabel = labels.FirstOrDefault(label =>
                label.name == "Text_CreationDate" || label.name == "Text_date");

            if (worldLabel != null)
            {
                worldLabel.name = "Text_idWorldName";
                farmNameText = worldLabel;
            }
            if (creationLabel != null)
                dateText = creationLabel;
        }

        private void RequestRemoveSlot()
        {
            ConfirmationWindow getConfirmationWindow = confirmationWindow?.Reference?.GetComponent<ConfirmationWindow>();

            if (getConfirmationWindow != null)
            {
                getConfirmationWindow.Configure(new ConfirmationWindow.Configuration()
                {
                    question = "Do you want to remove this save slot?",
                    answerYes = "Yes",
                    answerNo = "No",
                    actionYes = OnActionRemove
                });
            }
        }

        private void OnActionRemove()
        {
            SaveMaster.DeleteSave(slot);
            LoadSlot(slot);
            OnRemoveRequested.Invoke();
        }

        public void LoadSlot(int slot)
        {
            this.slot = slot;

            if (!isInitialized)
            {
                Initialize();
            }

            if (!SaveMaster.IsSlotUsed(slot))
            {
                SetEmpty();
            }
            else
            {
                canvasGroup.alpha = 1f;

                string saveJson = "";
                SaveData saveData;

                if (!SaveMaster.GetMetaData("savedata", out saveJson, slot) ||
                    string.IsNullOrWhiteSpace(saveJson))
                {
                    // Repair older slots created by the previous New Game flow.
                    // Their savegame exists, but their metadata file can be empty.
                    DateTime fallbackCreationDate = SaveMaster.GetSaveCreationTime(slot);
                    if (fallbackCreationDate == default)
                        fallbackCreationDate = DateTime.Now;
                    saveData = new SaveData
                    {
                        lastScene = startingLevel != null ? startingLevel.Value : "Level_Farm",
                        playerName = $"Meadow {slot + 1}",
                        farmName = $"Meadow {slot + 1}",
                        creationDate = fallbackCreationDate.ToString("O", CultureInfo.InvariantCulture),
                        timePlayed = SaveMaster.GetSaveTimePlayed(slot).ToString()
                    };
                    SaveMaster.SetMetaData("savedata", JsonUtility.ToJson(saveData), slot);
                }
                else
                {
                    saveData = JsonUtility.FromJson<SaveData>(saveJson) ?? new SaveData();
                }

                string worldName = !string.IsNullOrWhiteSpace(saveData.farmName)
                    ? saveData.farmName
                    : !string.IsNullOrWhiteSpace(saveData.playerName)
                        ? saveData.playerName
                        : $"Meadow {slot + 1}";
                farmNameText?.SetText(worldName);

                // The redesigned slot has exactly two labels: world name
                // and the calendar date on which that world was created.
                if (playerNameText != null && playerNameText != farmNameText)
                    playerNameText.gameObject.SetActive(false);

                DateTime creationDate;
                if (!DateTime.TryParse(saveData.creationDate, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out creationDate))
                {
                    creationDate = SaveMaster.GetSaveCreationTime(slot);
                    if (creationDate == default)
                        creationDate = DateTime.Now;
                    saveData.creationDate = creationDate.ToString("O", CultureInfo.InvariantCulture);
                    SaveMaster.SetMetaData("savedata", JsonUtility.ToJson(saveData), slot);
                }
                dateText?.SetText(creationDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));

                slotAvailableObjects.gameObject.SetActive(false);
                slotUsedObjects.gameObject.SetActive(true);

                displayCharacterBodyData?.Load(slot);
            }
        }

        public void SetEmpty()
        {
            this.slot = -1;

            canvasGroup.alpha = 0.5f;

            slotAvailableObjects.gameObject.SetActive(true);
            slotUsedObjects.gameObject.SetActive(false);
        }
    }
}
