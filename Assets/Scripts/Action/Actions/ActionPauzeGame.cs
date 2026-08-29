using Event.Events;
using UnityEngine;

namespace Action.Actions
{
    [CreateAssetMenu(menuName = "Actions/Pauze Game")]
    public class ActionPauzeGame : ScriptableObject
    {
        [SerializeField]
        private BoolEvent pauzeEvent;

        public void Execute(bool state)
        {
            pauzeEvent.Invoke(state);
        }

    }
}
