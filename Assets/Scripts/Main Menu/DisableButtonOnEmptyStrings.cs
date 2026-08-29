using Referencing.Scriptable_Variables.Variables;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Main_Menu
{
    [RequireComponent(typeof(Button))]
    public class DisableButtonOnEmptyStrings : MonoBehaviour
    {
        private Button button;

        [SerializeField]
        private StringVariable[] strings;

        private void Awake()
        {
            button = this.GetComponent<Button>();
        }

        public void Verify ()
        {

            button.interactable = strings.All(s => !string.IsNullOrEmpty(s.Value));
        }
    }
}
