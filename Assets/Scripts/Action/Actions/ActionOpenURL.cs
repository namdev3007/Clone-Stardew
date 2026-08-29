using Referencing.Scriptable_Variables.Variables;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Action.Actions
{
    [CreateAssetMenu(menuName = "Actions/OpenURL")]
    public class ActionOpenURL : ScriptableObject
    {
        public void Load(string url)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
        openWindow(url);
#else
            Application.OpenURL(url);
#endif
        }

        public void Load(StringVariable variable)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
        openWindow(variable.Value);
#else
            Application.OpenURL(variable.Value);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void openWindow(string url);
#endif
    }
}
