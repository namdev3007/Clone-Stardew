using UnityEngine;

namespace Entity_Components.Character
{
    [DisallowMultipleComponent]
    public class FullBodyPlayerSpriteAnimator : MonoBehaviour
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

        private Vector2 direction = Vector2.down;
        private float velocity;
        private float elapsed;
        private float actionSpeed = 1f;
        private ActionType action;

        public void SetMovement(Vector2 newDirection, float newVelocity)
        {
            if (newDirection.sqrMagnitude > 0.001f)
                direction = newDirection.normalized;
            velocity = newVelocity;
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
        }

        private void Update()
        {
            if (target == null) return;

            DirectionSet set = action == ActionType.None
                ? (velocity > 0.01f ? walk : idle)
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
                target.sprite = frames[frame % frames.Length];
            target.flipX = Mathf.Abs(direction.x) >= Mathf.Abs(direction.y) && direction.x < 0f;
            elapsed += UnityEngine.Time.deltaTime;
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
