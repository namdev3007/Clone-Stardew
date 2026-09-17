using UnityEngine;

namespace World.NPC
{
    [DisallowMultipleComponent]
    public sealed class NpcSpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private UnityEngine.Sprite[] idleFrames;
        [SerializeField, Min(0.1f)] private float framesPerSecond = 6f;

        private float elapsed;
        private Vector3 lockedLocalScale;
        private bool hasLockedScale;
        private SpriteRenderer[] renderers;
        private float lastSortingY = float.NaN;

        public void Configure(SpriteRenderer spriteRenderer, UnityEngine.Sprite[] frames, float fps = 6f)
        {
            target = spriteRenderer;
            idleFrames = frames;
            framesPerSecond = Mathf.Max(0.1f, fps);
            LockCurrentScale();
            elapsed = 0f;
            RefreshFrame();
        }

        private void Awake()
        {
            LockCurrentScale();
            CacheRenderers();
            UpdateDepthSorting();
        }

        private void OnEnable()
        {
            if (!hasLockedScale)
                LockCurrentScale();
            elapsed = 0f;
            RefreshFrame();
            CacheRenderers();
            UpdateDepthSorting();
        }

        private void Update()
        {
            if (target == null || idleFrames == null || idleFrames.Length == 0)
                return;

            elapsed += UnityEngine.Time.deltaTime;
            RefreshFrame();
        }

        private void LateUpdate()
        {
            if (!Mathf.Approximately(lastSortingY, transform.position.y))
                UpdateDepthSorting();
        }

        private void RefreshFrame()
        {
            if (target == null || idleFrames == null || idleFrames.Length == 0)
                return;

            int frame = Mathf.FloorToInt(elapsed * framesPerSecond) % idleFrames.Length;
            if (hasLockedScale)
                target.transform.localScale = lockedLocalScale;
            target.sprite = idleFrames[frame];
        }

        private void LockCurrentScale()
        {
            if (target == null)
                return;

            lockedLocalScale = target.transform.localScale;
            hasLockedScale = true;
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void UpdateDepthSorting()
        {
            if (target == null)
                return;

            if (renderers == null || renderers.Length == 0)
                CacheRenderers();

            lastSortingY = transform.position.y;
            int bodyOrder = Mathf.RoundToInt(-lastSortingY * 100f);
            target.sortingLayerName = "Dynamic";
            target.sortingOrder = bodyOrder;

            // Dialogue/status icons remain one step above the NPC while the NPC
            // as a whole is compared with the player and map props by foot Y.
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer == null || renderer == target)
                    continue;
                renderer.sortingLayerID = target.sortingLayerID;
                renderer.sortingOrder = bodyOrder + 1;
            }
        }
    }
}
