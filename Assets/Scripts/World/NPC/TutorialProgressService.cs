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
            public bool metSeller;
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
        public bool MetSeller { get { EnsureLoaded(); return data.metSeller; } }
        public int HoedCellCount { get { EnsureLoaded(); return data.hoedCells.Count; } }
        public bool HoeObjectiveComplete => HoedCellCount >= 3;

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

        public void MarkGrandpaLessonComplete()
        {
            EnsureLoaded();
            data.completedGrandpaLesson = true;
            Commit();
        }

        public void MarkMetSeller()
        {
            EnsureLoaded();
            data.metSeller = true;
            Commit();
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
