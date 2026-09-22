using System;
using System.Collections.Generic;
using Plugins.Lowscope.ComponentSaveSystem;
using UnityEngine;

namespace World.NPC
{
    [DisallowMultipleComponent]
    public sealed class TutorialProgressService : MonoBehaviour
    {
        private const string SaveKey = "npc-tutorial-progress-v1";

        [Serializable]
        private sealed class ProgressData
        {
            public bool metGrandpa;
            public bool receivedStarterTools;
            public bool completedGrandpaLesson;
            public bool playedGrandpaCompletionDialogue;
            public bool metSeller;
            public int highestUnlockedCropOrder;
            public List<string> hoedCells = new List<string>();
        }

        private static TutorialProgressService instance;
        private ProgressData data = new ProgressData();
        private bool loaded;
        private int loadedSlot = int.MinValue;

        public static TutorialProgressService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<TutorialProgressService>();
                    if (instance == null)
                    {
                        GameObject root = new GameObject("NPC Tutorial Progress");
                        instance = root.AddComponent<TutorialProgressService>();
                        DontDestroyOnLoad(root);
                    }
                }

                instance.EnsureLoaded();
                return instance;
            }
        }

        public bool MetGrandpa { get { EnsureLoaded(); return data.metGrandpa; } }
        public bool ReceivedStarterTools { get { EnsureLoaded(); return data.receivedStarterTools; } }
        public bool CompletedGrandpaLesson { get { EnsureLoaded(); return data.completedGrandpaLesson; } }
        public bool PlayedGrandpaCompletionDialogue
        {
            get { EnsureLoaded(); return data.playedGrandpaCompletionDialogue; }
        }
        public bool MetSeller { get { EnsureLoaded(); return data.metSeller; } }
        public int HighestUnlockedCropOrder
        {
            get { EnsureLoaded(); return Mathf.Clamp(data.highestUnlockedCropOrder, 0, 7); }
        }
        public int HoedCellCount { get { EnsureLoaded(); return data.hoedCells.Count; } }
        public bool HoeObjectiveComplete => HoedCellCount >= 3;
        public bool NeedsGrandpaCompletionDialogue
        {
            get
            {
                EnsureLoaded();
                return !data.playedGrandpaCompletionDialogue &&
                       (data.completedGrandpaLesson || data.hoedCells.Count >= 3);
            }
        }

        public event System.Action ProgressChanged;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureLoaded();
        }

        public void MarkMetGrandpa()
        {
            EnsureLoaded();
            data.metGrandpa = true;
            Commit();
        }

        public void MarkStarterToolsReceived()
        {
            EnsureLoaded();
            data.receivedStarterTools = true;
            Commit();
        }

        public void RecordHoedCell(Vector3Int cell)
        {
            EnsureLoaded();
            if (!data.metGrandpa || data.completedGrandpaLesson)
                return;

            string key = cell.x + "," + cell.y + "," + cell.z;
            if (data.hoedCells.Contains(key))
                return;

            data.hoedCells.Add(key);
            Commit();
        }

        /// <summary>
        /// Reconciles tutorial progress with the soil that is actually hoed in the scene.
        /// This recovers progress when a dialogue was interrupted before its completion
        /// callback, or when an older save contains tilled tiles but no tutorial cell list.
        /// </summary>
        public void SyncHoedCellsFromGrid(GridManager gridManager)
        {
            EnsureLoaded();
            if (!data.metGrandpa || data.completedGrandpaLesson || gridManager == null ||
                gridManager.DirtHoleTileMap == null)
                return;

            bool changed = false;
            foreach (Vector3Int cell in gridManager.DirtHoleTileMap.cellBounds.allPositionsWithin)
            {
                if (!gridManager.DirtHoleTileMap.HasTile(cell) || !gridManager.CanHoeCell(cell))
                    continue;

                string key = cell.x + "," + cell.y + "," + cell.z;
                if (data.hoedCells.Contains(key))
                    continue;

                data.hoedCells.Add(key);
                changed = true;
            }

            if (changed)
                Commit();
        }

        public void MarkGrandpaLessonComplete()
        {
            EnsureLoaded();
            data.completedGrandpaLesson = true;
            Commit();
        }

        public void MarkGrandpaCompletionDialoguePlayed()
        {
            EnsureLoaded();
            data.completedGrandpaLesson = true;
            data.playedGrandpaCompletionDialogue = true;
            Commit();
        }

        public void MarkMetSeller()
        {
            EnsureLoaded();
            data.metSeller = true;
            Commit();
        }

        /// <summary>Developer shortcut used by the hidden in-game cheat panel.</summary>
        public void CheatUnlockAllCrops()
        {
            EnsureLoaded();
            data.metSeller = true;
            data.highestUnlockedCropOrder = 7;
            Commit();
        }

        public void RecordCropHarvested(string cropName)
        {
            EnsureLoaded();
            int harvestedOrder = GetCropOrder(cropName);
            int currentOrder = Mathf.Clamp(data.highestUnlockedCropOrder, 0, 7);

            // Only the currently available crop can advance the chain. This prevents
            // showcase/cheat crops from skipping several shop unlocks at once.
            if (harvestedOrder < 0 || harvestedOrder > currentOrder || currentOrder >= 7)
                return;

            int nextOrder = harvestedOrder + 1;
            if (nextOrder <= currentOrder)
                return;

            data.highestUnlockedCropOrder = nextOrder;
            Commit();
        }

        private static int GetCropOrder(string cropName)
        {
            switch (cropName)
            {
                case "Carrot": return 0;
                case "Onion": return 1;
                case "Garlic": return 2;
                case "Cabbage": return 3;
                case "Potato": return 4;
                case "Tomato": return 5;
                case "Banana": return 6;
                case "Mango": return 7;
                default: return -1;
            }
        }

        private void EnsureLoaded()
        {
            if (!SaveMaster.IsSlotLoaded())
                return;

            int activeSlot = SaveMaster.GetActiveSlot();
            if (loaded && loadedSlot == activeSlot)
                return;

            data = new ProgressData();

            if (SaveMaster.GetMetaData(SaveKey, out string json) && !string.IsNullOrWhiteSpace(json))
            {
                ProgressData loadedData = JsonUtility.FromJson<ProgressData>(json);
                if (loadedData != null)
                    data = loadedData;
            }

            if (data.hoedCells == null)
                data.hoedCells = new List<string>();
            data.highestUnlockedCropOrder = Mathf.Clamp(data.highestUnlockedCropOrder, 0, 7);
            loaded = true;
            loadedSlot = activeSlot;
        }

        private void Commit()
        {
            if (SaveMaster.IsSlotLoaded())
                SaveMaster.SetMetaData(SaveKey, JsonUtility.ToJson(data));
            ProgressChanged?.Invoke();
            DialogueUIController.InstanceOrNull?.RefreshTutorialObjective();
        }
    }
}
