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

        public void Configure(SpriteRenderer spriteRenderer, UnityEngine.Sprite[] frames, float fps = 6f)
        {
            target = spriteRenderer;
            idleFrames = frames;
            framesPerSecond = Mathf.Max(0.1f, fps);
            elapsed = 0f;
            RefreshFrame();
        }

        private void OnEnable()
        {
            elapsed = 0f;
            RefreshFrame();
        }

        private void Update()
        {
            if (target == null || idleFrames == null || idleFrames.Length == 0)
                return;

            elapsed += UnityEngine.Time.deltaTime;
            RefreshFrame();
        }

        private void RefreshFrame()
        {
            if (target == null || idleFrames == null || idleFrames.Length == 0)
                return;

            int frame = Mathf.FloorToInt(elapsed * framesPerSecond) % idleFrames.Length;
            target.sprite = idleFrames[frame];
        }
    }
}
