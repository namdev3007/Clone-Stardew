using Event.Events;
using Event.Unity_Events;
using UnityEngine;
using UnityEngine.Events;

namespace Event.Listeners
{
    [AddComponentMenu("Farming Kit/Events/Vector Event Listener")]
    public class Vector2Listener : ScriptableEventListener<Vector2>
    {
        public Vector2Event eventObject;

        public UnityEventVector2 eventAction = new UnityEventVector2();

        protected override ScriptableEvent<Vector2> ScriptableEvent
        {
            get
            {
                return eventObject;
            }
        }

        protected override UnityEvent<Vector2> Action
        {
            get
            {
                return eventAction;
            }
        }
    }
}
