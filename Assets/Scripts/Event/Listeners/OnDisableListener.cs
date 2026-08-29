using UnityEngine;
using UnityEngine.Events;

namespace Event.Listeners
{
    public class OnDisableListener : MonoBehaviour
    {
        [SerializeField]
        private UnityEvent disableEvent;

        private void OnDisable()
        {
            disableEvent.Invoke();
        }
    }
}
