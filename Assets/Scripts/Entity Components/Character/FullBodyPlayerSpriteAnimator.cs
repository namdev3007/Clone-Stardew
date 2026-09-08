using Entity_Components.Interfaces;
using UnityEngine;

namespace Entity_Components.Character
{
    [DisallowMultipleComponent]
    public class FullBodyPlayerSpriteAnimator : MonoBehaviour, IMove, IAim
    {
        public enum ActionType { None, Chop, Hoe, Water, Harvest }

        [System.Serializable]
        public class DirectionSet
        {
            public UnityEngine.Sprite[] down;
            public UnityEngine.Sprite[] right;
            public UnityEngine.Sprite[] up;
        }

        [SerializeField] private SpriteRenderer target;
        [SerializeField] private float idleFps = 5f;
        [SerializeField] private float walkFps = 10f;
        [SerializeField] private float actionFps = 10f;
        [SerializeField] private DirectionSet idle;
        [SerializeField] private DirectionSet walk;
        [SerializeField] private DirectionSet chop;
        [SerializeField] private DirectionSet hoe;
        [SerializeField] private DirectionSet water;
        [SerializeField] private DirectionSet harvest;
        [Header("Hold item")]
        [SerializeField] private DirectionSet holdIdle;
        [SerializeField] private DirectionSet holdWalk;
        [SerializeField] private Vector2 heldItemOffset = new Vector2(0f, 0.21f);
        [SerializeField, Min(0.01f), Tooltip("Maximum world-space width/height of the item above the player.")]
        private float heldItemMaxSize = 0.14f;

        private Vector2 direction = Vector2.down;
        private float velocity;
        private float elapsed;
        private float actionSpeed = 1f;
        private ActionType action;
        private UnityEngine.Sprite heldItem;
        private SpriteRenderer heldItemRenderer;

        public void SetHeldItem(UnityEngine.Sprite itemSprite)
        {
            heldItem = itemSprite;
            EnsureHeldItemRenderer();
            heldItemRenderer.sprite = itemSprite;
            heldItemRenderer.enabled = itemSprite != null && action == ActionType.None;
            elapsed = 0f;
        }

        public void SetMovement(Vector2 newDirection, float newVelocity)
        {
            if (newDirection.sqrMagnitude > 0.001f)
                direction = newDirection.normalized;
            velocity = newVelocity;
        }

        public void OnMove(Vector2 newDirection, float newVelocity)
        {
            SetMovement(newDirection, newVelocity);
        }

        public void OnAim(Vector2 newDirection)
        {
            SetMovement(newDirection, 0f);
        }

        public void PlayAction(ActionType type, float speed)
        {
            action = type;
            actionSpeed = Mathf.Max(0.01f, speed);
            elapsed = 0f;
            velocity = 0f;
        }

        private void Awake()
        {
            // The old character is assembled from body parts. Keep all gameplay
            // components, but hide those renderers while this full-body skin is active.
            foreach (SpriteRenderer renderer in transform.parent.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.enabled = renderer == target;

            EnsureHeldItemRenderer();
        }

        private void Update()
        {
            if (target == null) return;

            bool displayingHeldItem = heldItem != null && action == ActionType.None;
            DirectionSet set = action == ActionType.None
                ? (displayingHeldItem
                    ? (velocity > 0.01f ? holdWalk : holdIdle)
                    : (velocity > 0.01f ? walk : idle))
                : GetActionSet(action);
            UnityEngine.Sprite[] frames = GetFrames(set);
            if (frames == null || frames.Length == 0) return;

            float fps = action == ActionType.None
                ? (velocity > 0.01f ? walkFps : idleFps)
                : actionFps * actionSpeed;
            int frame = Mathf.FloorToInt(elapsed * fps);

            if (action != ActionType.None && frame >= frames.Length)
            {
                action = ActionType.None;
                elapsed = 0f;
                frames = GetFrames(idle);
                frame = 0;
            }

            if (frames != null && frames.Length > 0)
            {
                // Standing hold sprites are a one-shot lift animation. Once the
                // object reaches the top, keep the final pose instead of looping.
                int displayedFrame = displayingHeldItem && velocity <= 0.01f
                    ? Mathf.Min(frame, frames.Length - 1)
                    : frame % frames.Length;
                target.sprite = frames[displayedFrame];
            }
            target.flipX = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y) && direction.x < 0f;
            UpdateHeldItemRenderer(displayingHeldItem, frame);
            elapsed += UnityEngine.Time.deltaTime;
        }

        private void EnsureHeldItemRenderer()
        {
            if (heldItemRenderer != null || target == null)
                return;

            Transform existing = target.transform.Find("Held Item");
            if (existing != null)
                heldItemRenderer = existing.GetComponent<SpriteRenderer>();

            if (heldItemRenderer == null)
            {
                GameObject itemObject = new GameObject("Held Item");
                itemObject.layer = target.gameObject.layer;
                itemObject.transform.SetParent(target.transform, false);
                heldItemRenderer = itemObject.AddComponent<SpriteRenderer>();
            }

            heldItemRenderer.sortingLayerID = target.sortingLayerID;
            heldItemRenderer.sortingOrder = target.sortingOrder + 1;
            heldItemRenderer.enabled = false;
        }

        private void UpdateHeldItemRenderer(bool visible, int frame)
        {
            EnsureHeldItemRenderer();
            if (heldItemRenderer == null)
                return;

            heldItemRenderer.enabled = visible;
            if (!visible)
                return;

            heldItemRenderer.sprite = heldItem;
            heldItemRenderer.flipX = false;
            Vector2 itemSize = heldItem != null ? heldItem.bounds.size : Vector2.one;
            float largestDimension = Mathf.Max(itemSize.x, itemSize.y);
            float normalizedScale = largestDimension > 0f ? heldItemMaxSize / largestDimension : 1f;
            heldItemRenderer.transform.localScale = Vector3.one * normalizedScale;
            // Keep the object locked to the hands. The character frames already
            // contain the walking motion, so adding another bob makes it float.
            heldItemRenderer.transform.localPosition = new Vector3(heldItemOffset.x, heldItemOffset.y, -0.01f);
        }

        private DirectionSet GetActionSet(ActionType type)
        {
            switch (type)
            {
                case ActionType.Chop: return chop;
                case ActionType.Hoe: return hoe;
                case ActionType.Water: return water;
                case ActionType.Harvest: return harvest;
                default: return idle;
            }
        }

        private UnityEngine.Sprite[] GetFrames(DirectionSet set)
        {
            if (set == null) return null;
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y)) return set.right;
            return direction.y > 0f ? set.up : set.down;
        }
    }
}
