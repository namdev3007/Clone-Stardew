using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Utility
{
    [AddComponentMenu("Farming Kit/User Interface/Hover Action")]
    public class UIHoverAction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        private UnityEvent onPointerEnter;

        [SerializeField]
        private UnityEvent onPointerExit;

        public void OnPointerEnter(PointerEventData eventData)
        {
            onPointerEnter.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            onPointerExit.Invoke();
        }
    }
}
