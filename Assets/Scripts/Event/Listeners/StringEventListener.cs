using Event.Events;
using Event.Unity_Events;
using UnityEngine;
using UnityEngine.Events;

namespace Event.Listeners
{
    [AddComponentMenu("Farming Kit/Events/String Event Listener")]
    public class StringEventListener : ScriptableEventListener<string>
    {
        [SerializeField]
        protected StringEvent eventObject;

        [SerializeField]
        protected UnityEventString eventAction;

        protected override ScriptableEvent<string> ScriptableEvent
        {
            get
            {
                return eventObject;
            }
        }

        protected override UnityEvent<string> Action
        {
            get
            {
                return eventAction;
            }
        }
    }
}
