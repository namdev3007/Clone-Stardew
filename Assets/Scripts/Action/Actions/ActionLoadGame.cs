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
        }
    }
}