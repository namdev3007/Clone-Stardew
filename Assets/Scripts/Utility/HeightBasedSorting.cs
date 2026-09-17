using Entity_Components.Interfaces;
using UnityEngine;
using UnityEngine.Rendering;

namespace Utility
{
    /// <summary>
    /// Sorts the attached sorting group based on the Y axis.
    /// </summary>

    [AddComponentMenu("Farming Kit/User Interface/Height Based Sorting")]
    [RequireComponent(typeof(SortingGroup)), DisallowMultipleComponent]
    public class HeightBasedSorting : MonoBehaviour, IMove
    {
        [SerializeField]
        private SortingGroup sortingGroup;

        [SerializeField]
        private float positionScaling = -100;

        [SerializeField]
        private bool flip;

        private float lastY = float.NaN;

        private void OnValidate()
        {
            if (sortingGroup == null)
            {
                sortingGroup = GetComponent<SortingGroup>();
            }

            UpdateOrder();
        }

        private void Start()
        {
            UpdateOrder();
        }

        private void LateUpdate()
        {
            // Also covers teleports and movement code which does not dispatch IMove.
            if (!Mathf.Approximately(lastY, transform.position.y))
                UpdateOrder();
        }

        public void OnMove(Vector2 direction, float velocity)
        {
            UpdateOrder();
        }

        private void UpdateOrder()
        {
            if (sortingGroup != null)
            {
                lastY = transform.position.y;
                sortingGroup.sortingOrder = Mathf.RoundToInt(lastY * positionScaling);
            }
        }
    }
}
