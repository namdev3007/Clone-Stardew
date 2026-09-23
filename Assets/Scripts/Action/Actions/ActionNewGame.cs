using Data;
using Plugins.Lowscope.ComponentSaveSystem;
using Plugins.Lowscope.ComponentSaveSystem.Core;
using Referencing.Scriptable_Variables.Variables;
using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Action.Actions
{
    [CreateAssetMenu(menuName = "Actions/New Game" )]
    public class ActionNewGame : ScriptableObject
    {
        [SerializeField]
        private StringVariable playerScene;

        [SerializeField]
        private StringVariable firstScene;

        public void Execute()
        {
            Execute(string.Empty);
        }

        public void Execute(string synchronizedName)
        {
            int getUnusedSlot = SaveFileUtility.GetAvailableSaveSlot();
            if (getUnusedSlot < 0)
            {
                Debug.LogError("No available save slot found for New Game.");
                return;
            }

            SaveMaster.SetSlot(getUnusedSlot, true);

            // Create the load-screen metadata before changing scene. Previously it
            // was created later by SaveSystem; leaving the game early (or a system
            // initialization delay) produced a valid save file with an empty
            // metadata sidecar, so the slot looked missing in Screen _ SaveSlots.
            string cleanName = string.IsNullOrWhiteSpace(synchronizedName)
                ? $"Meadow {getUnusedSlot + 1}"
                : synchronizedName.Trim();
            SaveData saveData = new SaveData
            {
                lastScene = firstScene != null && !string.IsNullOrWhiteSpace(firstScene.Value)
                    ? firstScene.Value
                    : "Level_Farm",
                playerName = cleanName,
                farmName = cleanName,
                creationDate = DateTime.Now.ToString("O", CultureInfo.InvariantCulture),
                timePlayed = TimeSpan.Zero.ToString()
            };
            SaveMaster.SetMetaData("savedata", JsonUtility.ToJson(saveData));
            SaveMaster.SetMetaData("new-game-pending", "1");
            SaveMaster.WriteActiveSaveToDisk(false);

            SceneManager.LoadScene(playerScene.Value);
        }
    }
}
