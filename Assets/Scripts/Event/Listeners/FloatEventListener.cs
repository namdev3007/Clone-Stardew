using Event.Events;
using Event.Unity_Events;
using UnityEngine;
using UnityEngine.Events;

namespace Event.Listeners
{
    [AddComponentMenu("Farming Kit/Events/Float Event Listener")]
    public class FloatEventListener : ScriptableEventListener<float>
    {
        [SerializeField]
        protected FloatEvent eventObject;

        [SerializeField]
        protected UnityEventFloat eventAction;

        protected override ScriptableEvent<float> ScriptableEvent
        {
            get
            {
                return eventObject;
            }
        }

        protected override UnityEvent<float> Action
        {
            get
            {
                return eventAction;
            }
        }
    }
}
