using UnityEngine;
using UnityEngine.EventSystems;

namespace User_Interface
{
    public sealed class PauseMenuHoverIndicator : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private GameObject indicator;

        private void Awake()
        {
            SetIndicator(false);
        }

        private void OnDisable()
        {
            SetIndicator(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetIndicator(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetIndicator(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            SetIndicator(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            SetIndicator(false);
        }

        private void SetIndicator(bool visible)
        {
            if (indicator != null)
            {
                indicator.SetActive(visible);
            }
        }
    }
}
