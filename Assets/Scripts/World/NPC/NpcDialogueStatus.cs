using UnityEngine;

namespace World.NPC
{
    [DisallowMultipleComponent]
    public sealed class NpcDialogueStatus : MonoBehaviour
    {
        public enum DialogueState
        {
            None,
            Ellipsis,
            Question,
            Exclamation
        }

        [SerializeField] private SpriteRenderer target;
        [SerializeField] private UnityEngine.Sprite ellipsis;
        [SerializeField] private UnityEngine.Sprite question;
        [SerializeField] private UnityEngine.Sprite exclamation;
        [SerializeField] private DialogueState state;

        public DialogueState State => state;

        public void Configure(SpriteRenderer spriteRenderer, UnityEngine.Sprite ellipsisSprite,
            UnityEngine.Sprite questionSprite, UnityEngine.Sprite exclamationSprite)
        {
            target = spriteRenderer;
            ellipsis = ellipsisSprite;
            question = questionSprite;
            exclamation = exclamationSprite;
            EnsureBehindNpc();
            SetState(DialogueState.None);
        }

        public void SetState(DialogueState newState)
        {
            state = newState;
            if (target == null)
                return;

            target.sprite = GetSprite(newState);
            target.enabled = newState != DialogueState.None && target.sprite != null;
        }

        public void Hide() => SetState(DialogueState.None);
        public void ShowEllipsis() => SetState(DialogueState.Ellipsis);
        public void ShowQuestion() => SetState(DialogueState.Question);
        public void ShowExclamation() => SetState(DialogueState.Exclamation);

        private const float BobAmplitude = 0.02f;
        private const float BobCyclesPerSecond = 1.2f;
        private Vector3 baseLocalPosition = new Vector3(0f, 0.462f, 0.01f);
        private bool hasBasePosition;

        private void Awake()
        {
            EnsureBehindNpc();
            SetState(state);
        }

        private void Update()
        {
            if (target == null || !target.enabled)
                return;

            if (!hasBasePosition)
                EnsureBehindNpc();

            float bob = Mathf.Sin(UnityEngine.Time.time * Mathf.PI * 2f * BobCyclesPerSecond) * BobAmplitude;
            target.transform.localPosition = new Vector3(baseLocalPosition.x, baseLocalPosition.y + bob, baseLocalPosition.z);
        }

        private void EnsureBehindNpc()
        {
            if (target == null)
                return;

            SpriteRenderer body = GetComponent<SpriteRenderer>();
            if (body != null)
            {
                target.sortingLayerID = body.sortingLayerID;
                target.sortingOrder = body.sortingOrder - 1;
            }
            else
            {
                target.sortingOrder = -1;
            }

            Vector3 localPos = target.transform.localPosition;
            float targetY = Mathf.Approximately(localPos.y, 0.29f) ? 0.462f : localPos.y;
            float targetZ = localPos.z <= 0f ? 0.01f : localPos.z;
            baseLocalPosition = new Vector3(localPos.x, targetY, targetZ);
            target.transform.localPosition = baseLocalPosition;
            hasBasePosition = true;
        }

        private UnityEngine.Sprite GetSprite(DialogueState dialogueState)
        {
            switch (dialogueState)
            {
                case DialogueState.Ellipsis: return ellipsis;
                case DialogueState.Question: return question;
                case DialogueState.Exclamation: return exclamation;
                default: return null;
            }
        }
    }
}
