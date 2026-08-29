using Referencing.Scriptable_Reference;
using UnityEngine;
using User_Interface;

namespace Action.Actions
{
    [CreateAssetMenu(menuName = "Actions/Fade Screen")]
    public class ActionFadeScreen : ScriptableObject
    {
        [SerializeField]
        private ScriptableReference fadeScreenReference;

        public void FadeScreen(float duration)
        {
            if (fadeScreenReference != null)
            {
                fadeScreenReference.Reference.GetComponentInChildren<FadeImage>().Hide(duration);
                GameObject.DontDestroyOnLoad(fadeScreenReference.Reference);
            }
        }
    }
}
