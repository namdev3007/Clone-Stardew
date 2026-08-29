using Event.Events;
using Event.Unity_Events;
using UnityEngine;
using UnityEngine.Events;

namespace Event.Listeners
{
    [AddComponentMenu("Farming Kit/Events/Int Event Listener")]
    public class IntEventListener : ScriptableEventListener<int>
    {
        [SerializeField]
        protected IntEvent eventObject;

        [SerializeField]
        protected UnityEventInt eventAction;

        protected override ScriptableEvent<int> ScriptableEvent
        {
            get
            {
                return eventObject;
            }
        }

        protected override UnityEvent<int> Action
        {
            get
            {
                return eventAction;
            }
        }
    }
}
