using Event.Events;
using UnityEngine;
using UnityEngine.Events;

namespace Event.Listeners
{
    [AddComponentMenu("Farming Kit/Events/Game Event Listener")]
    public class GameEventListener : MonoBehaviour
    {
        public GameEvent ScriptableEvent;

        public UnityEvent Action;

        public void Dispatch()
        {
            Action?.Invoke();
        }

        public void OnEnable()
        {
            ScriptableEvent?.AddListener(this);
        }

        public void OnDisable()
        {
            ScriptableEvent?.RemoveListener(this);
        }
    }
}
