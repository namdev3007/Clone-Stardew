using Data;
using Event.Events;
using Plugins.Lowscope.ComponentSaveSystem;
using Referencing.Scriptable_Variables.References;
using UnityEngine;

namespace GameSystem.Systems
{
    /// <summary>
    /// Notifies listeners to save the game.
    /// Obtains saved data from callback, and writes it to a file.
    /// </summary>

    [AddComponentMenu("Farming Kit/Systems/Save System")]
    public class SaveSystem : GameSystem
    {
        [SerializeField]
        private GameEvent onNewGameStarted;

        [SerializeField]
        private StringEvent onSceneWarp;

        [SerializeField]
        private FloatEvent onWarpStart;

        [SerializeField]
        private FloatEvent onWarpEnd;

        [SerializeField]
        private StringReference playerName;

        [SerializeField]
        private StringReference farmName;

        [SerializeField]
        private StringReference initialScene;
        
        [SerializeField, Tooltip("The save slot to use when no save slot is assigned.")]
        private int fallBackSaveSlot;
    
        [System.NonSerialized]
        private bool isNewGame;

        private SaveData cachedSaveData;

        public override void OnLoadSystem()
        {
            // Ensures that a save slot is loaded.
            if (!SaveMaster.IsSlotLoaded())
            {
                SaveMaster.SetSlot(fallBackSaveSlot,true);
            }

            SaveMaster.GetMetaData("savedata", out string saveJson);

            if (string.IsNullOrEmpty(saveJson))
            {
                isNewGame = true;
                cachedSaveData = new SaveData {
                    lastScene = initialScene.Value,
                    playerName = playerName.Value,
                    farmName = farmName.Value
                };
                SaveMaster.SetMetaData("savedata", JsonUtility.ToJson(cachedSaveData));
            }
            else
            {
                cachedSaveData = JsonUtility.FromJson<SaveData>(saveJson);
            }

            onSceneWarp?.AddListener(OnSceneWarp);
        }

        private void OnSceneWarp(string scene)
        {
            cachedSaveData.lastScene = scene;
        }

        private void Start()
        {
            if (isNewGame)
            {
                SaveMaster.WriteActiveSaveToDisk();
                onNewGameStarted.Invoke();
            }
        }

        private void OnDestroy()
        {
            // In case no save is loaded, do not set the time played metadata.
            if (!SaveMaster.IsSlotLoaded())
                return;
            
            cachedSaveData.timePlayed = SaveMaster.GetSaveTimePlayed().ToString();
            SaveMaster.SetMetaData("savedata", JsonUtility.ToJson(cachedSaveData));
        }

    }
}
