using Plugins.Lowscope.ComponentSaveSystem;
using Plugins.Lowscope.ComponentSaveSystem.Core;
using Referencing.Scriptable_Variables.Variables;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Action.Actions
{
    [CreateAssetMenu(menuName = "Actions/New Game" )]
    public class ActionNewGame : ScriptableObject
    {
        [SerializeField]
        private StringVariable playerScene;

        public void Execute()
        {
            int getUnusedSlot = SaveFileUtility.GetAvailableSaveSlot();
            SaveMaster.SetSlot(getUnusedSlot, true);
            SceneManager.LoadScene(playerScene.Value);
        }
    }
}
