using UnityEngine;
using UnityEngine.Events;

namespace Event.Listeners
{
    public class OnEnableListener : MonoBehaviour
    {
        [SerializeField]
        private UnityEvent enableEvent;

        private void OnEnable()
        {
            enableEvent.Invoke();
        }
    }
}
