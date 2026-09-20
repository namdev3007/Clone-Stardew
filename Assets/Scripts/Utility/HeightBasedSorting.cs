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

        [Tooltip("Optional renderer whose visible bottom is used as the ground contact point.")]
        [SerializeField]
        private SpriteRenderer groundAnchorRenderer;

        [SerializeField]
        private bool useRendererBottom;

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
            if (!Mathf.Approximately(lastY, GetGroundY()))
                UpdateOrder();
        }

        public void OnMove(Vector2 direction, float velocity)
        {
            UpdateOrder();
        }

        /// <summary>
        /// Uses a sprite's bottom edge as the depth anchor while leaving that
        /// SpriteRenderer's authored Order in Layer untouched. The surrounding
        /// SortingGroup is what is compared with the player and other props.
        /// </summary>
        public void ConfigureGroundAnchor(SpriteRenderer renderer)
        {
            sortingGroup = GetComponent<SortingGroup>();
            groundAnchorRenderer = renderer;
            useRendererBottom = renderer != null;
            UpdateOrder();
        }

        private void UpdateOrder()
        {
            if (sortingGroup != null)
            {
                lastY = GetGroundY();
                sortingGroup.sortingOrder = Mathf.RoundToInt(lastY * positionScaling);
            }
        }

        private float GetGroundY()
        {
            return useRendererBottom && groundAnchorRenderer != null
                ? groundAnchorRenderer.bounds.min.y
                : transform.position.y;
        }
    }
}
