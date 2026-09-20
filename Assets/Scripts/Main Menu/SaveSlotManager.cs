using Plugins.Lowscope.ComponentSaveSystem.Core;
using Referencing.Scriptable_Reference;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using User_Interface;

namespace Main_Menu
{
    public class SaveSlotManager : MonoBehaviour
    {
        private UISaveSlotDisplayer[] saveSlotDisplayers;

        private List<int> saveGames;

        [SerializeField]
        private GameObject saveSlotContainer;

        [SerializeField]
        private Button buttonTabRight;

        [SerializeField]
        private Button buttonTabLeft;

        [SerializeField]
        private TextMeshProUGUI tabDisplayer;

        [SerializeField]
        private ScriptableReference confirmationWindow;

        private int currentTabIndex = 0;

        private void Awake()
        {
            saveSlotDisplayers = saveSlotContainer?.GetComponentsInChildren<UISaveSlotDisplayer>(true);

            buttonTabRight?.onClick.AddListener(OnSwitchTabRight);
            buttonTabLeft?.onClick.AddListener(OnSwitchTabLeft);
        }

        private void OnEnable()
        {
            RefreshSaveSlots();
        }

        public void RefreshSaveSlots()
        {
            saveGames = SaveFileUtility.GetUsedSlots().OrderBy(slot => slot).ToList();

            // This screen uses a ScrollRect rather than the old tab buttons. Grow
            // the list to match every used save so newly-created slots are not
            // hidden behind the two scene-authored template rows.
            if (buttonTabLeft == null && buttonTabRight == null)
            {
                EnsureDisplayerCapacity(saveGames.Count);
                LoadScrollableSlots();
                return;
            }

            if (saveGames.Count == 0)
            {
                // Keep the load-game screen visible. Its plus button is the entry
                // point for creating the first save slot.
                currentTabIndex = 0;
                LoadSlots(currentTabIndex);
                return;
            }

            LoadSlots(currentTabIndex);
        }

        private void EnsureDisplayerCapacity(int requiredCount)
        {
            if (saveSlotContainer == null)
                return;

            if (saveSlotDisplayers == null)
                saveSlotDisplayers = saveSlotContainer.GetComponentsInChildren<UISaveSlotDisplayer>(true);
            if (saveSlotDisplayers.Length == 0 || requiredCount <= saveSlotDisplayers.Length)
                return;

            List<UISaveSlotDisplayer> expanded = saveSlotDisplayers.ToList();
            UISaveSlotDisplayer template = saveSlotDisplayers[saveSlotDisplayers.Length - 1];
            while (expanded.Count < requiredCount)
            {
                UISaveSlotDisplayer clone = Instantiate(template, saveSlotContainer.transform);
                clone.name = $"UI Save Slot Displayer ({expanded.Count + 1})";
                clone.transform.localScale = Vector3.one;
                expanded.Add(clone);
            }

            saveSlotDisplayers = expanded.ToArray();
        }

        private void LoadScrollableSlots()
        {
            if (saveSlotDisplayers == null)
                return;

            for (int i = 0; i < saveSlotDisplayers.Length; i++)
            {
                bool hasSave = i < saveGames.Count;
                saveSlotDisplayers[i].gameObject.SetActive(hasSave);
                if (hasSave)
                    saveSlotDisplayers[i].LoadSlot(saveGames[i]);
            }

            currentTabIndex = 0;
            if (saveSlotContainer != null && saveSlotContainer.transform is RectTransform content)
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void LoadSlots(int offset)
        {
            for (int i = 0; i < saveSlotDisplayers.Length; i++)
            {
                int slotIndex = i + (offset * saveSlotDisplayers.Length);

                if (slotIndex >= 0 && slotIndex < saveGames.Count)
                {
                    saveSlotDisplayers[i].LoadSlot(saveGames[slotIndex]);
                }
                else
                {
                    saveSlotDisplayers[i].SetEmpty();
                }

            }

            buttonTabLeft?.gameObject.SetActive(offset + 1 > 1);
            buttonTabRight?.gameObject.SetActive(saveGames.Count > saveSlotDisplayers.Length * (offset + 1));

            tabDisplayer?.transform.parent.gameObject.SetActive(saveGames.Count > saveSlotDisplayers.Length);
            tabDisplayer?.SetText($"Tab {offset + 1}");
        }

        private void OnSwitchTabLeft()
        {
            LoadSlots(currentTabIndex -= 1);
        }

        private void OnSwitchTabRight()
        {
            LoadSlots(currentTabIndex += 1);
        }
    }
}
