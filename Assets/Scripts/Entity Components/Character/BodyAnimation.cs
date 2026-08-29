using Entity_Components.Interfaces;
using Referencing;
using UnityEngine;

namespace Entity_Components.Character
{
    [AddComponentMenu("Farming Kit/Entity Components/Body/Animation")]
    [RequireComponent(typeof(Animator))]
    public class BodyAnimation : MonoBehaviour, IMove, IAim
    {
        protected Animator animator;

        protected int directionXID;
        protected int directionYID;
        protected int velocityID;
        protected int useSpeedID;
        protected int triggerFinishedAction;

        protected Vector2 lastDirection;
        protected float lastVelocity;

        private bool initialized = false;

        [SerializeField]
        private SoundCollection walkingSteps;
        private AudioSource audioSource;
        private FullBodyPlayerSpriteAnimator fullBodyAnimator;

        [SerializeField]
        private SpriteRenderer toolSprite;

        public void OnStep()
        {
            if (walkingSteps != null)
            {
                if (audioSource == null)
                {
                    audioSource = GetComponent<AudioSource>();
                }

                walkingSteps.Play(audioSource);
            }
        }

        private void Initialize()
        {
            animator = GetComponent<Animator>();
            fullBodyAnimator = GetComponent<FullBodyPlayerSpriteAnimator>();

            directionXID = Animator.StringToHash("Direction X");
            directionYID = Animator.StringToHash("Direction Y");
            velocityID = Animator.StringToHash("Velocity");
            useSpeedID = Animator.StringToHash("Use Speed");
            triggerFinishedAction = Animator.StringToHash("Finished Action");

            initialized = true;
        }

        public float ApplySmashAnimation(float speed, UnityEngine.Sprite sprite)
        {
            PlayAnimation("Smash", speed, sprite);
            fullBodyAnimator?.PlayAction(FullBodyPlayerSpriteAnimator.ActionType.Chop, speed);

            // For simplicity, all action animations are set to be 1 seconds within the animation tab.
            return 1 / speed;
        }

        public float ApplySlashAnimation(float speed, UnityEngine.Sprite sprite)
        {
            PlayAnimation("Slash", speed, sprite);
            fullBodyAnimator?.PlayAction(FullBodyPlayerSpriteAnimator.ActionType.Chop, speed);

            // For simplicity, all action animations are set to be 1 seconds within the animation tab.
            return 1 / speed;
        }

        public float ApplyDropAnimation(float speed, UnityEngine.Sprite sprite)
        {
            PlayAnimation("Drop", speed, sprite);
            fullBodyAnimator?.PlayAction(FullBodyPlayerSpriteAnimator.ActionType.Water, speed);

            // For simplicity, all action animations are set to be 1 seconds within the animation tab.
            return 1 / speed;
        }

        public float ApplyHoeAnimation(float speed, UnityEngine.Sprite sprite)
        {
            PlayAnimation("Smash", speed, sprite);
            fullBodyAnimator?.PlayAction(FullBodyPlayerSpriteAnimator.ActionType.Hoe, speed);
            return 1 / speed;
        }

        private void PlayAnimation (string animationName, float speed, UnityEngine.Sprite sprite)
        {
            animator.SetFloat(useSpeedID, speed);
            animator.PlayInFixedTime(animationName);
            animator.SetFloat(velocityID, 0);

            if (toolSprite != null)
            {
                toolSprite.sprite = sprite;
            }
        }

        public void OnMove(Vector2 direction, float velocity)
        {
            UpdateAnimationParameters(direction, velocity);
        }

        public void OnAim(Vector2 direction)
        {
            UpdateAnimationParameters(direction, 0);
        }

        private void UpdateAnimationParameters(Vector2 direction, float velocity)
        {
            if (!initialized)
            {
                Initialize();
            }

            if (direction.sqrMagnitude > 0)
            {
                if (Mathf.Abs(direction.x) == Mathf.Abs(direction.y))
                {
                    direction.y = 0;
                }

                lastDirection = direction.normalized;
            }

            animator.SetFloat(directionXID, lastDirection.x);
            animator.SetFloat(directionYID, lastDirection.y);
            animator.SetFloat(velocityID, velocity);
            fullBodyAnimator?.SetMovement(lastDirection, velocity);

            lastVelocity = velocity;
        }
    }
}
