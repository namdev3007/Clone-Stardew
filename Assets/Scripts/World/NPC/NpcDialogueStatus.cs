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

        private void Awake()
        {
            SetState(state);
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
