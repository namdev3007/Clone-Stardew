using Plugins.Lowscope.ComponentSaveSystem;
using Referencing.Scriptable_Variables.Variables;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Action.Actions
{
    [CreateAssetMenu(menuName = "Actions/Load Game")]
    public class ActionLoadGame : ScriptableObject
    {
        [SerializeField]
        private StringVariable playerScene;

        public void Execute(int slotNumber)
        {
            SaveMaster.SetSlot(slotNumber, true);

            SaveMaster.GetMetaData("savedata", out string saveJson);
            if (!string.IsNullOrEmpty(saveJson))
            {
                SceneManager.LoadScene(playerScene.Value); 
                // Last saved scene is loaded through the WarpSystem
            }
            else
            {
                Data.SaveData fallbackData = new Data.SaveData
                {
                    lastScene = "Level_Farm",
                    playerName = $"Meadow {slotNumber + 1}",
                    farmName = $"Meadow {slotNumber + 1}",
                    creationDate = System.DateTime.Now.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                    timePlayed = System.TimeSpan.Zero.ToString()
                };
                SaveMaster.SetMetaData("savedata", JsonUtility.ToJson(fallbackData));
                SceneManager.LoadScene(playerScene.Value);
            }
        }
    }
}