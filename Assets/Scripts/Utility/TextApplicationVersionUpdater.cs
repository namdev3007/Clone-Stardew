using UnityEngine;

namespace Utility
{
    public class TextApplicationVersionUpdater : MonoBehaviour
    {
        [SerializeField]
        private TMPro.TextMeshProUGUI text;

        private void Awake()
        {
            text?.SetText($"Version {Application.version}");
        }
    }
}
