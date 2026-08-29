using UnityEngine;
using UnityEngine.SceneManagement;

namespace Action.Actions
{
    [CreateAssetMenu(menuName = "Actions/Open New Scene")]
    public class ActionOpenScene : ScriptableObject
    {
        [SerializeField]
        private string sceneName;

        public void LoadScene(string sceneName)
        {
            SceneManager.UnloadSceneAsync(SceneManager.GetActiveScene().buildIndex);
            SceneManager.LoadScene(sceneName);
            Resources.UnloadUnusedAssets();
        }
    }
}
