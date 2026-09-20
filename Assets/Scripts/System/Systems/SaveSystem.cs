using Data;
using Event.Events;
using Plugins.Lowscope.ComponentSaveSystem;
using Referencing.Scriptable_Variables.References;
using System;
using System.Globalization;
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
            bool pendingNewGame = SaveMaster.GetMetaData("new-game-pending", out string pendingValue) &&
                                  pendingValue == "1";

            if (string.IsNullOrEmpty(saveJson) || pendingNewGame)
            {
                isNewGame = true;
                string synchronizedName = !string.IsNullOrWhiteSpace(farmName.Value)
                    ? farmName.Value.Trim()
                    : playerName.Value?.Trim() ?? string.Empty;

                SaveData preparedData = !string.IsNullOrEmpty(saveJson)
                    ? JsonUtility.FromJson<SaveData>(saveJson)
                    : null;
                if (string.IsNullOrWhiteSpace(synchronizedName) && preparedData != null)
                    synchronizedName = !string.IsNullOrWhiteSpace(preparedData.farmName)
                        ? preparedData.farmName
                        : preparedData.playerName;

                SetReferenceValue(playerName, synchronizedName);
                SetReferenceValue(farmName, synchronizedName);
                string initialSceneName = initialScene != null ? initialScene.Value : string.Empty;
                if (string.IsNullOrWhiteSpace(initialSceneName) && preparedData != null)
                    initialSceneName = preparedData.lastScene;
                if (string.IsNullOrWhiteSpace(initialSceneName))
                    initialSceneName = "Level_Farm";

                cachedSaveData = new SaveData {
                    lastScene = initialSceneName,
                    playerName = synchronizedName,
                    farmName = synchronizedName,
                    creationDate = preparedData != null && !string.IsNullOrWhiteSpace(preparedData.creationDate)
                        ? preparedData.creationDate
                        : DateTime.Now.ToString("O", CultureInfo.InvariantCulture)
                };
                SaveMaster.SetMetaData("savedata", JsonUtility.ToJson(cachedSaveData));
                SaveMaster.SetMetaData("new-game-pending", "0");
            }
            else
            {
                cachedSaveData = JsonUtility.FromJson<SaveData>(saveJson);
                if (string.IsNullOrWhiteSpace(cachedSaveData.farmName))
                    cachedSaveData.farmName = cachedSaveData.playerName;
                if (string.IsNullOrWhiteSpace(cachedSaveData.playerName))
                    cachedSaveData.playerName = cachedSaveData.farmName;
                SetReferenceValue(playerName, cachedSaveData.playerName);
                SetReferenceValue(farmName, cachedSaveData.farmName);
            }

            onSceneWarp?.AddListener(OnSceneWarp);
        }

        private static void SetReferenceValue(StringReference reference, string value)
        {
            if (reference == null)
                return;
            if (reference.UseConstant)
                reference.ConstantValue = value;
            else if (reference.Variable != null)
                reference.Variable.Value = value;
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
